using ProductsApi.Data.Entities;
using ProductsApi.Features.Cart;
using Xunit;

namespace ProductsApi.UnitTests;

public class CartPricingTests
{
    [Fact]
    public void MixedCurrentCurrenciesDoNotProduceMisleadingSubtotal()
    {
        var cart = new Cart { Items = [
            new CartItem { ProductId = 1, Quantity = 1, UnitPriceAtAddition = 10, CurrencyAtAddition = "USD" },
            new CartItem { ProductId = 2, Quantity = 2, UnitPriceAtAddition = 10, CurrencyAtAddition = "USD" }] };
        var dto = CartMapper.ToDto(cart, new Dictionary<long, Product> {
            [1] = new() { Prices = [new ProductPrice { ActualPrice = 10, CurrencyCode = "USD" }] },
            [2] = new() { Prices = [new ProductPrice { ActualPrice = 10, CurrencyCode = "EUR" }] }
        });
        Assert.Null(dto.Subtotal);
        Assert.Null(dto.Currency);
        Assert.True(dto.Items[1].PriceChanged);
    }

    [Fact]
    public void InvalidLatestPriceDoesNotFallBackToOldPrice()
    {
        var product = new Product { Prices = [
            new ProductPrice { Id = 1, ActualPrice = 10, CurrencyCode = "USD" },
            new ProductPrice { Id = 2, ActualPrice = -1, CurrencyCode = "USD" }] };
        Assert.Null(CartMapper.GetCurrentPrice(product));
        product.Prices.Last().ActualPrice = 0;
        Assert.Equal(0, CartMapper.GetCurrentPrice(product)!.ActualPrice);
        product.IsActive = false;
        Assert.Null(CartMapper.GetCurrentPrice(product));
    }
}
