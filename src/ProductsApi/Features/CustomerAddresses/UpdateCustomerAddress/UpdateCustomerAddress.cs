using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.CustomerAddresses.Shared;

namespace ProductsApi.Features.CustomerAddresses.UpdateCustomerAddress;

public sealed record UpdateCustomerAddressCommand(
    Guid UserId,
    Guid AddressId,
    UpdateCustomerAddressRequest Request);

public sealed class UpdateCustomerAddressHandler(AppDbContext dbContext)
    : ICommandHandler<UpdateCustomerAddressCommand, CustomerAddressResult<CustomerAddressDto>>
{
    public async Task<CustomerAddressResult<CustomerAddressDto>> Handle(
        UpdateCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var address = await LoadAddressAsync(
            command.UserId,
            command.AddressId,
            cancellationToken);

        return address is null
            ? CustomerAddressResult<CustomerAddressDto>.NotFound("Address not found.")
            : await ApplyAndSaveAsync(
                address,
                command.Request,
                cancellationToken);
    }

    private Task<CustomerAddress?> LoadAddressAsync(
        Guid userId,
        Guid addressId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerAddresses.SingleOrDefaultAsync(
            address =>
                address.PublicId == addressId &&
                address.Customer.UserId == userId,
            cancellationToken);

    private async Task<CustomerAddressResult<CustomerAddressDto>> ApplyAndSaveAsync(
        CustomerAddress address,
        UpdateCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        SetOriginalVersion(address, request.Version);
        CustomerAddressMapper.Apply(address, request, DateTime.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CustomerAddressResult<CustomerAddressDto>.Success(
                CustomerAddressMapper.ToDto(address));
        }
        catch (DbUpdateConcurrencyException)
        {
            return CustomerAddressResult<CustomerAddressDto>.Conflict(
                "The address was changed by another request. Reload it and try again.");
        }
    }

    private void SetOriginalVersion(
        CustomerAddress address,
        byte[] version) =>
        dbContext.Entry(address)
            .Property(item => item.RowVersion)
            .OriginalValue = version;
}
