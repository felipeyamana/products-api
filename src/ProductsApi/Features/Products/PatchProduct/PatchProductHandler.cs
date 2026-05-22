using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Products.Shared;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.PatchProduct;

public sealed class PatchProductHandler(AppDbContext dbContext)
    : ICommandHandler<PatchProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(PatchProductCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = ProductRequestValidator.ValidatePatch(request);
        if (validationError is not null)
            return Result<ProductDto>.Fail(validationError);

        var product = await dbContext.Products
            .WithCatalogDetails()
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (product is null)
            return Result<ProductDto>.Fail("Product not found.");

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await ProductDataAccess.ExternalProductIdExistsAsync(
                dbContext, request.ExternalProductId, command.Id, cancellationToken))
            return Result<ProductDto>.Fail($"A product with external id '{request.ExternalProductId}' already exists.");

        ProductMapper.ApplyPatch(product, request, DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await dbContext.Products
            .AsNoTracking()
            .WithCatalogDetails()
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (updated is null)
            return Result<ProductDto>.Fail("Product not found.");

        return Result<ProductDto>.Ok(ProductMapper.ToDto(updated));
    }
}
