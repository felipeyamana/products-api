using ProductsApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.Shared;

internal static class ProductDataAccess
{
    public static Task<bool> ExternalProductIdExistsAsync(
        AppDbContext dbContext,
        string externalProductId,
        long? excludeProductId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products.Where(p => p.ExternalProductId == externalProductId);
        if (excludeProductId is not null)
            query = query.Where(p => p.Id != excludeProductId.Value);

        return query.AnyAsync(cancellationToken);
    }
}
