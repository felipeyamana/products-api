using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ProductsApi.Data.Entities;
using ProductsApi.Features.CustomerAddresses.Shared;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class CustomerAddressTests(MsSqlContainerFixture fixture) : IAsyncLifetime
{
    private Guid customerUserId;
    private Guid otherUserId;

    public async Task InitializeAsync()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        await fixture.ResetDatabaseAsync();
        customerUserId = await CreateCustomerAsync();
        otherUserId = await CreateCustomerAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddressEndpoints_RequireCustomerRoleAndScopes()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AddressFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/customers/me/addresses")).StatusCode);

        Authenticate(
            client,
            TestECommerceJwt.CreateToken(
                customerUserId.ToString(),
                "addresses:read",
                role: "CartUser"));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/customers/me/addresses")).StatusCode);

        Authenticate(client, CustomerToken(customerUserId, "addresses:read"));
        var create = await client.PostAsJsonAsync(
            "/api/customers/me/addresses",
            AddressRequest("Home"));

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task AddressEndpoints_ManageDefaultsConcurrencyAndOwnership()
    {
        if (!fixture.IsEnabled) return;

        await using var factory = new AddressFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Authenticate(
            client,
            CustomerToken(customerUserId, "addresses:read addresses:write"));

        var firstResponse = await client.PostAsJsonAsync(
            "/api/customers/me/addresses",
            AddressRequest("Home"));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<CustomerAddressDto>())!;
        Assert.True(first.IsDefault);
        Assert.Equal("BR", first.CountryCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/customers/me/addresses",
            AddressRequest("Work"));
        secondResponse.EnsureSuccessStatusCode();
        var second = (await secondResponse.Content.ReadFromJsonAsync<CustomerAddressDto>())!;
        Assert.False(second.IsDefault);

        var defaultResponse = await client.PutAsJsonAsync(
            $"/api/customers/me/addresses/{second.Id}/default",
            new SetDefaultCustomerAddressRequest(second.Version));
        defaultResponse.EnsureSuccessStatusCode();
        var secondDefault =
            (await defaultResponse.Content.ReadFromJsonAsync<CustomerAddressDto>())!;
        Assert.True(secondDefault.IsDefault);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/customers/me/addresses/{first.Id}",
            UpdateRequest("Old home", first.Version));
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

        var refreshedFirst = await client.GetFromJsonAsync<CustomerAddressDto>(
            $"/api/customers/me/addresses/{first.Id}");
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/customers/me/addresses/{first.Id}",
            UpdateRequest("New home", refreshedFirst!.Version));
        updateResponse.EnsureSuccessStatusCode();

        Authenticate(
            client,
            CustomerToken(otherUserId, "addresses:read addresses:write"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/customers/me/addresses/{first.Id}")).StatusCode);

        Authenticate(
            client,
            CustomerToken(customerUserId, "addresses:read addresses:write"));
        var encodedVersion = Uri.EscapeDataString(
            Convert.ToBase64String(secondDefault.Version));
        var deleteResponse = await client.DeleteAsync(
            $"/api/customers/me/addresses/{second.Id}?version={encodedVersion}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var addresses = (await client.GetFromJsonAsync<CustomerAddressDto[]>(
            "/api/customers/me/addresses"))!;
        var remaining = Assert.Single(addresses);
        Assert.Equal(first.Id, remaining.Id);
        Assert.True(remaining.IsDefault);
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"address-{userId:N}@example.com";
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

    private static CreateCustomerAddressRequest AddressRequest(string label) =>
        new(
            label,
            "Felipe Customer",
            "+55 11 99999-0000",
            "100 Main Street",
            null,
            "Sao Paulo",
            "SP",
            "01000-000",
            "br");

    private static UpdateCustomerAddressRequest UpdateRequest(
        string label,
        byte[] version) =>
        new(
            label,
            "Felipe Customer",
            "+55 11 99999-0000",
            "101 Main Street",
            "Apartment 1",
            "Sao Paulo",
            "SP",
            "01000-001",
            "BR",
            version);

    private static string CustomerToken(Guid userId, string scope) =>
        TestECommerceJwt.CreateToken(
            userId.ToString(),
            scope,
            role: "CustomerUser");

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private sealed class AddressFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Database:MigrateOnStartup"] = "false",
                    ["Jwt:Issuer"] = "address-tests",
                    ["Jwt:Audience"] = "address-tests",
                    ["Jwt:Key"] = "address-test-signing-key-with-more-than-32-characters",
                    ["Jwt:ApiKey"] = "address-test-api-key",
                    ["Jwt:RoleClaimType"] = "role",
                    ["Jwt:ExpireMinutes"] = "30",
                    ["ECommerceJwt:Issuer"] = TestECommerceJwt.Issuer,
                    ["ECommerceJwt:Audience"] = TestECommerceJwt.Audience,
                    ["ECommerceJwt:PublicKey"] = TestECommerceJwt.PublicKey,
                    ["ECommerceJwt:KeyId"] = TestECommerceJwt.KeyId,
                    ["Redis:Enabled"] = "false"
                }));
        }
    }
}
