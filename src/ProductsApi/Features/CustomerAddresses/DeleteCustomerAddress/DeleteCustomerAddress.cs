using System.Data;
using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.CustomerAddresses.Shared;

namespace ProductsApi.Features.CustomerAddresses.DeleteCustomerAddress;

public sealed record DeleteCustomerAddressCommand(
    Guid UserId,
    Guid AddressId,
    byte[] Version);

public sealed class DeleteCustomerAddressHandler(AppDbContext dbContext)
    : ICommandHandler<DeleteCustomerAddressCommand, CustomerAddressResult<bool>>
{
    public async Task<CustomerAddressResult<bool>> Handle(
        DeleteCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(
                () => DeleteAsync(command, cancellationToken));
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

    private async Task<CustomerAddressResult<bool>> DeleteAsync(
        DeleteCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var customerId = await LoadCustomerIdAsync(
            command.UserId,
            cancellationToken);

        if (customerId is null)
        {
            return CustomerAddressResult<bool>.NotFound("Address not found.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var address = await LoadAddressAsync(
            customerId.Value,
            command.AddressId,
            cancellationToken);

        if (address is null)
        {
            return CustomerAddressResult<bool>.NotFound("Address not found.");
        }

        SetOriginalVersion(address, command.Version);
        var replacement = await LoadReplacementAsync(
            address,
            cancellationToken);

        await DeleteAsync(address, cancellationToken);
        await PromoteAsync(replacement, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CustomerAddressResult<bool>.Success(true);
    }

    private Task<long?> LoadCustomerIdAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.UserId == userId)
            .Select(customer => (long?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<CustomerAddress?> LoadAddressAsync(
        long customerId,
        Guid addressId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerAddresses.SingleOrDefaultAsync(
            address =>
                address.PublicId == addressId &&
                address.CustomerId == customerId,
            cancellationToken);

    private Task<CustomerAddress?> LoadReplacementAsync(
        CustomerAddress address,
        CancellationToken cancellationToken) =>
        address.IsDefault
            ? dbContext.CustomerAddresses
                .Where(candidate =>
                    candidate.CustomerId == address.CustomerId &&
                    candidate.Id != address.Id)
                .OrderBy(candidate => candidate.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
            : Task.FromResult<CustomerAddress?>(null);

    private void SetOriginalVersion(
        CustomerAddress address,
        byte[] version) =>
        dbContext.Entry(address)
            .Property(item => item.RowVersion)
            .OriginalValue = version;

    private async Task DeleteAsync(
        CustomerAddress address,
        CancellationToken cancellationToken)
    {
        dbContext.CustomerAddresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PromoteAsync(
        CustomerAddress? replacement,
        CancellationToken cancellationToken)
    {
        if (replacement is null)
        {
            return;
        }

        replacement.IsDefault = true;
        replacement.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CustomerAddressResult<bool> AddressChangedConflict() =>
        CustomerAddressResult<bool>.Conflict(
            "The address was changed by another request. Reload it and try again.");

    private static CustomerAddressResult<bool> DefaultChangedConflict() =>
        CustomerAddressResult<bool>.Conflict(
            "The default address changed while this request was processed. Retry the request.");
}
