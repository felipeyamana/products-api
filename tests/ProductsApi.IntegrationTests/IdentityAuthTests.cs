using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductsApi.Controllers;
using ProductsApi.Features.Auth.RegisterUser;
using ProductsApi.Features.Auth.Shared;
using Xunit;

namespace ProductsApi.IntegrationTests;

[Collection(MsSqlCollection.Name)]
public sealed class IdentityAuthTests(MsSqlContainerFixture fixture)
{
    private const string ApiKey = "identity-auth-test-api-key";
    private const string Audience = "identity-auth-tests";
    private const string Issuer = "identity-auth-tests";
    private const string SigningKey = "identity-auth-test-signing-key-with-32-characters";

    [Fact]
    public async Task RegisterAndLogin_CreateCustomerAndValidatePassword()
    {
        if (!fixture.IsEnabled)
        {
            return;
        }

        var email = $"shopper-{Guid.NewGuid():N}@example.com";
        await using var factory = new ProductsApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest(email, "Password1", "Password1"))
        };
        registerRequest.Headers.Add("X-API-Key", ApiKey);
        registerRequest.Headers.Add("X-Client-IP", "127.0.0.10");

        using var registerResponse = await client.SendAsync(registerRequest);
        registerResponse.EnsureSuccessStatusCode();
        var registeredUser = await registerResponse.Content.ReadFromJsonAsync<AuthenticatedUserDto>();
        Assert.NotNull(registeredUser);
        Assert.Equal(email, registeredUser.Email);

        await using (var db = fixture.CreateDbContext())
        {
            var customer = await db.Customers.SingleAsync(x => x.UserId == registeredUser.Id);
            Assert.True(customer.Id > 0);
        }

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, "Password1"))
        };
        loginRequest.Headers.Add("X-API-Key", ApiKey);
        loginRequest.Headers.Add("X-Client-IP", "127.0.0.10");

        using var loginResponse = await client.SendAsync(loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var loggedInUser = await loginResponse.Content.ReadFromJsonAsync<AuthenticatedUserDto>();
        Assert.Equal(registeredUser.Id, loggedInUser!.Id);
    }

    private sealed class ProductsApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:ExpireMinutes"] = "30",
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Key"] = SigningKey,
                    ["Jwt:ApiKey"] = ApiKey,
                    ["ECommerceJwt:Issuer"] = TestECommerceJwt.Issuer,
                    ["ECommerceJwt:Audience"] = TestECommerceJwt.Audience,
                    ["ECommerceJwt:PublicKey"] = TestECommerceJwt.PublicKey,
                    ["ECommerceJwt:KeyId"] = TestECommerceJwt.KeyId,
                    ["Redis:Enabled"] = "false",
                    ["Redis:RegisterNullCacheWhenDisabled"] = "false",
                    ["Database:MigrateOnStartup"] = "false"
                }));
        }
    }
}
