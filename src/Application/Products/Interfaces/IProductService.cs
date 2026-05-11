using Application.Products.Dtos;

namespace Application.Products.Interfaces;

public interface IProductService
{
    Task<PagedProductsDto> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<ProductDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto?> ReplaceAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto?> PatchAsync(long id, PatchProductRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
