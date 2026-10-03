namespace ProductsApi.Data.Entities;

public sealed class Order
{
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public long CustomerId { get; set; }
    public Guid? CheckoutCartVersion { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public OrderPaymentStatus PaymentStatus { get; set; } = OrderPaymentStatus.Pending;
    public DateTime? PaidAtUtc { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string ShippingAddressLine1 { get; set; } = "";
    public string? ShippingAddressLine2 { get; set; }
    public string? ShippingPhoneNumber { get; set; }
    public string? ShippingPhoneRegionCode { get; set; }
    public string ShippingCity { get; set; } = "";
    public string ShippingRegion { get; set; } = "";
    public string ShippingPostalCode { get; set; } = "";
    public string ShippingCountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Customer Customer { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = [];
    public List<PaymentAttempt> PaymentAttempts { get; set; } = [];
}

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Processing = 3,
    Shipped = 4,
    Completed = 5,
    Cancelled = 6
}

public enum OrderPaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Expired = 4
}