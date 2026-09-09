using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ProductsApi.Security;

public sealed class ECommerceJwtOptions
{
    public const string SectionName = "ECommerceJwt";
    public const string AuthenticationScheme = "ECommerceJwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string PublicKey { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;

    public RsaSecurityKey CreateSecurityKey()
    {
        try
        {
            var publicKey = Convert.FromBase64String(PublicKey);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out var bytesRead);

            if (bytesRead != publicKey.Length || rsa.KeySize < 2048)
            {
                throw new InvalidOperationException(
                    "ECommerceJwt:PublicKey must be a complete RSA SubjectPublicKeyInfo key of at least 2048 bits.");
            }

            return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false))
            {
                KeyId = KeyId
            };
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            throw new InvalidOperationException(
                "ECommerceJwt:PublicKey must be a valid base64-encoded RSA SubjectPublicKeyInfo key.",
                exception);
        }
    }
}
