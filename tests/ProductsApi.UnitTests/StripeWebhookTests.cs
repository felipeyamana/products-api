using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProductsApi.Controllers;
using ProductsApi.Payments;
using Stripe;
using Moq;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class StripeWebhookTests
{
    [Fact]
    public async Task MissingConfigurationReturnsUnavailable()
    {
        var controller = Create("", "{}", "");
        Assert.IsType<StatusCodeResult>(await controller.Receive(default));
        Assert.Equal(503, ((StatusCodeResult)await controller.Receive(default)).StatusCode);
    }

    [Fact]
    public async Task InvalidSignatureReturnsBadRequest()
    {
        var controller = Create("whsec_test", "{}", "t=1,v1=invalid");
        Assert.IsType<BadRequestResult>(await controller.Receive(default));
    }

    [Theory]
    [InlineData("checkout.session.completed", true, 200)]
    [InlineData("checkout.session.completed", false, 503)]
    [InlineData("checkout.session.async_payment_succeeded", true, 200)]
    [InlineData("checkout.session.async_payment_failed", true, 200)]
    [InlineData("checkout.session.expired", true, 200)]
    [InlineData("customer.created", true, 200)]
    public async Task SignedEventsReturnProcessorOutcome(
        string eventType, bool processed, int status)
    {
        const string secret = "whsec_test";
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = "evt_test",
            @object = "event",
            api_version = StripeConfiguration.ApiVersion,
            type = eventType,
            data = new { @object = new { id = "obj_test", @object = "customer" } }
        });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var header = $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
        var result = Assert.IsAssignableFrom<StatusCodeResult>(
            await Create(secret, payload, header, processed).Receive(default));
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task SignedEventWithDifferentApiVersionIsProcessed()
    {
        const string secret = "whsec_test";
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = "evt_version_mismatch",
            @object = "event",
            api_version = "2019-12-03",
            type = "checkout.session.completed",
            data = new { @object = new { id = "cs_test", @object = "checkout.session" } }
        });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var header = $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";

        var result = Assert.IsAssignableFrom<OkResult>(
            await Create(secret, payload, header).Receive(default));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    private static IStripeWebhookProcessor CreateProcessor(bool processed)
    {
        var processor = new Mock<IStripeWebhookProcessor>();
        processor.Setup(x => x.ProcessAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processed);
        return processor.Object;
    }

    private static StripeWebhookController Create(string secret, string body, string signature, bool processed = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.Headers["Stripe-Signature"] = signature;
        return new StripeWebhookController(
            Options.Create(new StripeOptions { WebhookSigningSecret = secret }),
            CreateProcessor(processed),
            NullLogger<StripeWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }
}
