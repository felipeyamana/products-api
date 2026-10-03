using ProductsApi.Data.Entities;

namespace ProductsApi.Payments;

public static class StripePaymentRules
{
    // Explicitly restrict supported currencies to those with two decimal places.
    public static long AmountInMinorUnits(
        decimal amount,
        string currency)
    {
        if (currency.ToUpperInvariant() is not
            ("BRL" or "USD" or "EUR" or "GBP"))
        {
            throw new InvalidOperationException(
                "Unsupported checkout currency.");
        }

        if (amount <= 0 ||
            decimal.Truncate(amount * 100) != amount * 100)
        {
            throw new InvalidOperationException(
                "Invalid checkout amount.");
        }

        return checked((long)(amount * 100));
    }

    public static void Apply(
        PaymentAttempt attempt,
        Order order,
        string eventType,
        string paymentStatus,
        bool isLatestAttempt,
        DateTime utcNow)
    {
        if (attempt.Status == PaymentAttemptStatus.Paid)
        {
            return;
        }

        if (IsSuccessful(eventType, paymentStatus))
        {
            MarkPaid(attempt, order, utcNow);
            return;
        }

        if (eventType == "checkout.session.async_payment_failed")
        {
            MarkUnpaidTerminal(
                attempt,
                order,
                PaymentAttemptStatus.Failed,
                OrderPaymentStatus.Failed,
                eventType,
                isLatestAttempt,
                utcNow);
        }
        else if (eventType == "checkout.session.expired")
        {
            MarkUnpaidTerminal(
                attempt,
                order,
                PaymentAttemptStatus.Expired,
                OrderPaymentStatus.Expired,
                eventType,
                isLatestAttempt,
                utcNow);
        }
    }

    private static bool IsSuccessful(
        string eventType,
        string paymentStatus) =>
        paymentStatus == "paid" &&
        eventType is
            "checkout.session.completed" or
            "checkout.session.async_payment_succeeded";

    private static void MarkPaid(
        PaymentAttempt attempt,
        Order order,
        DateTime utcNow)
    {
        attempt.Status = PaymentAttemptStatus.Paid;
        attempt.FailureCode = null;
        attempt.CompletedAtUtc = utcNow;
        attempt.UpdatedAtUtc = utcNow;

        if (order.PaymentStatus != OrderPaymentStatus.Paid)
        {
            order.PaymentStatus = OrderPaymentStatus.Paid;
            order.PaidAtUtc = utcNow;
        }

        if (order.Status == OrderStatus.Pending)
        {
            order.Status = OrderStatus.Confirmed;
        }

        order.UpdatedAtUtc = utcNow;
    }

    private static void MarkUnpaidTerminal(
        PaymentAttempt attempt,
        Order order,
        PaymentAttemptStatus attemptStatus,
        OrderPaymentStatus orderStatus,
        string failureCode,
        bool isLatestAttempt,
        DateTime utcNow)
    {
        attempt.Status = attemptStatus;
        attempt.FailureCode = failureCode;
        attempt.CompletedAtUtc = utcNow;
        attempt.UpdatedAtUtc = utcNow;

        if (isLatestAttempt &&
            order.PaymentStatus != OrderPaymentStatus.Paid)
        {
            order.PaymentStatus = orderStatus;
            order.UpdatedAtUtc = utcNow;
        }
    }
}