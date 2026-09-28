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
                () => CreateInTransactionAsync(command, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return DefaultChangedConflict();
        }
    }

    private async Task<CustomerAddressResult<CustomerAddressDto>> CreateInTransactionAsync(
        CreateCustomerAddressCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var customer = await LoadCustomerAsync(
            command.UserId,
            cancellationToken);

        if (customer is null)
        {
            return CustomerAddressResult<CustomerAddressDto>
                .NotFound("Customer record not found.");
        }

        var currentDefault = await LoadDefaultAddressAsync(
            customer.Id,
            cancellationToken);
        var makeDefault = currentDefault is null || command.Request.IsDefault;
        var utcNow = DateTime.UtcNow;

        await DemoteCurrentDefaultAsync(
            currentDefault,
            makeDefault,
            utcNow,
            cancellationToken);
        var address = await CreateAddressAsync(
            customer.Id,
            command.Request,
            makeDefault,
            utcNow,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return CustomerAddressResult<CustomerAddressDto>.Success(
            CustomerAddressMapper.ToDto(address));
    }

    private Task<Customer?> LoadCustomerAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers.SingleOrDefaultAsync(
            customer => customer.UserId == userId,
            cancellationToken);

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
