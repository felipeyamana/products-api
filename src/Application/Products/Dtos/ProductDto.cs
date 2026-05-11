namespace Application.Products.Dtos;

public sealed record ProductDto(
    long Id,
    string Name,
    string? Brand,
    string? Description,
    int CategoryId,
    string CategoryName,
    int? SubCategoryId,
    string? SubCategoryName,
    string? SourceUrl,
    string? ExternalProductId,
    decimal? AverageRating,
    int? TotalRatings,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal? CurrentPrice,
    decimal? ListPrice,
    string? PriceCurrencyCode);
