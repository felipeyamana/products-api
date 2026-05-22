using ProductsApi.Features.Products.Shared;

namespace ProductsApi.Features.Products.ReplaceProduct;

public sealed record ReplaceProductCommand(long Id, UpdateProductRequest Request);
