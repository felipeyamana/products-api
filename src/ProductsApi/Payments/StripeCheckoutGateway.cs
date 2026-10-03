using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace ProductsApi.Payments;

public interface IStripeCheckoutGateway
{
    Task<Session> GetAsync(string sessionId, CancellationToken ct);
    Task<Session> CreateAsync(SessionCreateOptions request, string idempotencyKey, CancellationToken ct);
}

public sealed class StripeCheckoutGateway(IOptions<StripeOptions> options) : IStripeCheckoutGateway
{
    private SessionService Sessions => new(new StripeClient(options.Value.ApiKey));

    public Task<Session> GetAsync(string sessionId, CancellationToken ct) =>
        Sessions.GetAsync(sessionId, cancellationToken: ct);

    public Task<Session> CreateAsync(SessionCreateOptions request, string idempotencyKey, CancellationToken ct) =>
        Sessions.CreateAsync(request, new RequestOptions { IdempotencyKey = idempotencyKey }, ct);
}
