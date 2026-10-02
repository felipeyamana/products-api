namespace ProductsApi.Features.Products.GetPagedProducts;

public static class ProductCatalogSort
{
    public const string NameAscending = "name-asc";
    public const string NameDescending = "name-desc";
    public const string PriceAscending = "price-asc";
    public const string PriceDescending = "price-desc";
    public const string RatingDescending = "rating-desc";
    public const string Newest = "newest";

    public static readonly IReadOnlyCollection<string> SupportedValues =
    [
        NameAscending,
        NameDescending,
        PriceAscending,
        PriceDescending,
        RatingDescending,
        Newest
    ];
}
