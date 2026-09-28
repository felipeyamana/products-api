using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.Customers.GetCustomerProfile;
using ProductsApi.Features.Customers.Shared;
using ProductsApi.Features.Customers.UpdateCustomerProfile;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/customers/me")]
public sealed class CustomersController(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CustomersRead)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Dispatch<
            GetCustomerProfileQuery,
            CustomerProfileResult>(
                new GetCustomerProfileQuery(UserId),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.CustomersWrite)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            UpdateCustomerProfileCommand,
            CustomerProfileResult>(
                new UpdateCustomerProfileCommand(UserId, request),
                cancellationToken);

        return ToActionResult(result);
    }

    private IActionResult ToActionResult(CustomerProfileResult result) =>
        result.Failure switch
        {
            CustomerProfileFailureKind.None => Ok(result.Value),
            CustomerProfileFailureKind.NotFound => NotFound(new ErrorResponse(result.Error!)),
            CustomerProfileFailureKind.Conflict => Conflict(new ErrorResponse(result.Error!)),
            _ => throw new InvalidOperationException("Unknown customer profile result.")
        };
}
