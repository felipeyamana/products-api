using Application.Products;
using Application.Products.Dtos;

namespace Application.Products.Services;

internal static class ProductRequestValidator
{
    public static void ValidateCreate(CreateProductRequest r)
    {
        ValidateName(r.Name);
        ValidateOptionalLengths(r.Brand, ProductConstraints.MaxBrandLength, nameof(r.Brand));
        ValidateOptionalLengths(r.SourceUrl, ProductConstraints.MaxUrlLength, nameof(r.SourceUrl));
        ValidateOptionalLengths(r.ExternalProductId, ProductConstraints.MaxExternalIdLength, nameof(r.ExternalProductId));
        ValidateOptionalLengths(r.PriceStoreName, ProductConstraints.MaxStoreNameLength, nameof(r.PriceStoreName));
        ValidateRating(r.AverageRating);

        if (r.ListPrice is not null && r.Price is null)
            throw new ArgumentException("ListPrice requires Price when creating a product.");

        ValidatePricePair(r.Price, r.ListPrice);
    }

    public static void ValidateReplace(UpdateProductRequest r)
    {
        ValidateName(r.Name);
        ValidateOptionalLengths(r.Brand, ProductConstraints.MaxBrandLength, nameof(r.Brand));
        ValidateOptionalLengths(r.SourceUrl, ProductConstraints.MaxUrlLength, nameof(r.SourceUrl));
        ValidateOptionalLengths(r.ExternalProductId, ProductConstraints.MaxExternalIdLength, nameof(r.ExternalProductId));
        ValidateOptionalLengths(r.PriceStoreName, ProductConstraints.MaxStoreNameLength, nameof(r.PriceStoreName));
        ValidateRating(r.AverageRating);

        if (r.ListPrice is not null && r.Price is null)
            throw new ArgumentException("ListPrice requires Price on replace unless you clear prices separately.");

        ValidatePricePair(r.Price, r.ListPrice);
    }

    public static void ValidatePatch(PatchProductRequest r)
    {
        if (r.Name is not null)
            ValidateName(r.Name);

        if (r.Brand is not null)
            ValidateOptionalLengths(r.Brand, ProductConstraints.MaxBrandLength, nameof(r.Brand));

        if (r.SourceUrl is not null)
            ValidateOptionalLengths(r.SourceUrl, ProductConstraints.MaxUrlLength, nameof(r.SourceUrl));

        if (r.ExternalProductId is not null)
            ValidateOptionalLengths(r.ExternalProductId, ProductConstraints.MaxExternalIdLength, nameof(r.ExternalProductId));

        if (r.PriceStoreName is not null)
            ValidateOptionalLengths(r.PriceStoreName, ProductConstraints.MaxStoreNameLength, nameof(r.PriceStoreName));

        if (r.AverageRating is not null)
            ValidateRating(r.AverageRating);

        if (r.Price is not null || r.ListPrice is not null)
            ValidatePricePair(r.Price, r.ListPrice);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        if (name.Length > ProductConstraints.MaxNameLength)
            throw new ArgumentException($"Name must be at most {ProductConstraints.MaxNameLength} characters.");
    }

    private static void ValidateOptionalLengths(string? value, int max, string field)
    {
        if (value is null)
            return;

        if (value.Length > max)
            throw new ArgumentException($"{field} must be at most {max} characters.");
    }

    private static void ValidateRating(decimal? rating)
    {
        if (rating is null)
            return;

        if (rating < 0 || rating > 5)
            throw new ArgumentException("AverageRating must be between 0 and 5.");
    }

    private static void ValidatePricePair(decimal? payPrice, decimal? listPrice)
    {
        if (payPrice is not null && payPrice < 0)
            throw new ArgumentException("Price cannot be negative.");

        if (listPrice is not null && listPrice < 0)
            throw new ArgumentException("ListPrice cannot be negative.");

        if (payPrice is not null && listPrice is not null && listPrice < payPrice)
            throw new ArgumentException("ListPrice must be greater than or equal to Price.");
    }
}
