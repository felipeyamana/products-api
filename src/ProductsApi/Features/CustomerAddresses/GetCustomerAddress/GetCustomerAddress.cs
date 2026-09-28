using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.CustomerAddresses.Shared;

namespace ProductsApi.Features.CustomerAddresses.GetCustomerAddress;

public sealed record GetCustomerAddressQuery(Guid UserId, Guid AddressId);

public sealed class GetCustomerAddressHandler(AppDbContext dbContext)
    : IQueryHandler<GetCustomerAddressQuery, CustomerAddressResult<CustomerAddressDto>>
{
    public async Task<CustomerAddressResult<CustomerAddressDto>> Handle(
        GetCustomerAddressQuery query,
        CancellationToken cancellationToken)
    {
        var address = await LoadAddressAsync(
            query.UserId,
            query.AddressId,
            cancellationToken);

        return address is null
            ? CustomerAddressResult<CustomerAddressDto>.NotFound("Address not found.")
            : CustomerAddressResult<CustomerAddressDto>.Success(
                CustomerAddressMapper.ToDto(address));
    }

    private Task<CustomerAddress?> LoadAddressAsync(
        Guid userId,
        Guid addressId,
        CancellationToken cancellationToken) =>
        dbContext.CustomerAddresses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                address =>
                    address.PublicId == addressId &&
                    address.Customer.UserId == userId,
                cancellationToken);
}
