using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.CustomerAddresses.Shared;

namespace ProductsApi.Features.CustomerAddresses.GetCustomerAddresses;

public sealed record GetCustomerAddressesQuery(Guid UserId);

public sealed class GetCustomerAddressesHandler(AppDbContext dbContext)
    : IQueryHandler<GetCustomerAddressesQuery, CustomerAddressResult<IReadOnlyList<CustomerAddressDto>>>
{
    public async Task<CustomerAddressResult<IReadOnlyList<CustomerAddressDto>>> Handle(
        GetCustomerAddressesQuery query,
        CancellationToken cancellationToken)
    {
        if (!await CustomerExistsAsync(query.UserId, cancellationToken))
        {
            return CustomerAddressResult<IReadOnlyList<CustomerAddressDto>>
                .NotFound("Customer record not found.");
        }

        var addresses = await LoadAddressesAsync(
            query.UserId,
            cancellationToken);

        return CustomerAddressResult<IReadOnlyList<CustomerAddressDto>>
            .Success(addresses);
    }

    private Task<bool> CustomerExistsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                customer => customer.UserId == userId,
                cancellationToken);

    private async Task<IReadOnlyList<CustomerAddressDto>> LoadAddressesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var addresses = await dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.Customer.UserId == userId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return addresses
            .Select(CustomerAddressMapper.ToDto)
            .ToArray();
    }
}
