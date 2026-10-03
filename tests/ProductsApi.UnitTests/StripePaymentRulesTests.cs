using ProductsApi.Data.Entities;
using ProductsApi.Payments;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class StripePaymentRulesTests
{
    private static readonly DateTime UtcNow =
        new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("BRL", 50, 5000)]
    [InlineData("usd", 12.34, 1234)]
    public void ConvertsSupportedCurrenciesWithoutFloatingPointRounding(
        string currency,
        decimal amount,
        long expected) =>
        Assert.Equal(
            expected,
            StripePaymentRules.AmountInMinorUnits(amount, currency));

    [Theory]
    [InlineData("JPY", 50)]
    [InlineData("BRL", 0)]
    [InlineData("BRL", -1)]
    [InlineData("BRL", 1.001)]
    public void RejectsUnsupportedCurrencyOrInvalidAmount(
        string currency,
        decimal amount) =>
        Assert.Throws<InvalidOperationException>(() =>
            StripePaymentRules.AmountInMinorUnits(amount, currency));

    [Fact]
    public void UnpaidCompletionDoesNotConfirmOrder()
    {
        var attempt = Attempt();
        var order = new Order();

        StripePaymentRules.Apply(
            attempt,
            order,
            "checkout.session.completed",
            "unpaid",
            true,
            UtcNow);

        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.PaidAtUtc);
    }

    [Fact]
    public void DelayedSuccessConfirmsOrderAndAttempt()
    {
        var attempt = Attempt();
        var order = new Order();

        StripePaymentRules.Apply(
            attempt,
            order,
            "checkout.session.async_payment_succeeded",
            "paid",
            true,
            UtcNow);

        Assert.Equal(PaymentAttemptStatus.Paid, attempt.Status);
        Assert.Equal(UtcNow, attempt.CompletedAtUtc);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(UtcNow, order.PaidAtUtc);
    }

    [Fact]
    public void RepeatedOrStaleEventsCannotUndoPayment()
    {
        var attempt = Attempt();
        var order = new Order();

        StripePaymentRules.Apply(
            attempt,
            order,
            "checkout.session.completed",
            "paid",
            true,
            UtcNow);

        var later = UtcNow.AddMinutes(1);
        StripePaymentRules.Apply(
            attempt,
            order,
            "checkout.session.expired",
            "unpaid",
            true,
            later);

        Assert.Equal(PaymentAttemptStatus.Paid, attempt.Status);
        Assert.Equal(UtcNow, attempt.CompletedAtUtc);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(UtcNow, order.PaidAtUtc);
    }

    [Fact]
    public void LatePaymentDoesNotResetFulfillmentStatus()
    {
        var attempt = Attempt();
        var order = new Order { Status = OrderStatus.Shipped };

        StripePaymentRules.Apply(
            attempt,
            order,
            "checkout.session.completed",
            "paid",
            true,
            UtcNow);

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
    }

    [Theory]
    [InlineData(
        "checkout.session.expired",
        PaymentAttemptStatus.Expired,
        OrderPaymentStatus.Expired)]
    [InlineData(
        "checkout.session.async_payment_failed",
        PaymentAttemptStatus.Failed,
        OrderPaymentStatus.Failed)]
    public void CurrentTerminalAttemptUpdatesOrder(
        string eventType,
        PaymentAttemptStatus attemptStatus,
        OrderPaymentStatus orderStatus)
    {
        var attempt = Attempt();
        var order = new Order();

        StripePaymentRules.Apply(
            attempt,
            order,
            eventType,
            "unpaid",
            true,
            UtcNow);

        Assert.Equal(attemptStatus, attempt.Status);
        Assert.Equal(orderStatus, order.PaymentStatus);
        Assert.Equal(UtcNow, attempt.CompletedAtUtc);
        Assert.Equal(eventType, attempt.FailureCode);
        Assert.Null(order.PaidAtUtc);
    }

    [Fact]
    public void StaleFailureDoesNotOverwriteCurrentOrderPaymentState()
    {
        var attempt = Attempt();
        var order = new Order();

        StripePaymentRules.Apply(
            attempt,
            order,
            "checkout.session.async_payment_failed",
            "unpaid",
            false,
            UtcNow);

        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
    }

    private static PaymentAttempt Attempt() =>
        new()
        {
            Status = PaymentAttemptStatus.Pending
        };
}