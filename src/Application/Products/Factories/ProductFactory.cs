using Application.Products;
using Application.Products.Dtos;
using Domain.Entities;

namespace Application.Products.Factories;

public sealed class ProductFactory : IProductFactory
{
    public ProductDto ToDto(Product product)
    {
        var latest = product.Prices.OrderByDescending(x => x.CapturedAt).FirstOrDefault();
        var currency = latest?.CurrencyCode.Trim();

        return new ProductDto(
            Id: product.Id,
            Name: product.Name,
            Brand: product.Brand,
            Description: product.Description,
            CategoryId: product.CategoryId,
            CategoryName: product.Category.Name,
            SubCategoryId: product.SubCategoryId,
            SubCategoryName: product.SubCategory?.Name,
            ExternalProductId: product.ExternalProductId,
            AverageRating: product.AverageRating,
            TotalRatings: product.TotalRatings,
            IsActive: product.IsActive,
            CreatedAt: product.CreatedAt,
            UpdatedAt: product.UpdatedAt,
            CurrentPrice: latest?.ActualPrice,
            ListPrice: latest?.DiscountPrice,
            PriceCurrencyCode: string.IsNullOrEmpty(currency) ? null : currency);
    }

    public Product CreateFrom(CreateProductRequest request, DateTime utcNow)
    {
        var product = new Product
        {
            Name = request.Name.Trim(),
            Brand = TrimOrNull(request.Brand),
            Description = request.Description,
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
            SourceUrl = TrimOrNull(request.SourceUrl),
            ExternalProductId = TrimOrNull(request.ExternalProductId),
            AverageRating = request.AverageRating,
            TotalRatings = request.TotalRatings,
            IsActive = request.IsActive,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        AppendPriceSnapshotIfProvided(product, request.Price, request.ListPrice, request.PriceStoreName, utcNow);
        return product;
    }

    public void ApplyFullReplace(Product product, UpdateProductRequest request, DateTime utcNow)
    {
        product.Name = request.Name.Trim();
        product.Brand = TrimOrNull(request.Brand);
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.SubCategoryId = request.SubCategoryId;
        product.SourceUrl = TrimOrNull(request.SourceUrl);
        product.ExternalProductId = TrimOrNull(request.ExternalProductId);
        product.AverageRating = request.AverageRating;
        product.TotalRatings = request.TotalRatings;
        product.IsActive = request.IsActive;
        product.UpdatedAt = utcNow;

        product.Prices.Clear();
        AppendPriceSnapshotIfProvided(product, request.Price, request.ListPrice, request.PriceStoreName, utcNow);
    }

    public void ApplyPatch(Product product, PatchProductRequest request, DateTime utcNow)
    {
        if (!string.IsNullOrWhiteSpace(request.ExternalProductId))
            product.ExternalProductId = request.ExternalProductId.Trim();

        if (request.Name is not null)
            product.Name = request.Name.Trim();

        if (request.Brand is not null)
            product.Brand = TrimOrNull(request.Brand);

        if (request.Description is not null)
            product.Description = request.Description;

        if (request.CategoryId is not null)
            product.CategoryId = request.CategoryId.Value;

        if (request.SubCategoryId is not null)
            product.SubCategoryId = request.SubCategoryId;

        if (request.SourceUrl is not null)
            product.SourceUrl = TrimOrNull(request.SourceUrl);

        if (request.AverageRating is not null)
            product.AverageRating = request.AverageRating;

        if (request.TotalRatings is not null)
            product.TotalRatings = request.TotalRatings;

        if (request.IsActive is not null)
            product.IsActive = request.IsActive.Value;

        product.UpdatedAt = utcNow;

        var touchesPrice =
            request.Price is not null || request.ListPrice is not null || request.PriceStoreName is not null;

        if (touchesPrice)
            MergeLatestPrice(product, request, utcNow);
    }

    private static void AppendPriceSnapshotIfProvided(
        Product product,
        decimal? price,
        decimal? listPrice,
        string? storeName,
        DateTime utcNow)
    {
        if (price is null)
            return;

        product.Prices.Add(new ProductPrice
        {
            ActualPrice = price.Value,
            DiscountPrice = listPrice,
            CurrencyCode = "USD",
            StoreName = NormalizeStoreName(storeName),
            CapturedAt = utcNow,
        });
    }

    private static void MergeLatestPrice(Product product, PatchProductRequest request, DateTime utcNow)
    {
        var latest = product.Prices.OrderByDescending(x => x.CapturedAt).FirstOrDefault();
        if (latest is null)
        {
            if (request.Price is not null)
                AppendPriceSnapshotIfProvided(product, request.Price, request.ListPrice, request.PriceStoreName, utcNow);

            return;
        }

        if (request.Price is not null)
            latest.ActualPrice = request.Price.Value;

        if (request.ListPrice is not null)
            latest.DiscountPrice = request.ListPrice;

        if (request.PriceStoreName is not null)
            latest.StoreName = NormalizeStoreName(request.PriceStoreName);

        latest.CapturedAt = utcNow;
        latest.CurrencyCode = "USD";
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeStoreName(string? storeName)
    {
        if (string.IsNullOrWhiteSpace(storeName))
            return null;

        var t = storeName.Trim();
        return t.Length <= ProductConstraints.MaxStoreNameLength
            ? t
            : t[..ProductConstraints.MaxStoreNameLength];
    }
}
