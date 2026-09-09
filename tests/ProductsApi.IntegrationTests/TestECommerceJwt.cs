using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ProductsApi.IntegrationTests;

internal static class TestECommerceJwt
{
    public const string Issuer = "ecommerce-api-tests";
    public const string Audience = "products-api-cart-tests";
    public const string KeyId = "ecommerce-test-1";

    private static readonly byte[] PrivateKey;
    public static string PublicKey { get; }

    static TestECommerceJwt()
    {
        using var rsa = RSA.Create(2048);
        PrivateKey = rsa.ExportPkcs8PrivateKey();
        PublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }

    public static string CreateToken(
        string subject,
        string scope,
        string keyId = KeyId)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(PrivateKey, out _);
        var securityKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true))
        {
            KeyId = keyId
        };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim("role", "CartUser"),
                new Claim("scope", scope)
            ],
            now,
            now.AddMinutes(5),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
