namespace ProductsApi.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public bool Enabled { get; init; }
    public bool RegisterNullCacheWhenDisabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string InstanceName { get; init; } = "products-api:";
    public int ConnectTimeoutMilliseconds { get; set; } = 2000;
    public int SyncTimeoutMilliseconds { get; set; } = 2000;
    public int AsyncTimeoutMilliseconds { get; set; } = 2000;
}
