namespace ProductsApi.Caching;

public static class ProductCacheInvalidation
{
    public static async Task InvalidateStockAsync(
        this IEnumerable<IProductCache> productCaches,
        IEnumerable<long> productIds,
        CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        foreach (var cache in productCaches)
        {
            foreach (var productId in ids)
            {
                await cache.InvalidateProductAsync(productId, cancellationToken);
            }

            await cache.InvalidateProductsAsync(cancellationToken);
        }
    }
}
