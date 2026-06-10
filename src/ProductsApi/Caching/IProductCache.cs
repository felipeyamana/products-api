using ProductsApi.Features.Products.Shared;

namespace ProductsApi.Caching;

public interface IProductCache
{
    Task<PagedProductsDto?> GetPagedProductsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task SetPagedProductsAsync(PagedProductsDto products, CancellationToken cancellationToken);

    Task<ProductDto?> GetProductAsync(long id, CancellationToken cancellationToken);

    Task SetProductAsync(ProductDto product, CancellationToken cancellationToken);

    Task InvalidateProductAsync(long id, CancellationToken cancellationToken);

    Task InvalidateProductsAsync(CancellationToken cancellationToken);
}
