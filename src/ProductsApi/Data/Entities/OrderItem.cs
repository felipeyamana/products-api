namespace ProductsApi.Data.Entities;

public sealed class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductExternalId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public Order Order { get; set; } = null!;
    public Product? Product { get; set; }
}
