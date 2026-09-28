using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Features.Customers.Shared;

namespace ProductsApi.Features.Customers.GetCustomerProfile;

public sealed record GetCustomerProfileQuery(Guid UserId);

public sealed class GetCustomerProfileHandler(AppDbContext dbContext)
    : IQueryHandler<GetCustomerProfileQuery, CustomerProfileResult>
{
    public Task<CustomerProfileResult> Handle(
        GetCustomerProfileQuery query,
        CancellationToken cancellationToken) =>
        LoadProfileAsync(query.UserId, cancellationToken);

    private async Task<CustomerProfileResult> LoadProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await BuildProfileQuery(userId)
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null
            ? CustomerProfileResult.NotFound("Customer record not found.")
            : CustomerProfileResult.Success(profile);
    }

    private IQueryable<CustomerProfileDto> BuildProfileQuery(Guid userId) =>
        dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.UserId == userId)
            .Join(
                dbContext.Users.AsNoTracking(),
                customer => customer.UserId,
                user => (Guid?)user.Id,
                (customer, user) => new CustomerProfileDto(
                    user.Email!,
                    customer.FirstName,
                    customer.LastName,
                    customer.PhoneNumber,
                    customer.CreatedAtUtc,
                    customer.UpdatedAtUtc,
                    customer.RowVersion));
}
