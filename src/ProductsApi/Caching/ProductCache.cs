using ProductsApi.Features.Products.Shared;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ProductsApi.Caching;

public sealed class ProductCache(IDistributedCache cache, ILogger<ProductCache> logger) : IProductCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions ProductOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
    private static readonly DistributedCacheEntryOptions PagedProductsOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
    };

    private const string ProductsVersionKey = "products:version";

    public async Task<PagedProductsDto?> GetPagedProductsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var version = await GetProductsVersionAsync(cancellationToken);
        return await GetAsync<PagedProductsDto>(GetPagedProductsKey(page, pageSize, version), cancellationToken);
    }

    public async Task SetPagedProductsAsync(PagedProductsDto products, CancellationToken cancellationToken)
    {
        var version = await GetProductsVersionAsync(cancellationToken);
        await SetAsync(GetPagedProductsKey(products.PageNumber, products.PageSize, version), products, PagedProductsOptions, cancellationToken);
    }

    public Task<ProductDto?> GetProductAsync(long id, CancellationToken cancellationToken) =>
        GetAsync<ProductDto>(GetProductKey(id), cancellationToken);

    public Task SetProductAsync(ProductDto product, CancellationToken cancellationToken) =>
        SetAsync(GetProductKey(product.Id), product, ProductOptions, cancellationToken);

    public Task InvalidateProductAsync(long id, CancellationToken cancellationToken) =>
        TryCacheOperationAsync(() => cache.RemoveAsync(GetProductKey(id), cancellationToken));

    public async Task InvalidateProductsAsync(CancellationToken cancellationToken)
    {
        var nextVersion = Guid.NewGuid().ToString("N");
        await TryCacheOperationAsync(() => cache.SetStringAsync(ProductsVersionKey, nextVersion, cancellationToken));
    }

    private async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var json = await cache.GetStringAsync(key, cancellationToken);
            return string.IsNullOrWhiteSpace(json)
                ? default
                : JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for key '{CacheKey}'.", key);
            return default;
        }
    }

    private async Task SetAsync<T>(
        string key,
        T value,
        DistributedCacheEntryOptions options,
        CancellationToken cancellationToken)
    {
        await TryCacheOperationAsync(() =>
            cache.SetStringAsync(key, JsonSerializer.Serialize(value, JsonOptions), options, cancellationToken));
    }

    private async Task<string> GetProductsVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var version = await cache.GetStringAsync(ProductsVersionKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            version = Guid.NewGuid().ToString("N");
            await cache.SetStringAsync(ProductsVersionKey, version, cancellationToken);
            return version;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache version read failed.");
            return "uncached";
        }
    }

    private async Task TryCacheOperationAsync(Func<Task> cacheOperation)
    {
        try
        {
            await cacheOperation();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed.");
        }
    }

    private static string GetProductKey(long id) => $"products:item:{id}";

    private static string GetPagedProductsKey(int page, int pageSize, string version) =>
        $"products:list:{version}:page:{page}:page-size:{pageSize}";
}
