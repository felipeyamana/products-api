using System.Data;
using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.CustomerAddresses.Shared;

namespace ProductsApi.Features.CustomerAddresses.CreateCustomerAddress;

public sealed record CreateCustomerAddressCommand(
    Guid UserId,
    CreateCustomerAddressRequest Request);

public sealed class CreateCustomerAddressHandler(AppDbContext dbContext)
    : ICommandHandler<CreateCustomerAddressCommand, CustomerAddressResult<CustomerAddressDto>>
{
    public async Task<CustomerAddressResult<CustomerAddressDto>> Handle(
        CreateCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(
                () => CreateAsync(command, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return DefaultChangedConflict();
        }
    }

    private async Task<CustomerAddressResult<CustomerAddressDto>> CreateAsync(
        CreateCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        var customerId = await LoadCustomerIdAsync(
            command.UserId,
            cancellationToken);

        if (customerId is null)
        {
            return CustomerAddressResult<CustomerAddressDto>
                .NotFound("Customer record not found.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var currentDefault = await LoadDefaultAddressAsync(
            customerId.Value,
            cancellationToken);
        var makeDefault = currentDefault is null || command.Request.IsDefault;
        var utcNow = DateTime.UtcNow;

        await DemoteCurrentDefaultAsync(
            currentDefault,
            makeDefault,
            utcNow,
            cancellationToken);
        var address = await CreateAddressAsync(
            customerId.Value,
            command.Request,
            makeDefault,
            utcNow,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return CustomerAddressResult<CustomerAddressDto>.Success(
            CustomerAddressMapper.ToDto(address));
    }

    private Task<long?> LoadCustomerIdAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.UserId == userId)
            .Select(customer => (long?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<CustomerAddress?> LoadDefaultAddressAsync(
        long customerId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerAddresses.SingleOrDefaultAsync(
            address => address.CustomerId == customerId && address.IsDefault,
            cancellationToken);

    private async Task DemoteCurrentDefaultAsync(
        CustomerAddress? currentDefault,
        bool makeDefault,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!makeDefault || currentDefault is null)
        {
            return;
        }

        currentDefault.IsDefault = false;
        currentDefault.UpdatedAtUtc = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CustomerAddress> CreateAddressAsync(
        long customerId,
        CreateCustomerAddressRequest request,
        bool makeDefault,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var address = CustomerAddressMapper.Create(
            customerId,
            request,
            makeDefault,
            utcNow);

        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        return address;
    }

    private static CustomerAddressResult<CustomerAddressDto> DefaultChangedConflict() =>
        CustomerAddressResult<CustomerAddressDto>.Conflict(
            "The default address changed while this address was being created. Retry the request.");
}
