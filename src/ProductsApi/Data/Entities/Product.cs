namespace ProductsApi.Data.Entities;

public class Product
{
    public long Id { get; set; }

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

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;

    public Category? SubCategory { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();

    public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
}
