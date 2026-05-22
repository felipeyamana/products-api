using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Products.Shared;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.DeleteProduct;

public sealed class DeleteProductHandler(AppDbContext dbContext)
    : ICommandHandler<DeleteProductCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .WithCatalogDetails()
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (product is null)
            return Result<bool>.Fail("Product not found.");

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<bool>.Ok(true);
    }
}
