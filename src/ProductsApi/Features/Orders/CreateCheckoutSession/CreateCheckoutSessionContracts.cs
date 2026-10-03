namespace ProductsApi.Features.Orders.CreateCheckoutSession;

public sealed record CreateCheckoutSessionCommand(
    Guid UserId,
    Guid OrderId);

public sealed record CheckoutSessionDto(
    Guid OrderId,
    string SessionId,
    string ClientSecret);

public enum CheckoutFailureKind
{
    None,
    BadRequest,
    NotFound,
    Conflict,
    ProviderUnavailable,
    Configuration
}

public sealed record CreateCheckoutSessionResult(
    CheckoutSessionDto? Value,
    CheckoutFailureKind Failure,
    string? Error)
{
    public static CreateCheckoutSessionResult Success(
        CheckoutSessionDto value) =>
        new(value, CheckoutFailureKind.None, null);

    public static CreateCheckoutSessionResult BadRequest(string error) =>
        new(null, CheckoutFailureKind.BadRequest, error);

    public static CreateCheckoutSessionResult NotFound(string error) =>
        new(null, CheckoutFailureKind.NotFound, error);

    public static CreateCheckoutSessionResult Conflict(string error) =>
        new(null, CheckoutFailureKind.Conflict, error);

    public static CreateCheckoutSessionResult ProviderUnavailable(
        string error) =>
        new(null, CheckoutFailureKind.ProviderUnavailable, error);

    public static CreateCheckoutSessionResult Configuration(string error) =>
        new(null, CheckoutFailureKind.Configuration, error);
}