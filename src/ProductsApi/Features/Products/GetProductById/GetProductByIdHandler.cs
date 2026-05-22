using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Products.Shared;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.GetProductById;

public sealed class GetProductByIdHandler(AppDbContext dbContext)
    : IQueryHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .WithCatalogDetails()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken);

        if (product is null)
            return Result<ProductDto>.Fail("Product not found.");

        return Result<ProductDto>.Ok(ProductMapper.ToDto(product));
    }
}
