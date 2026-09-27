namespace ProductsApi.Data.Entities;

public sealed class Customer
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<Order> Orders { get; set; } = [];
}
