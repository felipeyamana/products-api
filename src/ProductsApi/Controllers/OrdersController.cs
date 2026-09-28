using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.Orders.CreateOrder;
using ProductsApi.Features.Orders.GetOrder;
using ProductsApi.Features.Orders.GetOrders;
using ProductsApi.Features.Orders.Shared;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private string Subject => User.FindFirst("sub")!.Value;
    private Guid UserId => Guid.Parse(Subject);

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.OrdersRead)]
    [ProducesResponseType(typeof(PagedOrdersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = GetOrdersHandler.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.Dispatch<
            GetOrdersQuery,
            OrderResult<PagedOrdersDto>>(
                new GetOrdersQuery(UserId, page, pageSize),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("{orderId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.OrdersRead)]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Dispatch<
            GetOrderQuery,
            OrderResult<OrderDetailDto>>(
                new GetOrderQuery(UserId, orderId),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.OrdersWrite)]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            CreateOrderCommand,
            OrderResult<OrderDetailDto>>(
                new CreateOrderCommand(UserId, Subject, request),
                cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetOrder),
                new { orderId = result.Value!.Id },
                result.Value)
            : ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(OrderResult<T> result) =>
        result.Failure switch
        {
            OrderFailureKind.None => Ok(result.Value),
            OrderFailureKind.BadRequest => BadRequest(new ErrorResponse(result.Error!)),
            OrderFailureKind.NotFound => NotFound(new ErrorResponse(result.Error!)),
            OrderFailureKind.Conflict => Conflict(new ErrorResponse(result.Error!)),
            _ => throw new InvalidOperationException("Unknown order result.")
        };
}
