using Application.Products.Dtos;
using Domain.Entities;

namespace Application.Products.Factories;

public interface IProductFactory
{
    ProductDto ToDto(Product product);

    /// <summary>Builds a new product with timestamps and optional initial USD price row.</summary>
    Product CreateFrom(CreateProductRequest request, DateTime utcNow);

    /// <summary>Full replace of scalar fields; clears existing prices and applies optional new snapshot.</summary>
    void ApplyFullReplace(Product product, UpdateProductRequest request, DateTime utcNow);

    /// <summary>Merge patch semantics for mutable fields and optional price touch.</summary>
    void ApplyPatch(Product product, PatchProductRequest request, DateTime utcNow);
}
