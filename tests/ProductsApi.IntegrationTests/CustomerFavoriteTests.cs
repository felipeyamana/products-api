using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Favorites.Shared;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class CustomerFavoriteTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    private Guid customerUserId;
    private Guid otherUserId;
    private long productId;

    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
        customerUserId = await CreateCustomerAsync();
        otherUserId = await CreateCustomerAsync();
        productId = await CreateProductAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FavoriteEndpoints_RequireCustomerRoleAndScopes()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new FavoriteFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/customers/me/favorites")).StatusCode);

        Authenticate(
            client,
            TestECommerceJwt.CreateToken(
                customerUserId.ToString(),
                "favorites:read",
                role: "CartUser"));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/customers/me/favorites")).StatusCode);

        Authenticate(client, CustomerToken(customerUserId, "favorites:read"));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsync($"/api/customers/me/favorites/{productId}", null)).StatusCode);
    }

    [Fact]
    public async Task FavoriteEndpoints_AddListAndRemoveIdempotentlyPerCustomer()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new FavoriteFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(
            client,
            CustomerToken(customerUserId, "favorites:read favorites:write"));

        var addResponse = await client.PutAsync(
            $"/api/customers/me/favorites/{productId}",
            null);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var added = (await addResponse.Content.ReadFromJsonAsync<CustomerFavoriteDto>())!;
        Assert.Equal(productId, added.Product.Id);
        Assert.Equal("Favorite product", added.Product.Name);
        Assert.Equal(49.99m, added.Product.CurrentPrice);

        var duplicateResponse = await client.PutAsync(
            $"/api/customers/me/favorites/{productId}",
            null);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

        await using (var dbContext = fixture.CreateDbContext())
        {
            Assert.Equal(1, await dbContext.CustomerFavorites.CountAsync());
        }

        var favorites = (await client.GetFromJsonAsync<CustomerFavoriteDto[]>(
            "/api/customers/me/favorites"))!;
        Assert.Equal(productId, Assert.Single(favorites).Product.Id);

        Authenticate(
            client,
            CustomerToken(otherUserId, "favorites:read favorites:write"));
        Assert.Empty((await client.GetFromJsonAsync<CustomerFavoriteDto[]>(
            "/api/customers/me/favorites"))!);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/customers/me/favorites/{productId}")).StatusCode);

        Authenticate(
            client,
            CustomerToken(customerUserId, "favorites:read favorites:write"));
        Assert.Single((await client.GetFromJsonAsync<CustomerFavoriteDto[]>(
            "/api/customers/me/favorites"))!);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/customers/me/favorites/{productId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/customers/me/favorites/{productId}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CustomerFavoriteDto[]>(
            "/api/customers/me/favorites"))!);
    }

    [Fact]
    public async Task AddFavorite_ReturnsNotFoundForUnknownProduct()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new FavoriteFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(client, CustomerToken(customerUserId, "favorites:write"));

        var response = await client.PutAsync(
            "/api/customers/me/favorites/9223372036854775807",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"favorite-{userId:N}@example.com";
        await using var dbContext = fixture.CreateDbContext();

        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        await dbContext.SaveChangesAsync();

        dbContext.Customers.Add(new Customer { UserId = userId });
        await dbContext.SaveChangesAsync();

        return userId;
    }

    private async Task<long> CreateProductAsync()
    {
        await using var dbContext = fixture.CreateDbContext();
        var category = new Category { Name = "Favorites" };
        var product = new Product
        {
            Name = "Favorite product",
            Brand = "Acme",
            Category = category,
            AverageRating = 4.75m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Prices =
            [
                new ProductPrice
                {
                    ActualPrice = 49.99m,
                    CurrencyCode = "USD",
                    CapturedAt = DateTime.UtcNow
                }
            ]
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return product.Id;
    }

    private static string CustomerToken(Guid userId, string scope) =>
        TestECommerceJwt.CreateToken(
            userId.ToString(),
            scope,
            role: "CustomerUser");

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private sealed class FavoriteFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Database:MigrateOnStartup"] = "false",
                    ["Jwt:Issuer"] = "favorite-tests",
                    ["Jwt:Audience"] = "favorite-tests",
                    ["Jwt:Key"] = "favorite-test-signing-key-with-more-than-32-characters",
                    ["Jwt:ApiKey"] = "favorite-test-api-key",
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
