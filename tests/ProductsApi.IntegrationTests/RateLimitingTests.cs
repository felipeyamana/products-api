using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductsApi.Controllers;
using ProductsApi.Security;
using Xunit;

namespace ProductsApi.IntegrationTests;

public sealed class RateLimitingTests
{
    [Fact]
    public void Defaults_AreSuitableForHumanAuthAndAggregateCatalogTraffic()
    {
        var options = new RateLimitingOptions();

        Assert.Equal(10, options.Auth.PermitLimit);
        Assert.Equal(60, options.ServiceToken.PermitLimit);
        Assert.Equal(1000, options.Products.PermitLimit);
        Assert.All(
            [options.Auth, options.ServiceToken, options.Products],
            window => Assert.Equal(60, window.WindowSeconds));
    }

    [Fact]
    public async Task ServiceTokenLimit_CanBeOverriddenThroughConfiguration()
    {
        await using var factory = new RateLimitFactory();
        using var client = factory.CreateClient();

        var first = await CreateTokenAsync(client);
        var second = await CreateTokenAsync(client);
        var rejected = await CreateTokenAsync(client);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    private static async Task<HttpResponseMessage> CreateTokenAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token")
        {
            Content = JsonContent.Create(
                new TokenRequest("rate-limit-tests", ["ProductManager"]))
        };
        request.Headers.Add("X-API-Key", "rate-limit-test-api-key");
        return await client.SendAsync(request);
    }

    private sealed class RateLimitFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ProductsApiRateLimitTests;User Id=sa;Password=Placeholder_password_123;TrustServerCertificate=True",
                    ["Database:MigrateOnStartup"] = "false",
                    ["Jwt:Issuer"] = "rate-limit-tests",
                    ["Jwt:Audience"] = "rate-limit-tests",
                    ["Jwt:Key"] = "rate-limit-test-signing-key-with-more-than-32-characters",
                    ["Jwt:ApiKey"] = "rate-limit-test-api-key",
                    ["Jwt:RoleClaimType"] = "role",
                    ["Jwt:ExpireMinutes"] = "30",
                    ["ECommerceJwt:Issuer"] = TestECommerceJwt.Issuer,
                    ["ECommerceJwt:Audience"] = TestECommerceJwt.Audience,
                    ["ECommerceJwt:PublicKey"] = TestECommerceJwt.PublicKey,
                    ["ECommerceJwt:KeyId"] = TestECommerceJwt.KeyId,
                    ["Redis:Enabled"] = "false",
                    ["RateLimiting:ServiceToken:PermitLimit"] = "2"
                }));

            return base.CreateHost(builder);
        }
    }
}
