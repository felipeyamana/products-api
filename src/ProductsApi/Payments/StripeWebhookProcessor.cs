using Microsoft.EntityFrameworkCore;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using Stripe;
using Stripe.Checkout;

namespace ProductsApi.Payments;

public interface IStripeWebhookProcessor
{
    Task<bool> ProcessAsync(
        Event stripeEvent,
        CancellationToken cancellationToken);
}

public sealed class StripeWebhookProcessor(
    AppDbContext dbContext,
    OrderPaymentLock paymentLock,
    IStripeCheckoutGateway stripeCheckout,
    TimeProvider timeProvider,
    ILogger<StripeWebhookProcessor> logger)
    : IStripeWebhookProcessor
{
    private const string ProviderName = "Stripe";

    public async Task<bool> ProcessAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (!IsCheckoutEvent(stripeEvent.Type))
        {
            return true;
        }

        if (stripeEvent.Data.Object is not Session received)
        {
            return false;
        }

        if (!TryGetReferences(
                received,
                out var orderId,
                out var paymentAttemptId))
        {
            logger.LogInformation(
                "Ignoring unlinked Stripe event {EventId}.",
                stripeEvent.Id);
            return true;
        }

        var session = await stripeCheckout.GetAsync(
            received.Id,
            cancellationToken);

        return await ApplySessionAsync(
            stripeEvent,
            session,
            orderId,
            paymentAttemptId,
            cancellationToken);
    }

    private Task<bool> ApplySessionAsync(
        Event stripeEvent,
        Session session,
        Guid orderId,
        Guid paymentAttemptId,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            () => ApplySessionInTransactionAsync(
                stripeEvent,
                session,
                orderId,
                paymentAttemptId,
                cancellationToken));
    }

    private async Task<bool> ApplySessionInTransactionAsync(
        Event stripeEvent,
        Session session,
        Guid orderId,
        Guid paymentAttemptId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        await using var transaction = await paymentLock.AcquireAsync(
            orderId,
            cancellationToken);

        var attempt = await LoadAttemptAsync(
            orderId,
            paymentAttemptId,
            cancellationToken);

        if (attempt is null ||
            !SessionMatchesAttempt(session, attempt))
        {
            logger.LogError(
                "Stripe session {SessionId} does not match order {OrderId} and payment attempt {PaymentAttemptId}.",
                session.Id,
                orderId,
                paymentAttemptId);
            return false;
        }

        var latestAttemptNumber = await LatestAttemptNumberAsync(
            attempt.OrderId,
            cancellationToken);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LinkProviderSession(attempt, session.Id, utcNow);

        var effectiveEventType = session.PaymentStatus == "paid"
            ? "checkout.session.completed"
            : stripeEvent.Type;

        StripePaymentRules.Apply(
            attempt,
            attempt.Order,
            effectiveEventType,
            session.PaymentStatus,
            attempt.AttemptNumber == latestAttemptNumber,
            utcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task<PaymentAttempt?> LoadAttemptAsync(
        Guid orderId,
        Guid paymentAttemptId,
        CancellationToken cancellationToken) =>
        dbContext.PaymentAttempts
            .Include(attempt => attempt.Order)
            .SingleOrDefaultAsync(
                attempt =>
                    attempt.PublicId == paymentAttemptId &&
                    attempt.Order.PublicId == orderId,
                cancellationToken);

    private Task<int> LatestAttemptNumberAsync(
        long orderId,
        CancellationToken cancellationToken) =>
        dbContext.PaymentAttempts
            .Where(attempt => attempt.OrderId == orderId)
            .MaxAsync(
                attempt => attempt.AttemptNumber,
                cancellationToken);

    private static void LinkProviderSession(
        PaymentAttempt attempt,
        string providerSessionId,
        DateTime utcNow)
    {
        if (attempt.ProviderSessionId is null)
        {
            attempt.ProviderSessionId = providerSessionId;
            if (attempt.Status == PaymentAttemptStatus.Pending)
            {
                attempt.Status = PaymentAttemptStatus.CheckoutCreated;
            }

            attempt.UpdatedAtUtc = utcNow;
        }
    }

    private static bool SessionMatchesAttempt(
        Session session,
        PaymentAttempt attempt)
    {
        if (attempt.Provider != ProviderName ||
            attempt.ProviderSessionId is not null &&
            attempt.ProviderSessionId != session.Id)
        {
            return false;
        }

        if (session.Mode != "payment" ||
            session.ClientReferenceId !=
                attempt.Order.PublicId.ToString("D") ||
            !HasExpectedMetadata(session, attempt))
        {
            return false;
        }

        try
        {
            return session.AmountTotal ==
                   StripePaymentRules.AmountInMinorUnits(
                       attempt.Amount,
                       attempt.CurrencyCode) &&
                   string.Equals(
                       session.Currency,
                       attempt.CurrencyCode,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or OverflowException)
        {
            return false;
        }
    }

    private static bool HasExpectedMetadata(
        Session session,
        PaymentAttempt attempt) =>
        session.Metadata is not null &&
        session.Metadata.TryGetValue(
            "order_id",
            out var orderReference) &&
        orderReference == attempt.Order.PublicId.ToString("D") &&
        session.Metadata.TryGetValue(
            "payment_attempt_id",
            out var attemptReference) &&
        attemptReference == attempt.PublicId.ToString("D");

    private static bool TryGetReferences(
        Session session,
        out Guid orderId,
        out Guid paymentAttemptId)
    {
        orderId = Guid.Empty;
        paymentAttemptId = Guid.Empty;

        return session.Metadata is not null &&
               session.Metadata.TryGetValue(
                   "order_id",
                   out var orderReference) &&
               Guid.TryParse(orderReference, out orderId) &&
               session.Metadata.TryGetValue(
                   "payment_attempt_id",
                   out var attemptReference) &&
               Guid.TryParse(
                   attemptReference,
                   out paymentAttemptId);
    }

    private static bool IsCheckoutEvent(string eventType) =>
        eventType is
            "checkout.session.completed" or
            "checkout.session.async_payment_succeeded" or
            "checkout.session.async_payment_failed" or
            "checkout.session.expired";
}