using System.Data;
using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.CustomerAddresses.Shared;

namespace ProductsApi.Features.CustomerAddresses.SetDefaultCustomerAddress;

public sealed record SetDefaultCustomerAddressCommand(
    Guid UserId,
    Guid AddressId,
    SetDefaultCustomerAddressRequest Request);

public sealed class SetDefaultCustomerAddressHandler(AppDbContext dbContext)
    : ICommandHandler<SetDefaultCustomerAddressCommand, CustomerAddressResult<CustomerAddressDto>>
{
    public async Task<CustomerAddressResult<CustomerAddressDto>> Handle(
        SetDefaultCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(
                () => SetDefaultInTransactionAsync(command, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            return AddressChangedConflict();
        }
        catch (DbUpdateException)
        {
            return DefaultChangedConflict();
        }
    }

    private async Task<CustomerAddressResult<CustomerAddressDto>> SetDefaultInTransactionAsync(
        SetDefaultCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var address = await LoadAddressAsync(
            command.UserId,
            command.AddressId,
            cancellationToken);

        if (address is null)
        {
            return CustomerAddressResult<CustomerAddressDto>.NotFound("Address not found.");
        }

        if (!VersionsMatch(address, command.Request.Version))
        {
            return AddressChangedConflict();
        }

        if (!address.IsDefault)
        {
            await ReplaceDefaultAsync(
                address,
                command.Request.Version,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return CustomerAddressResult<CustomerAddressDto>.Success(
            CustomerAddressMapper.ToDto(address));
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

    private async Task ReplaceDefaultAsync(
        CustomerAddress address,
        byte[] version,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var currentDefault = await LoadCurrentDefaultAsync(
            address.CustomerId,
            cancellationToken);

        await DemoteAsync(
            currentDefault,
            utcNow,
            cancellationToken);
        await PromoteAsync(
            address,
            version,
            utcNow,
            cancellationToken);
    }

    private Task<CustomerAddress?> LoadCurrentDefaultAsync(
        long customerId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerAddresses.SingleOrDefaultAsync(
            address => address.CustomerId == customerId && address.IsDefault,
            cancellationToken);

    private async Task DemoteAsync(
        CustomerAddress? address,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (address is null)
        {
            return;
        }

        address.IsDefault = false;
        address.UpdatedAtUtc = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PromoteAsync(
        CustomerAddress address,
        byte[] version,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        SetOriginalVersion(address, version);
        address.IsDefault = true;
        address.UpdatedAtUtc = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void SetOriginalVersion(
        CustomerAddress address,
        byte[] version) =>
        dbContext.Entry(address)
            .Property(item => item.RowVersion)
            .OriginalValue = version;

    private static bool VersionsMatch(
        CustomerAddress address,
        byte[] version) =>
        address.RowVersion.AsSpan().SequenceEqual(version);

    private static CustomerAddressResult<CustomerAddressDto> AddressChangedConflict() =>
        CustomerAddressResult<CustomerAddressDto>.Conflict(
            "The address was changed by another request. Reload it and try again.");

    private static CustomerAddressResult<CustomerAddressDto> DefaultChangedConflict() =>
        CustomerAddressResult<CustomerAddressDto>.Conflict(
            "The default address changed while this request was processed. Retry the request.");
}
