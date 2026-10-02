namespace ProductsApi.Data.Entities;

public sealed class CustomerFavorite
{
    public long CustomerId { get; set; }
    public long ProductId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Customer Customer { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
