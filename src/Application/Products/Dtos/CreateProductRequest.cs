namespace Application.Products.Dtos;

public sealed class CreateProductRequest
{
    public string Name { get; set; } = null!;

    public string? Brand { get; set; }

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public int? SubCategoryId { get; set; }

    public string? SourceUrl { get; set; }

    public string? ExternalProductId { get; set; }

    public decimal? AverageRating { get; set; }

    public int? TotalRatings { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optional initial selling price in USD.</summary>
    public decimal? Price { get; set; }

    /// <summary>Optional list/MRP price in USD (maps to ProductPrices.DiscountPrice).</summary>
    public decimal? ListPrice { get; set; }

    public string? PriceStoreName { get; set; }
}
