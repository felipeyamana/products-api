using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProductsApi.Controllers;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Cart;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class CartTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    private long[] productIds = [];

    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
        await using var db = fixture.CreateDbContext();

        var category = new Category { Name = "Cart test category" };
        var products = Enumerable.Range(1, 51)
            .Select(number => CreateProduct(number, category))
            .ToArray();

        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // Tests must use generated IDs because deleting rows does not reset SQL identity values.
        productIds = products.Select(x => x.Id).ToArray();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CartEndpoints_RequireECommerceTokenAndMatchingScope()
    {
        if (!fixture.IsEnabled) return;

        // Arrange
        await using var factory = new CartFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        // Anonymous requests and Products API service tokens cannot access shopper carts.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
        Authenticate(client, await CreateServiceTokenAsync(client));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);

        // A valid signature with an unknown key ID is rejected.
        Authenticate(client, TestECommerceJwt.CreateToken("alice", "cart:read", "unknown-key"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);

        // A read-only shopper token authenticates but cannot mutate a cart.
        Authenticate(client, TestECommerceJwt.CreateToken("alice", "cart:read"));
        var response = await client.PutAsJsonAsync(
            $"/api/cart/items/{ProductId(1)}",
            new SetCartItemRequest(1, Guid.Empty));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CartEndpoints_PersistAndIsolateShopperCarts()
    {
        if (!fixture.IsEnabled) return;

        // Arrange: Alice starts with an empty cart.
        await using var factory = new CartFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(client, ShopperToken("alice"));
        var emptyCart = (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;

        // Mutations require the version returned by the latest cart response.
        var missingVersion = await client.PutAsJsonAsync(
            $"/api/cart/items/{ProductId(1)}",
            new { quantity = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);

        // Act: Alice adds two units of the first product.
        var addedResponse = await client.PutAsJsonAsync(
            $"/api/cart/items/{ProductId(1)}",
            new SetCartItemRequest(2, emptyCart.Version));
        addedResponse.EnsureSuccessStatusCode();
        var aliceCart = (await addedResponse.Content.ReadFromJsonAsync<CartDto>())!;

        // Assert: totals are calculated by the API and stale versions are rejected.
        Assert.Equal(180m, aliceCart.Subtotal);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.DeleteAsync($"/api/cart?version={emptyCart.Version}")).StatusCode);

        // Bob has a different cart even when using the same API endpoint.
        Authenticate(client, ShopperToken("bob"));
        Assert.Empty((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);

        // Alice can remove her item using her latest version.
        Authenticate(client, ShopperToken("alice"));
        var removedResponse = await client.DeleteAsync(
            $"/api/cart/items/{ProductId(1)}?version={aliceCart.Version}");
        removedResponse.EnsureSuccessStatusCode();
        Assert.Empty((await removedResponse.Content.ReadFromJsonAsync<CartDto>())!.Items);
    }

    [Fact]
    public async Task QuantityChange_PreservesOriginalPriceSnapshot()
    {
        if (!fixture.IsEnabled) return;

        // Arrange: add a product while its current price is 90.
        var addedCart = await SetSuccessfully("alice", ProductId(1), 1, Guid.Empty);
        Assert.Equal(90m, Assert.Single(addedCart.Items).UnitPriceAtAddition);

        // Act: the catalog price changes to 100 before the shopper changes quantity.
        await SetCatalogPrice(ProductId(1), 100m);
        var updatedCart = await SetSuccessfully(
            "alice",
            ProductId(1),
            2,
            addedCart.Version);

        // Assert: checkout uses the current total while retaining the original snapshot.
        var item = Assert.Single(updatedCart.Items);
        Assert.Equal(90m, item.UnitPriceAtAddition);
        Assert.Equal(100m, item.CurrentUnitPrice);
        Assert.True(item.PriceChanged);
        Assert.Equal(200m, updatedCart.Subtotal);
    }

    [Fact]
    public async Task ConcurrentMutations_OnlyAcceptOneRequestPerVersion()
    {
        if (!fixture.IsEnabled) return;

        // Two first additions race using the empty-cart version.
        var firstResults = await Task.WhenAll(
            Set("alice", ProductId(1), 1, Guid.Empty),
            Set("alice", ProductId(2), 1, Guid.Empty));

        AssertSingleSuccessAndConflict(firstResults);
        var cart = firstResults.Single(x => x.StatusCode == 200).Value!;

        // Two updates then race using the same current cart version.
        var productId = Assert.Single(cart.Items).ProductId;
        var updateResults = await Task.WhenAll(
            Set("alice", productId, 2, cart.Version),
            Set("alice", productId, 3, cart.Version));

        AssertSingleSuccessAndConflict(updateResults);
    }

    [Fact]
    public async Task SetItem_RejectsInvalidQuantityProductAndCurrency()
    {
        if (!fixture.IsEnabled) return;

        // Quantity and product validation happen before anything is persisted.
        Assert.Equal(400, (await Set("alice", ProductId(1), 100, Guid.Empty)).StatusCode);
        Assert.Equal(400, (await Set("alice", ProductId(1), 0, Guid.Empty)).StatusCode);
        Assert.Equal(400, (await Set("alice", long.MaxValue, 1, Guid.Empty)).StatusCode);

        // A cart cannot combine products whose current currencies differ.
        var cart = await SetSuccessfully("alice", ProductId(1), 1, Guid.Empty);
        await SetCatalogCurrency(ProductId(2), "EUR");

        var mixedCurrency = await Set("alice", ProductId(2), 1, cart.Version);
        Assert.Equal(400, mixedCurrency.StatusCode);
    }

    [Fact]
    public async Task Cart_EnforcesItemLimitAndRetainsDeletedProducts()
    {
        if (!fixture.IsEnabled) return;

        // Arrange: fill the cart to its 50-item limit.
        var cart = await SetSuccessfully("alice", ProductId(1), 1, Guid.Empty);
        for (var number = 2; number <= 50; number++)
        {
            cart = await SetSuccessfully(
                "alice",
                ProductId(number),
                1,
                cart.Version);
        }

        // The 51st distinct product is rejected.
        var overLimit = await Set("alice", ProductId(51), 1, cart.Version);
        Assert.Equal(400, overLimit.StatusCode);

        // Deleting catalog data must not silently delete the shopper's saved line.
        await DeleteProduct(ProductId(1));
        await using var db = fixture.CreateDbContext();
        var service = CreateCartService(db);
        cart = (await service.GetAsync("alice", default)).Value!;

        Assert.False(cart.Items.Single(x => x.ProductId == ProductId(1)).IsAvailable);
        Assert.Null(cart.Subtotal);

        // Clearing advances the version; removing an absent item is idempotent.
        var cleared = (await service.ClearAsync("alice", cart.Version, default)).Value!;
        Assert.Empty(cleared.Items);
        Assert.Equal(0m, cleared.Subtotal);
        Assert.Equal(
            cleared.Version,
            (await service.RemoveAsync("alice", ProductId(1), cleared.Version, default)).Value!.Version);
    }

    private async Task<CartResult> Set(
        string userId,
        long productId,
        int quantity,
        Guid version)
    {
        await using var db = fixture.CreateDbContext();
        return await CreateCartService(db).SetAsync(
            userId,
            productId,
            new SetCartItemRequest(quantity, version),
            default);
    }

    private async Task<CartDto> SetSuccessfully(
        string userId,
        long productId,
        int quantity,
        Guid version)
    {
        var result = await Set(userId, productId, quantity, version);
        Assert.Equal(200, result.StatusCode);
        return Assert.IsType<CartDto>(result.Value);
    }

    private async Task SetCatalogPrice(long productId, decimal price)
    {
        await using var db = fixture.CreateDbContext();
        var currentPrice = await db.ProductPrices.SingleAsync(x => x.ProductId == productId);
        currentPrice.ActualPrice = price;
        await db.SaveChangesAsync();
    }

    private async Task SetCatalogCurrency(long productId, string currency)
    {
        await using var db = fixture.CreateDbContext();
        var currentPrice = await db.ProductPrices.SingleAsync(x => x.ProductId == productId);
        currentPrice.CurrencyCode = currency;
        await db.SaveChangesAsync();
    }

    private async Task DeleteProduct(long productId)
    {
        await using var db = fixture.CreateDbContext();
        await db.Products.Where(x => x.Id == productId).ExecuteDeleteAsync();
    }

    private static CartService CreateCartService(AppDbContext db) =>
        new(db, new CartLockManager(db));

    private long ProductId(int productNumber) => productIds[productNumber - 1];

    private static string ShopperToken(string userId) =>
        TestECommerceJwt.CreateToken(userId, "cart:read cart:write");

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string> CreateServiceTokenAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token");
        request.Headers.Add("X-API-Key", "cart-test-key");
        request.Content = JsonContent.Create(new TokenRequest("service", ["ProductManager"]));

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }

    private static void AssertSingleSuccessAndConflict(IEnumerable<CartResult> results)
    {
        Assert.Single(results, x => x.StatusCode == 200);
        Assert.Single(results, x => x.StatusCode == 409);
    }

    private static Product CreateProduct(int number, Category category) => new()
    {
        Name = $"Product {number}",
        Category = category,
        Prices =
        [
            new ProductPrice
            {
                ActualPrice = 90m,
                DiscountPrice = 100m,
                CurrencyCode = "USD",
                CapturedAt = DateTime.UtcNow
            }
        ]
    };

    private sealed class CartFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Database:MigrateOnStartup"] = "false",
                    ["Jwt:Issuer"] = "cart-tests",
                    ["Jwt:Audience"] = "cart-tests",
                    ["Jwt:Key"] = "cart-test-signing-key-with-more-than-32-characters",
                    ["Jwt:ApiKey"] = "cart-test-key",
                    ["Jwt:RoleClaimType"] = "role",
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
}
