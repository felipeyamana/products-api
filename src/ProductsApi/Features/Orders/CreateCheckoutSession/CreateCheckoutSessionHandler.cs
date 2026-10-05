using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProductsApi.Caching;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Inventory;
using ProductsApi.Payments;
using Stripe;
using Stripe.Checkout;

namespace ProductsApi.Features.Orders.CreateCheckoutSession;

public sealed class CreateCheckoutSessionHandler(
    AppDbContext dbContext,
    OrderPaymentLock paymentLock,
    InventoryReservationService inventoryReservations,
    IEnumerable<IProductCache> productCaches,
    IStripeCheckoutGateway stripeCheckout,
    IOptions<StripeOptions> options,
    TimeProvider timeProvider,
    ILogger<CreateCheckoutSessionHandler> logger)
    : ICommandHandler<CreateCheckoutSessionCommand, CreateCheckoutSessionResult>
{
    private const string ProviderName = "Stripe";

    // Stripe guarantees idempotency-key retention for at least 24 hours.
    private static readonly TimeSpan SafeProviderRetryWindow =
        TimeSpan.FromHours(23);

    public async Task<CreateCheckoutSessionResult> Handle(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var configurationError = ValidateConfiguration(options.Value);
        if (configurationError is not null)
        {
            return configurationError;
        }

        var preparation = await PrepareAttemptAsync(
            command,
            cancellationToken);

        if (preparation.Error is not null)
        {
            return preparation.Error;
        }

        await productCaches.InvalidateStockAsync(
            preparation.ChangedProductIds,
            cancellationToken);

        try
        {
            return await OpenStripeSessionAsync(
                preparation.Attempt!,
                cancellationToken);
        }
        catch (StripeException exception)
        {
            logger.LogWarning(
                exception,
                "Stripe checkout failed for order {OrderId} and payment attempt {PaymentAttemptId}.",
                command.OrderId,
                preparation.Attempt!.AttemptId);

            return CreateCheckoutSessionResult.ProviderUnavailable(
                "Stripe checkout is temporarily unavailable. Retry this order.");
        }
    }

    private Task<AttemptPreparation> PrepareAttemptAsync(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            () => PrepareAttemptInTransactionAsync(
                command,
                cancellationToken));
    }

    private async Task<AttemptPreparation> PrepareAttemptInTransactionAsync(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        await using var transaction = await paymentLock.AcquireAsync(
            command.OrderId,
            cancellationToken);

        var order = await LoadOrderAsync(command, cancellationToken);
        var orderError = ValidateOrder(order);
        if (orderError is not null)
        {
            return AttemptPreparation.Failed(orderError);
        }

        var attempt = await LoadActiveAttemptAsync(
            order!.Id,
            cancellationToken);

        var reservation = await inventoryReservations.EnsureReservedAsync(
            order,
            cancellationToken);
        if (!reservation.IsSuccess)
        {
            return AttemptPreparation.Failed(
                CreateCheckoutSessionResult.Conflict(reservation.Error!));
        }

        if (attempt is null)
        {
            if (!TryGetAmount(order, out _))
            {
                return AttemptPreparation.Failed(
                    CreateCheckoutSessionResult.BadRequest(
                        "The order amount or currency is not supported."));
            }

            attempt = await CreateAttemptAsync(
                order,
                cancellationToken);
        }
        else if (reservation.ChangedProductIds.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return AttemptPreparation.Prepared(
            ToPreparedAttempt(order, attempt!),
            reservation.ChangedProductIds);
    }

    private async Task<CreateCheckoutSessionResult> OpenStripeSessionAsync(
        PreparedAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (attempt.ProviderSessionId is null &&
            IsOutsideSafeRetryWindow(attempt))
        {
            return CreateCheckoutSessionResult.Conflict(
                "This payment attempt requires reconciliation before it can be retried.");
        }

        var session = attempt.ProviderSessionId is null
            ? await CreateStripeSessionAsync(attempt, cancellationToken)
            : await stripeCheckout.GetAsync(
                attempt.ProviderSessionId,
                cancellationToken);

        if (session.Status != "open")
        {
            return CreateCheckoutSessionResult.Conflict(
                "Checkout is complete or expired. Refresh the order status.");
        }

        if (attempt.ProviderSessionId is null)
        {
            await RecordProviderSessionAsync(
                attempt.OrderId,
                attempt.AttemptId,
                session.Id,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(session.ClientSecret))
        {
            return CreateCheckoutSessionResult.ProviderUnavailable(
                "Stripe did not return an embedded Checkout client secret. Retry this order.");
        }

        return CreateCheckoutSessionResult.Success(
            new CheckoutSessionDto(
                attempt.OrderId,
                session.Id,
                session.ClientSecret));
    }

    private Task<Session> CreateStripeSessionAsync(
        PreparedAttempt attempt,
        CancellationToken cancellationToken) =>
        stripeCheckout.CreateAsync(
            BuildSessionOptions(attempt),
            attempt.IdempotencyKey,
            cancellationToken);

    private Task<Order?> LoadOrderAsync(
        CreateCheckoutSessionCommand command,
        CancellationToken cancellationToken) =>
        dbContext.Orders
            .Include(order => order.Items)
            .SingleOrDefaultAsync(
                order =>
                    order.PublicId == command.OrderId &&
                    order.Customer.UserId == command.UserId,
                cancellationToken);

    private Task<PaymentAttempt?> LoadActiveAttemptAsync(
        long orderId,
        CancellationToken cancellationToken) =>
        dbContext.PaymentAttempts.SingleOrDefaultAsync(
            attempt =>
                attempt.OrderId == orderId &&
                attempt.CompletedAtUtc == null,
            cancellationToken);

    private async Task<PaymentAttempt> CreateAttemptAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var attemptNumber = await NextAttemptNumberAsync(
            order.Id,
            cancellationToken);
        var attemptId = Guid.NewGuid();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var attempt = new PaymentAttempt
        {
            PublicId = attemptId,
            OrderId = order.Id,
            AttemptNumber = attemptNumber,
            Provider = ProviderName,
            IdempotencyKey = CreateIdempotencyKey(attemptId),
            Status = PaymentAttemptStatus.Pending,
            Amount = order.GrandTotal,
            CurrencyCode = order.CurrencyCode,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        order.PaymentStatus = OrderPaymentStatus.Pending;
        order.UpdatedAtUtc = utcNow;
        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        return attempt;
    }

    private async Task<int> NextAttemptNumberAsync(
        long orderId,
        CancellationToken cancellationToken)
    {
        var previous = await dbContext.PaymentAttempts
            .Where(attempt => attempt.OrderId == orderId)
            .MaxAsync(
                attempt => (int?)attempt.AttemptNumber,
                cancellationToken);

        return checked((previous ?? 0) + 1);
    }

    private Task RecordProviderSessionAsync(
        Guid orderId,
        Guid attemptId,
        string providerSessionId,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            () => RecordProviderSessionInTransactionAsync(
                orderId,
                attemptId,
                providerSessionId,
                cancellationToken));
    }

    private async Task RecordProviderSessionInTransactionAsync(
        Guid orderId,
        Guid attemptId,
        string providerSessionId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        await using var transaction = await paymentLock.AcquireAsync(
            orderId,
            cancellationToken);

        var attempt = await dbContext.PaymentAttempts.SingleOrDefaultAsync(
            item => item.PublicId == attemptId,
            cancellationToken);

        if (attempt is null)
        {
            throw new InvalidOperationException(
                "The prepared payment attempt no longer exists.");
        }

        if (attempt.ProviderSessionId is not null &&
            attempt.ProviderSessionId != providerSessionId)
        {
            throw new InvalidOperationException(
                "The payment attempt is linked to another provider session.");
        }

        attempt.ProviderSessionId = providerSessionId;
        if (attempt.Status == PaymentAttemptStatus.Pending)
        {
            attempt.Status = PaymentAttemptStatus.CheckoutCreated;
        }
        attempt.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private bool IsOutsideSafeRetryWindow(PreparedAttempt attempt) =>
        timeProvider.GetUtcNow().UtcDateTime - attempt.CreatedAtUtc >
        SafeProviderRetryWindow;

    private static CreateCheckoutSessionResult? ValidateOrder(Order? order)
    {
        if (order is null)
        {
            return CreateCheckoutSessionResult.NotFound("Order not found.");
        }

        return order.Status == OrderStatus.Pending &&
               order.PaymentStatus != OrderPaymentStatus.Paid
            ? null
            : CreateCheckoutSessionResult.Conflict(
                "This order is not awaiting payment.");
    }

    private static CreateCheckoutSessionResult? ValidateConfiguration(
        StripeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return CreateCheckoutSessionResult.Configuration(
                "Stripe:ApiKey is not configured.");
        }

        return null;
    }

    private static bool TryGetAmount(Order order, out long amount)
    {
        try
        {
            amount = StripePaymentRules.AmountInMinorUnits(
                order.GrandTotal,
                order.CurrencyCode);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or OverflowException)
        {
            amount = 0;
            return false;
        }
    }

    private SessionCreateOptions BuildSessionOptions(
        PreparedAttempt attempt) =>
        new()
        {
            Mode = "payment",
            UiMode = "embedded_page",
            RedirectOnCompletion = "never",
            AllowedPaymentMethodTypes = ["card"],
            ClientReferenceId = attempt.OrderId.ToString("D"),
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = attempt.OrderId.ToString("D"),
                ["payment_attempt_id"] = attempt.AttemptId.ToString("D")
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = attempt.CurrencyCode.ToLowerInvariant(),
                        UnitAmount = StripePaymentRules.AmountInMinorUnits(
                            attempt.Amount,
                            attempt.CurrencyCode),
                        ProductData =
                            new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Order {attempt.OrderId:D}"
                            }
                    }
                }
            ]
        };

    private static PreparedAttempt ToPreparedAttempt(
        Order order,
        PaymentAttempt attempt) =>
        new(
            order.PublicId,
            attempt.PublicId,
            attempt.IdempotencyKey,
            attempt.ProviderSessionId,
            attempt.Amount,
            attempt.CurrencyCode,
            attempt.CreatedAtUtc);

    private static string CreateIdempotencyKey(Guid attemptId) =>
        $"stripe-checkout-{attemptId:D}";

    private sealed record PreparedAttempt(
        Guid OrderId,
        Guid AttemptId,
        string IdempotencyKey,
        string? ProviderSessionId,
        decimal Amount,
        string CurrencyCode,
        DateTime CreatedAtUtc);

    private sealed record AttemptPreparation(
        PreparedAttempt? Attempt,
        CreateCheckoutSessionResult? Error,
        IReadOnlyList<long> ChangedProductIds)
    {
        public static AttemptPreparation Prepared(
            PreparedAttempt attempt,
            IReadOnlyList<long> changedProductIds) =>
            new(attempt, null, changedProductIds);

        public static AttemptPreparation Failed(
            CreateCheckoutSessionResult error) =>
            new(null, error, []);
    }
}
