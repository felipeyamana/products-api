using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductsApi.Common;
using ProductsApi.Common.Cqrs;
using ProductsApi.Features.CustomerAddresses.CreateCustomerAddress;
using ProductsApi.Features.CustomerAddresses.DeleteCustomerAddress;
using ProductsApi.Features.CustomerAddresses.GetCustomerAddress;
using ProductsApi.Features.CustomerAddresses.GetCustomerAddresses;
using ProductsApi.Features.CustomerAddresses.SetDefaultCustomerAddress;
using ProductsApi.Features.CustomerAddresses.Shared;
using ProductsApi.Features.CustomerAddresses.UpdateCustomerAddress;
using ProductsApi.Security;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/customers/me/addresses")]
public sealed class CustomerAddressesController(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AddressesRead)]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Dispatch<
            GetCustomerAddressesQuery,
            CustomerAddressResult<IReadOnlyList<CustomerAddressDto>>>(
                new GetCustomerAddressesQuery(UserId),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpGet("{addressId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AddressesRead)]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAddress(
        Guid addressId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.Dispatch<
            GetCustomerAddressQuery,
            CustomerAddressResult<CustomerAddressDto>>(
                new GetCustomerAddressQuery(UserId, addressId),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AddressesWrite)]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAddress(
        [FromBody] CreateCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            CreateCustomerAddressCommand,
            CustomerAddressResult<CustomerAddressDto>>(
                new CreateCustomerAddressCommand(UserId, request),
                cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetAddress),
                new { addressId = result.Value!.Id },
                result.Value)
            : ToActionResult(result);
    }

    [HttpPut("{addressId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AddressesWrite)]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAddress(
        Guid addressId,
        [FromBody] UpdateCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            UpdateCustomerAddressCommand,
            CustomerAddressResult<CustomerAddressDto>>(
                new UpdateCustomerAddressCommand(UserId, addressId, request),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("{addressId:guid}/default")]
    [Authorize(Policy = AuthorizationPolicies.AddressesWrite)]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDefaultAddress(
        Guid addressId,
        [FromBody] SetDefaultCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.Dispatch<
            SetDefaultCustomerAddressCommand,
            CustomerAddressResult<CustomerAddressDto>>(
                new SetDefaultCustomerAddressCommand(UserId, addressId, request),
                cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("{addressId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AddressesWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAddress(
        Guid addressId,
        [FromQuery] string? version,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return BadRequest(new ErrorResponse("Version is required."));
        }

        byte[] rowVersion;

        try
        {
            rowVersion = Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            return BadRequest(new ErrorResponse("Version must be a valid base64 row version."));
        }

        if (rowVersion.Length != 8)
        {
            return BadRequest(new ErrorResponse("Version must be an 8-byte row version."));
        }

        var result = await commandDispatcher.Dispatch<
            DeleteCustomerAddressCommand,
            CustomerAddressResult<bool>>(
                new DeleteCustomerAddressCommand(UserId, addressId, rowVersion),
                cancellationToken);

        return result.IsSuccess ? NoContent() : ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(CustomerAddressResult<T> result) =>
        result.Failure switch
        {
            CustomerAddressFailureKind.None => Ok(result.Value),
            CustomerAddressFailureKind.NotFound => NotFound(new ErrorResponse(result.Error!)),
            CustomerAddressFailureKind.Conflict => Conflict(new ErrorResponse(result.Error!)),
            _ => throw new InvalidOperationException("Unknown customer address result.")
        };
}
