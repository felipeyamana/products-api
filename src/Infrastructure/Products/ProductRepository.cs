using Application.Products.Repositories;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Products;

public sealed class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var ordered = _dbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Name);

        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Category)
            .Include(p => p.SubCategory)
            .Include(p => p.Prices)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Product?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.SubCategory)
            .Include(p => p.Prices)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetByIdTrackedAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .Include(p => p.Category)
            .Include(p => p.SubCategory)
            .Include(p => p.Prices)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<bool> ExternalProductIdExistsAsync(
        string externalProductId,
        long? excludeProductId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Products.Where(p => p.ExternalProductId == externalProductId);
        if (excludeProductId is not null)
            query = query.Where(p => p.Id != excludeProductId.Value);

        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _dbContext.Products.AddAsync(product, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public void Remove(Product product)
        => _dbContext.Products.Remove(product);
}
