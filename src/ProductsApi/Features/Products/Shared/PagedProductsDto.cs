namespace ProductsApi.Features.Products.Shared;

public sealed record PagedProductsDto(
    IReadOnlyList<ProductDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    ProductFacetsDto Facets)
{
    public PagedProductsDto(
        IReadOnlyList<ProductDto> items,
        int pageNumber,
        int pageSize,
        int totalCount,
        int totalPages)
        : this(items, pageNumber, pageSize, totalCount, totalPages, ProductFacetsDto.Empty)
    {
    }
}

public sealed record ProductFacetsDto(
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<ProductBrandFacetDto> Brands,
    IReadOnlyList<ProductRatingFacetDto> Ratings)
{
    public static ProductFacetsDto Empty { get; } = new(null, null, [], []);
}

public sealed record ProductBrandFacetDto(string Brand, int Count);

public sealed record ProductRatingFacetDto(decimal MinRating, int Count);
