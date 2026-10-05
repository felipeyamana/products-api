namespace ProductsApi.Data.Entities;

public sealed class ProductInventory
{
    public long ProductId { get; set; }
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public int Available => OnHand - Reserved;
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Product Product { get; set; } = null!;
}
