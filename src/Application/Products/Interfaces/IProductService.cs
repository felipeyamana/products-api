using Application.Products.Dtos;

namespace Application.Products.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
