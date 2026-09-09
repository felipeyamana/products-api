namespace ProductsApi.Features.Cart;

public sealed record SetCartItemRequest(int Quantity, Guid? Version);
public sealed record CartDto(Guid Version, IReadOnlyList<CartItemDto> Items,
    int TotalQuantity, string? Currency, decimal? Subtotal);
public sealed record CartItemDto(long ProductId, string? Name, int Quantity,
    decimal UnitPriceAtAddition, string CurrencyAtAddition, decimal? CurrentUnitPrice,
    string? CurrentCurrency, bool PriceChanged, bool IsAvailable, string? UnavailableReason,
    decimal? LineTotal, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CartResult(CartDto? Value, int StatusCode = 200, string? Error = null)
{
    public static CartResult Success(CartDto cart) => new(cart);
    public static CartResult BadRequest(string error) => new(null, 400, error);
    public static CartResult Forbidden(string error) => new(null, 403, error);
    public static CartResult Conflict(string error) => new(null, 409, error);
}
