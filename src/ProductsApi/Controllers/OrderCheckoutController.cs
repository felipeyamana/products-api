using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.Orders.CreateCheckoutSession;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/checkout")]
public sealed class OrderCheckoutController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.OrdersWrite)]
    [ProducesResponseType(typeof(CheckoutSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            CreateCheckoutSessionCommand,
            CreateCheckoutSessionResult>(
                new CreateCheckoutSessionCommand(UserId, orderId),
                cancellationToken);

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(CreateCheckoutSessionResult result) =>
        result.Failure switch
        {
            CheckoutFailureKind.None => Ok(result.Value),
            CheckoutFailureKind.BadRequest =>
                BadRequest(new ErrorResponse(result.Error!)),
            CheckoutFailureKind.NotFound =>
                NotFound(new ErrorResponse(result.Error!)),
            CheckoutFailureKind.Conflict =>
                Conflict(new ErrorResponse(result.Error!)),
            CheckoutFailureKind.ProviderUnavailable =>
                StatusCode(
                    StatusCodes.Status502BadGateway,
                    new ErrorResponse(result.Error!)),
            CheckoutFailureKind.Configuration =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new ErrorResponse(result.Error!)),
            _ => throw new InvalidOperationException(
                "Unknown checkout result.")
        };
}