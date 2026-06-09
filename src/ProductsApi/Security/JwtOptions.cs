namespace ProductsApi.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public int ExpireMinutes { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public string RoleClaimType { get; init; } = "role";
}
