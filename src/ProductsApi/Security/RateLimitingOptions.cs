namespace ProductsApi.Security;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public FixedWindowRateLimitOptions Auth { get; set; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 60,
        QueueLimit = 0
    };

    public FixedWindowRateLimitOptions ServiceToken { get; set; } = new()
    {
        PermitLimit = 60,
        WindowSeconds = 60,
        QueueLimit = 0
    };

    public FixedWindowRateLimitOptions Products { get; set; } = new()
    {
        PermitLimit = 1000,
        WindowSeconds = 60,
        QueueLimit = 0
    };
}

public sealed class FixedWindowRateLimitOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
    public int QueueLimit { get; set; }
}
