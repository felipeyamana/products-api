using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProductsApi.Payments;
using Stripe;

namespace ProductsApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/stripe/webhook")]
public sealed class StripeWebhookController(
    IOptions<StripeOptions> options,
    IStripeWebhookProcessor processor,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var secret = options.Value.WebhookSigningSecret;
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                Request.Headers["Stripe-Signature"].ToString(),
                secret,
                tolerance: 300,
                throwOnApiVersionMismatch: false);
        }
        catch (Exception exception) when (
            exception is StripeException or System.Text.Json.JsonException)
        {
            // Avoid logging payment payloads, signatures, or SDK exception details.
            logger.LogWarning("Rejected invalid Stripe webhook.");
            return BadRequest();
        }

        logger.LogInformation("Verified Stripe event {EventId} of type {EventType}.",
            stripeEvent.Id, stripeEvent.Type);

        try
        {
            return await processor.ProcessAsync(stripeEvent, cancellationToken)
                ? Ok()
                : StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (StripeException)
        {
            logger.LogWarning("Stripe session retrieval failed for event {EventId}.", stripeEvent.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}