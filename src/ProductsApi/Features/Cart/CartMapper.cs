using ProductsApi.Data.Entities;

namespace ProductsApi.Features.Cart;

public static class CartMapper
{
    private const string UnavailableReason =
        "Product is missing, inactive, or has no valid current price/currency.";

    public static CartDto EmptyCart { get; } = new(Guid.Empty, [], 0, null, 0m);

    public static ProductPrice? GetCurrentPrice(Product? product)
    {
        if (product is null || !product.IsActive)
            return null;

        var price = product.Prices.OrderByDescending(x => x.CapturedAt)
            .ThenByDescending(x => x.Id).FirstOrDefault();
        if (price is null || price.ActualPrice < 0)
            return null;

        var currency = price.CurrencyCode.Trim();
        return currency.Length == 3 && currency.All(char.IsAsciiLetter) ? price : null;
    }

    public static CartDto ToDto(
        Data.Entities.Cart cart, IReadOnlyDictionary<long, Product> products)
    {
        var items = cart.Items.OrderBy(x => x.Id)
            .Select(item => ToItemDto(item, products)).ToList();
        var currencies = items.Where(x => x.IsAvailable)
            .Select(x => x.CurrentCurrency).Distinct().ToList();
        var hasReliableSubtotal = items.All(x => x.IsAvailable) && currencies.Count <= 1;

        return new CartDto(
            cart.Version,
            items,
            items.Sum(x => x.Quantity),
            currencies.Count == 1 ? currencies[0] : null,
            hasReliableSubtotal ? items.Sum(x => x.LineTotal ?? 0) : null);
    }

    public static string NormalizeCurrency(string currencyCode) =>
        currencyCode.Trim().ToUpperInvariant();

    private static CartItemDto ToItemDto(
        CartItem item, IReadOnlyDictionary<long, Product> products)
    {
        products.TryGetValue(item.ProductId, out var product);
        var price = GetCurrentPrice(product);
        var currentCurrency = price is null ? null : NormalizeCurrency(price.CurrencyCode);
        var isAvailable = price is not null;

        return new CartItemDto(
            item.ProductId,
            product?.Name,
            item.Quantity,
            item.UnitPriceAtAddition,
            item.CurrencyAtAddition,
            price?.ActualPrice,
            currentCurrency,
            isAvailable && (price!.ActualPrice != item.UnitPriceAtAddition ||
                            currentCurrency != item.CurrencyAtAddition),
            isAvailable,
            isAvailable ? null : UnavailableReason,
            price?.ActualPrice * item.Quantity,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }
}
