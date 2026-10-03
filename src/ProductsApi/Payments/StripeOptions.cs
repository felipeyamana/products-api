namespace ProductsApi.Payments;

public sealed class StripeOptions
{
    public string ApiKey { get; set; } = "";
    public string WebhookSigningSecret { get; set; } = "";
}
