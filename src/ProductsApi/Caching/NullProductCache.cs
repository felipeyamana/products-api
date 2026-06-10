using ProductsApi.Features.Products.Shared;

namespace ProductsApi.Caching;

public sealed class NullProductCache : IProductCache
{
    public Task<PagedProductsDto?> GetPagedProductsAsync(int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<PagedProductsDto?>(null);

    public Task SetPagedProductsAsync(PagedProductsDto products, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<ProductDto?> GetProductAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult<ProductDto?>(null);

    public Task SetProductAsync(ProductDto product, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateProductAsync(long id, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateProductsAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
