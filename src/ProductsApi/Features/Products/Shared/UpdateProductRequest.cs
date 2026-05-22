namespace ProductsApi.Features.Products.Shared;

public sealed class UpdateProductRequest
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

    public decimal? Price { get; set; }

    public decimal? ListPrice { get; set; }

    public string? PriceStoreName { get; set; }
}
