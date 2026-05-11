using Application.Common;
using Application.Products.Dtos;

namespace Application.Products.Interfaces;

public interface IProductService
{
    Task<Result<PagedProductsDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> ReplaceAsync(long id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProductDto>> PatchAsync(long id, PatchProductRequest request, CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
