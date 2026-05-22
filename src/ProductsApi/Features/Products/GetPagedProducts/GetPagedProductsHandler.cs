using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Products.Shared;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.GetPagedProducts;

public sealed class GetPagedProductsHandler(AppDbContext dbContext)
    : IQueryHandler<GetPagedProductsQuery, Result<PagedProductsDto>>
{
    public async Task<Result<PagedProductsDto>> Handle(GetPagedProductsQuery query, CancellationToken cancellationToken)
    {
        var pageNumber = query.PageNumber;
        var pageSize = query.PageSize;
        var pagingError = ProductPaging.NormalizePage(ref pageNumber, ref pageSize);
        if (pagingError is not null)
            return Result<PagedProductsDto>.Fail(pagingError);

        var ordered = dbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Name);

        var totalCount = await ordered.CountAsync(cancellationToken);

        var products = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .WithCatalogDetails()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var items = products.Select(ProductMapper.ToDto).ToList();
        var totalPages = ProductPaging.TotalPages(totalCount, pageSize);

        return Result<PagedProductsDto>.Ok(
            new PagedProductsDto(items, pageNumber, pageSize, totalCount, totalPages));
    }
}
