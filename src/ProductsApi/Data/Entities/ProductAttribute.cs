namespace ProductsApi.Data.Entities;

public class ProductAttribute
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string AttributeName { get; set; } = null!;

    public string AttributeValue { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
}
