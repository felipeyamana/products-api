namespace ProductsApi.Data.Entities;

public sealed class PaymentAttempt
{
    public long Id { get; set; }
    public Guid PublicId { get; set; }
    public long OrderId { get; set; }
    public int AttemptNumber { get; set; }
    public string Provider { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string? ProviderSessionId { get; set; }
    public PaymentAttemptStatus Status { get; set; } =
        PaymentAttemptStatus.Pending;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string? FailureCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Order Order { get; set; } = null!;
}

public enum PaymentAttemptStatus
{
    Pending = 1,
    CheckoutCreated = 2,
    Paid = 3,
    Failed = 4,
    Expired = 5
}