using Domain.Entities;

namespace Application.Products.Repositories;

public interface IProductRepository
{
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Product?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Product?> GetByIdTrackedAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> ExternalProductIdExistsAsync(string externalProductId, long? excludeProductId, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    void Remove(Product product);
}
