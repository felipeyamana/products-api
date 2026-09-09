using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Cart;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ProductsApi.Controllers;
using Microsoft.Extensions.Hosting;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class CartTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled) return;

        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();
        var category = new Category { Name = "Cart test category" };
        for (int i = 1; i <= 51; i++)
            db.Products.Add(new Product { Name = $"Product {i}", Category = category,
                Prices = [new ProductPrice { ActualPrice = 90m, DiscountPrice = 100m, CurrencyCode = "USD", CapturedAt = DateTime.UtcNow }] });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<CartResult> Set(string user, long id, int quantity, Guid version)
    {
        await using var db = fixture.CreateDbContext();
        return await new CartService(db, new CartLockManager(db))
            .SetAsync(user, id, new(quantity, version), default);
    }

    [Fact]
    public async Task HttpContractAuthenticatesIsolatesAndChecksVersions()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new CartFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
        async Task<string> ServiceToken(string user)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token");
            request.Headers.Add("X-API-Key", "cart-test-key");
            request.Content = JsonContent.Create(new TokenRequest(user, ["ProductManager"]));
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        }
        var alice = TestECommerceJwt.CreateToken("alice", "cart:read cart:write");
        var bob = TestECommerceJwt.CreateToken("bob", "cart:read cart:write");
        var manager = await ServiceToken("service");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", manager);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestECommerceJwt.CreateToken("alice", "cart:read", "unknown-key"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice);
        var empty = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestECommerceJwt.CreateToken("alice", "cart:read"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync("/api/cart/items/1", new SetCartItemRequest(1, empty.Version))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/cart/items/1", new { quantity = 1 })).StatusCode);
        var addedResponse = await client.PutAsJsonAsync("/api/cart/items/1", new SetCartItemRequest(2, empty.Version));
        addedResponse.EnsureSuccessStatusCode();
        var cart = (await addedResponse.Content.ReadFromJsonAsync<CartDto>())!;
        Assert.Equal(180m, cart.Subtotal);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/cart?version={empty.Version}")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob);
        Assert.Empty((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice);
        var removed = await client.DeleteAsync($"/api/cart/items/1?version={cart.Version}");
        removed.EnsureSuccessStatusCode();
        Assert.Empty((await removed.Content.ReadFromJsonAsync<CartDto>())!.Items);
    }

    private sealed class CartFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:MigrateOnStartup"] = "false",
                ["Jwt:Issuer"] = "cart-tests", ["Jwt:Audience"] = "cart-tests",
                ["Jwt:Key"] = "cart-test-signing-key-with-more-than-32-characters",
                ["Jwt:ApiKey"] = "cart-test-key", ["Jwt:RoleClaimType"] = "role",
                ["Jwt:ExpireMinutes"] = "30",
                ["ECommerceJwt:Issuer"] = TestECommerceJwt.Issuer,
                ["ECommerceJwt:Audience"] = TestECommerceJwt.Audience,
                ["ECommerceJwt:PublicKey"] = TestECommerceJwt.PublicKey,
                ["ECommerceJwt:KeyId"] = TestECommerceJwt.KeyId,
                ["Redis:Enabled"] = "false"
            }));
            return base.CreateHost(builder);
        }
    }

    [Fact]
    public async Task PersistsSnapshotsAndPreservesThemOnQuantityChange()
    {
        if (!fixture.IsEnabled) return;

        var added = (await Set("alice", 1, 1, Guid.Empty)).Value!;
        Assert.Equal(90m, Assert.Single(added.Items).UnitPriceAtAddition);
        await using (var db = fixture.CreateDbContext())
        {
            var price = await db.ProductPrices.SingleAsync(x => x.ProductId == 1);
            price.ActualPrice = 100m;
            await db.SaveChangesAsync();
        }
        var edited = (await Set("alice", 1, 2, added.Version)).Value!;
        var item = Assert.Single(edited.Items);
        Assert.Equal(90m, item.UnitPriceAtAddition);
        Assert.Equal(100m, item.CurrentUnitPrice);
        Assert.True(item.PriceChanged);
        Assert.Equal(200m, edited.Subtotal);
        await using var readDb = fixture.CreateDbContext();
        var service = new CartService(readDb, new CartLockManager(readDb));
        Assert.Empty((await service.GetAsync("Alice", default)).Value!.Items);
        Assert.Equal(edited.Version, (await service.GetAsync("alice", default)).Value!.Version);
        Assert.Equal(409, (await service.ClearAsync("alice", added.Version, default)).StatusCode);
        Assert.Empty((await service.RemoveAsync("alice", 1, edited.Version, default)).Value!.Items);
    }

    [Fact]
    public async Task ConcurrentFirstAddsAndUpdatesOnlyAcceptOneVersion()
    {
        if (!fixture.IsEnabled) return;

        var results = await Task.WhenAll(Set("alice", 1, 1, Guid.Empty), Set("alice", 2, 1, Guid.Empty));
        Assert.Single(results, x => x.StatusCode == 200);
        Assert.Single(results, x => x.StatusCode == 409);
        var cart = results.Single(x => x.StatusCode == 200).Value!;
        results = await Task.WhenAll(Set("alice", cart.Items[0].ProductId, 2, cart.Version),
            Set("alice", cart.Items[0].ProductId, 3, cart.Version));
        Assert.Single(results, x => x.StatusCode == 200);
        Assert.Single(results, x => x.StatusCode == 409);
    }

    [Fact]
    public async Task EnforcesLimitsAndCurrencyAndRetainsDeletedProducts()
    {
        if (!fixture.IsEnabled) return;

        Assert.Equal(400, (await Set("alice", 1, 100, Guid.Empty)).StatusCode);
        Assert.Equal(400, (await Set("alice", 1, 0, Guid.Empty)).StatusCode);
        Assert.Equal(400, (await Set("alice", 9999, 1, Guid.Empty)).StatusCode);
        var cart = (await Set("alice", 1, 1, Guid.Empty)).Value!;
        await using (var db = fixture.CreateDbContext())
        {
            var price = await db.ProductPrices.SingleAsync(x => x.ProductId == 2);
            price.CurrencyCode = "EUR";
            await db.SaveChangesAsync();
        }
        Assert.Equal(400, (await Set("alice", 2, 1, cart.Version)).StatusCode);
        for (int i = 3; i <= 51; i++) cart = (await Set("alice", i, 1, cart.Version)).Value!;
        Assert.Equal(50, cart.Items.Count);
        await using (var db = fixture.CreateDbContext())
        {
            var price = await db.ProductPrices.SingleAsync(x => x.ProductId == 2);
            price.CurrencyCode = "USD";
            await db.SaveChangesAsync();
            await db.Products.Where(x => x.Id == 1).ExecuteDeleteAsync();
        }
        Assert.Equal(400, (await Set("alice", 2, 1, cart.Version)).StatusCode);
        await using var readDb = fixture.CreateDbContext();
        var service = new CartService(readDb, new CartLockManager(readDb));
        cart = (await service.GetAsync("alice", default)).Value!;
        Assert.False(cart.Items.Single(x => x.ProductId == 1).IsAvailable);
        Assert.Null(cart.Subtotal);
        var cleared = (await service.ClearAsync("alice", cart.Version, default)).Value!;
        Assert.Empty(cleared.Items);
        Assert.Equal(0m, cleared.Subtotal);
        Assert.Equal(cleared.Version, (await service.RemoveAsync("alice", 1, cleared.Version, default)).Value!.Version);
    }
}
