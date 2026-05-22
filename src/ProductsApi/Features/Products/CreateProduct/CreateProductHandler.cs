using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Products.Shared;
using Microsoft.EntityFrameworkCore;

namespace ProductsApi.Features.Products.CreateProduct;

public sealed class CreateProductHandler(AppDbContext dbContext)
    : ICommandHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = ProductRequestValidator.ValidateCreate(request);
        if (validationError is not null)
            return Result<ProductDto>.Fail(validationError);

        if (!string.IsNullOrWhiteSpace(request.ExternalProductId)
            && await ProductDataAccess.ExternalProductIdExistsAsync(
                dbContext, request.ExternalProductId, null, cancellationToken))
            return Result<ProductDto>.Fail($"A product with external id '{request.ExternalProductId}' already exists.");

        var utcNow = DateTime.UtcNow;
        var product = ProductMapper.CreateFrom(request, utcNow);

        await dbContext.Products.AddAsync(product, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await dbContext.Products
            .AsNoTracking()
            .WithCatalogDetails()
            .FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);

        if (created is null)
            return Result<ProductDto>.Fail("Product was not persisted.");

        return Result<ProductDto>.Ok(ProductMapper.ToDto(created));
    }
}
