using ProductsApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.Shared;

internal static class ProductQueryableExtensions
{
    public static IQueryable<Product> WithCatalogDetails(this IQueryable<Product> query)
        => query
            .Include(p => p.Category)
            .Include(p => p.SubCategory)
            .Include(p => p.Prices)
            .AsSplitQuery();
}
