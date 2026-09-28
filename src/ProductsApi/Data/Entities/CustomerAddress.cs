namespace ProductsApi.Data.Entities;

public sealed class CustomerAddress
{
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public long CustomerId { get; set; }
    public string? Label { get; set; }
    public string RecipientName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string AddressLine1 { get; set; } = "";
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = "";
    public string Region { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public bool IsDefault { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Customer Customer { get; set; } = null!;
}
