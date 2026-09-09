namespace ProductsApi.Data.Entities;

public sealed class Cart
{
    public string UserId { get; set; } = "";
    public Guid Version { get; set; }
    public List<CartItem> Items { get; set; } = [];
}

public sealed class CartItem
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPriceAtAddition { get; set; }
    public string CurrencyAtAddition { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
