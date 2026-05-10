using Application.Products.Dtos;
using Application.Products.Interfaces;

namespace Application.Products.Services;

public class ProductService : IProductService
{
    public Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ProductDto>>([]);
    }

    public Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var dto = new ProductDto(
            Id: id,
            Name: "Acme Widget Pro",
            Sku: "ACM-WDG-001",
            Price: 29.99m,
            Description: "A demonstration product returned from in-memory fake data.");

        return Task.FromResult<ProductDto?>(dto);
    }
}
