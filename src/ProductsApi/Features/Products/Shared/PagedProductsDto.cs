namespace ProductsApi.Features.Products.Shared;

public sealed record PagedProductsDto(
    IReadOnlyList<ProductDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
