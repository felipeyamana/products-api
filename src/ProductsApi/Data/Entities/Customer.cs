namespace ProductsApi.Data.Entities;

public sealed class Customer
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumberE164 { get; set; }
    public string? PhoneRegionCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<CustomerAddress> Addresses { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}
