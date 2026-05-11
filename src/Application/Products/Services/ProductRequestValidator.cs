using Application.Products.Dtos;

namespace Application.Products.Services;

internal static class ProductRequestValidator
{
    public static string? ValidateCreate(CreateProductRequest r)
    {
        var err = ValidateName(r.Name)
            ?? ValidateOptionalLength(r.Brand, ProductConstraints.MaxBrandLength, nameof(r.Brand))
            ?? ValidateOptionalLength(r.SourceUrl, ProductConstraints.MaxUrlLength, nameof(r.SourceUrl))
            ?? ValidateOptionalLength(r.ExternalProductId, ProductConstraints.MaxExternalIdLength, nameof(r.ExternalProductId))
            ?? ValidateOptionalLength(r.PriceStoreName, ProductConstraints.MaxStoreNameLength, nameof(r.PriceStoreName))
            ?? ValidateRating(r.AverageRating)
            ?? ValidatePricePair(r.Price, r.ListPrice);

        if (err is not null) return err;

        if (r.ListPrice is not null && r.Price is null)
            return "ListPrice requires Price when creating a product.";

        return null;
    }

    public static string? ValidateReplace(UpdateProductRequest r)
    {
        var err = ValidateName(r.Name)
            ?? ValidateOptionalLength(r.Brand, ProductConstraints.MaxBrandLength, nameof(r.Brand))
            ?? ValidateOptionalLength(r.SourceUrl, ProductConstraints.MaxUrlLength, nameof(r.SourceUrl))
            ?? ValidateOptionalLength(r.ExternalProductId, ProductConstraints.MaxExternalIdLength, nameof(r.ExternalProductId))
            ?? ValidateOptionalLength(r.PriceStoreName, ProductConstraints.MaxStoreNameLength, nameof(r.PriceStoreName))
            ?? ValidateRating(r.AverageRating)
            ?? ValidatePricePair(r.Price, r.ListPrice);

        if (err is not null) return err;

        if (r.ListPrice is not null && r.Price is null)
            return "ListPrice requires Price on replace unless you clear prices separately.";

        return null;
    }

    public static string? ValidatePatch(PatchProductRequest r)
    {
        if (r.Name is not null)
        {
            var nameErr = ValidateName(r.Name);
            if (nameErr is not null) return nameErr;
        }

        return (r.Brand is not null ? ValidateOptionalLength(r.Brand, ProductConstraints.MaxBrandLength, nameof(r.Brand)) : null)
            ?? (r.SourceUrl is not null ? ValidateOptionalLength(r.SourceUrl, ProductConstraints.MaxUrlLength, nameof(r.SourceUrl)) : null)
            ?? (r.ExternalProductId is not null ? ValidateOptionalLength(r.ExternalProductId, ProductConstraints.MaxExternalIdLength, nameof(r.ExternalProductId)) : null)
            ?? (r.PriceStoreName is not null ? ValidateOptionalLength(r.PriceStoreName, ProductConstraints.MaxStoreNameLength, nameof(r.PriceStoreName)) : null)
            ?? (r.AverageRating is not null ? ValidateRating(r.AverageRating) : null)
            ?? (r.Price is not null || r.ListPrice is not null ? ValidatePricePair(r.Price, r.ListPrice) : null);
    }

    private static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required.";

        if (name.Length > ProductConstraints.MaxNameLength)
            return $"Name must be at most {ProductConstraints.MaxNameLength} characters.";

        return null;
    }

    private static string? ValidateOptionalLength(string? value, int max, string field)
    {
        if (value is not null && value.Length > max)
            return $"{field} must be at most {max} characters.";

        return null;
    }

    private static string? ValidateRating(decimal? rating)
    {
        if (rating is not null && (rating < 0 || rating > 5))
            return "AverageRating must be between 0 and 5.";

        return null;
    }

    private static string? ValidatePricePair(decimal? payPrice, decimal? listPrice)
    {
        if (payPrice is not null && payPrice < 0)
            return "Price cannot be negative.";

        if (listPrice is not null && listPrice < 0)
            return "ListPrice cannot be negative.";

        if (payPrice is not null && listPrice is not null && listPrice < payPrice)
            return "ListPrice must be greater than or equal to Price.";

        return null;
    }
}
