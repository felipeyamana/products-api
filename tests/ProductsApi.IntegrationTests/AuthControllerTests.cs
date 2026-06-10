using ProductsApi.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace ProductsApi.IntegrationTests;

public class AuthControllerTests
{
    private const string ApiKey = "test-api-key";
    private const string Audience = "products-api-tests";
    private const string Issuer = "products-api-tests";
    private const string SigningKey = "test-signing-key-with-at-least-32-characters";

    [Fact]
    public async Task TokenEndpoint_ProtectsAndIssuesValidJwt()
    {
        await using var factory = new ProductsApiFactory();
        using var client = factory.CreateClient();

        var unauthorizedResponse = await client.PostAsJsonAsync(
            "/api/auth/token",
            new TokenRequest("integration-test", ["ProductManager"]));

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);

        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token");
        validRequest.Headers.Add("X-API-Key", ApiKey);
        validRequest.Content = JsonContent.Create(new TokenRequest("integration-test", ["ProductManager"]));

        var validResponse = await client.SendAsync(validRequest);

        validResponse.EnsureSuccessStatusCode();
        var tokenResponse = await validResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokenResponse);
        Assert.Equal("Bearer", tokenResponse.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.AccessToken));

        var principal = ValidateToken(tokenResponse.AccessToken);
        Assert.Equal("integration-test", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("ProductManager", principal.FindFirstValue("role"));

        using var invalidRoleRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/token");
        invalidRoleRequest.Headers.Add("X-API-Key", ApiKey);
        invalidRoleRequest.Content = JsonContent.Create(new TokenRequest("integration-test", ["Reader"]));

        var invalidRoleResponse = await client.SendAsync(invalidRoleRequest);

        Assert.Equal(HttpStatusCode.BadRequest, invalidRoleResponse.StatusCode);
    }

    private static ClaimsPrincipal ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        return handler.ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = "role"
            },
            out _);
    }

    private sealed class ProductsApiFactory : WebApplicationFactory<Program>
    {
        private static readonly Dictionary<string, string?> TestSettings = new()
        {
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=ProductsApiTests;User Id=sa;Password=Placeholder_password_123;TrustServerCertificate=True",
            ["Jwt__Audience"] = Audience,
            ["Jwt__ExpireMinutes"] = "30",
            ["Jwt__Issuer"] = Issuer,
            ["Jwt__Key"] = SigningKey,
            ["Jwt__ApiKey"] = ApiKey,
            ["Redis__Enabled"] = "false",
            ["Redis__RegisterNullCacheWhenDisabled"] = "false"
        };

        public ProductsApiFactory()
        {
            foreach (var setting in TestSettings)
            {
                Environment.SetEnvironmentVariable(setting.Key, setting.Value);
            }
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            foreach (var setting in TestSettings)
            {
                Environment.SetEnvironmentVariable(setting.Key, null);
            }
        }
    }
}
