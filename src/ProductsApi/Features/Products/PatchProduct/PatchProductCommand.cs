using ProductsApi.Features.Products.Shared;

namespace ProductsApi.Features.Products.PatchProduct;

public sealed record PatchProductCommand(long Id, PatchProductRequest Request);
