using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Customers.Shared;
using ProductsApi.Features.Orders.Shared;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class CustomerOrderTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    private TestData data = null!;

    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
        data = await CreateTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CustomerProfile_CanBeReadAndUpdatedWithConcurrency()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(
            client,
            Token(data.UserId, "customer:read customer:write", "CustomerUser"));

        var profile = await client.GetFromJsonAsync<CustomerProfileDto>(
            "/api/customers/me");
        Assert.NotNull(profile);
        Assert.Equal(data.Email, profile.Email);

        var update = new UpdateCustomerProfileRequest(
            "  Felipe ",
            " Customer  ",
            "+55 11 99999-0000",
            profile.Version);
        var updateResponse = await client.PutAsJsonAsync(
            "/api/customers/me",
            update);
        updateResponse.EnsureSuccessStatusCode();

        var updated =
            (await updateResponse.Content.ReadFromJsonAsync<CustomerProfileDto>())!;
        Assert.Equal("Felipe", updated.FirstName);
        Assert.Equal("Customer", updated.LastName);

        var staleResponse = await client.PutAsJsonAsync(
            "/api/customers/me",
            update);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task OrderEndpoints_CreateSnapshotsClearCartAndEnforceOwnership()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AccountFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(
            client,
            Token(data.UserId, "orders:read orders:write", "OrderUser"));

        var createResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order =
            (await createResponse.Content.ReadFromJsonAsync<OrderDetailDto>())!;

        Assert.Equal("Pending", order.Status);
        Assert.Equal(data.Email, order.CustomerEmail);
        Assert.Equal("Home recipient", order.ShippingAddress.RecipientName);
        Assert.Equal(50m, order.GrandTotal);
        Assert.Equal(2, Assert.Single(order.Items).Quantity);

        var retryResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderRequest(data.AddressId, data.CartVersion));
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        var retriedOrder =
            (await retryResponse.Content.ReadFromJsonAsync<OrderDetailDto>())!;
        Assert.Equal(order.Id, retriedOrder.Id);

        await using (var db = fixture.CreateDbContext())
        {
            var cart = await db.Carts
                .Include(item => item.Items)
                .SingleAsync(item => item.UserId == data.UserId.ToString());
            Assert.Empty(cart.Items);
            Assert.NotEqual(data.CartVersion, cart.Version);
        }

        var list = await client.GetFromJsonAsync<PagedOrdersDto>(
            "/api/orders?page=1&pageSize=20");
        Assert.Single(list!.Items);
        Assert.Equal(order.Id, list.Items[0].Id);

        var detail = await client.GetFromJsonAsync<OrderDetailDto>(
            $"/api/orders/{order.Id}");
        Assert.Equal(order.Id, detail!.Id);

        Authenticate(
            client,
            Token(data.OtherUserId, "orders:read", "OrderUser"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/orders/{order.Id}")).StatusCode);
    }

    private async Task<TestData> CreateTestDataAsync()
    {
        var userId = await CreateCustomerAsync();
        var otherUserId = await CreateCustomerAsync();
        var email = $"account-{userId:N}@example.com";
        var cartVersion = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();

        var user = await db.Users.SingleAsync(item => item.Id == userId);
        user.Email = email;
        user.NormalizedEmail = email.ToUpperInvariant();

        var customer = await db.Customers.SingleAsync(
            item => item.UserId == userId);
        var address = new CustomerAddress
        {
            CustomerId = customer.Id,
            Label = "Home",
            RecipientName = "Home recipient",
            PhoneNumber = "+55 11 99999-0000",
            AddressLine1 = "100 Main Street",
            City = "Sao Paulo",
            Region = "SP",
            PostalCode = "01000-000",
            CountryCode = "BR",
            IsDefault = true
        };
        var category = new Category { Name = $"Orders {Guid.NewGuid():N}" };
        var product = new Product
        {
            Name = "Order product",
            Category = category,
            ExternalProductId = $"order-{Guid.NewGuid():N}",
            Prices =
            [
                new ProductPrice
                {
                    ActualPrice = 25m,
                    CurrencyCode = "BRL",
                    CapturedAt = DateTime.UtcNow
                }
            ]
        };

        db.CustomerAddresses.Add(address);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.Carts.Add(new Cart
        {
            UserId = userId.ToString(),
            Version = cartVersion,
            Items =
            [
                new CartItem
                {
                    UserId = userId.ToString(),
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPriceAtAddition = 25m,
                    CurrencyAtAddition = "BRL",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            ]
        });
        await db.SaveChangesAsync();

        return new TestData(
            userId,
            otherUserId,
            email,
            address.PublicId,
            cartVersion);
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"account-{userId:N}@example.com";
        await using var db = fixture.CreateDbContext();

        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();

        db.Customers.Add(new Customer { UserId = userId });
        await db.SaveChangesAsync();

        return userId;
    }

    private static string Token(Guid userId, string scope, string role) =>
        TestECommerceJwt.CreateToken(
            userId.ToString(),
            scope,
            role: role);

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private sealed record TestData(
        Guid UserId,
        Guid OtherUserId,
        string Email,
        Guid AddressId,
        Guid CartVersion);

    private sealed class AccountFactory(string connectionString)
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
                    ["Jwt:Issuer"] = "account-tests",
                    ["Jwt:Audience"] = "account-tests",
                    ["Jwt:Key"] = "account-test-signing-key-with-more-than-32-characters",
                    ["Jwt:ApiKey"] = "account-test-api-key",
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
