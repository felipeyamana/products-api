using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Customers.Shared;

namespace ProductsApi.Features.Customers.UpdateCustomerProfile;

public sealed record UpdateCustomerProfileCommand(
    Guid UserId,
    UpdateCustomerProfileRequest Request);

public sealed class UpdateCustomerProfileHandler(AppDbContext dbContext)
    : ICommandHandler<UpdateCustomerProfileCommand, CustomerProfileResult>
{
    public async Task<CustomerProfileResult> Handle(
        UpdateCustomerProfileCommand command,
        CancellationToken cancellationToken)
    {
        var customer = await LoadCustomerAsync(
            command.UserId,
            cancellationToken);
        var email = await LoadEmailAsync(
            command.UserId,
            cancellationToken);

        return customer is null || email is null
            ? CustomerProfileResult.NotFound("Customer record not found.")
            : await ApplyAndSaveAsync(
                customer,
                email,
                command.Request,
                cancellationToken);
    }

    private Task<Customer?> LoadCustomerAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Customers.SingleOrDefaultAsync(
            customer => customer.UserId == userId,
            cancellationToken);

    private Task<string?> LoadEmailAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<CustomerProfileResult> ApplyAndSaveAsync(
        Customer customer,
        string email,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        SetOriginalVersion(customer, request.Version);
        ApplyProfile(customer, request);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CustomerProfileResult.Success(ToDto(customer, email));
        }
        catch (DbUpdateConcurrencyException)
        {
            return CustomerProfileResult.Conflict(
                "The customer profile was changed by another request. Reload it and try again.");
        }
    }

    private void SetOriginalVersion(Customer customer, byte[] version) =>
        dbContext.Entry(customer)
            .Property(item => item.RowVersion)
            .OriginalValue = version;

    private static void ApplyProfile(
        Customer customer,
        UpdateCustomerProfileRequest request)
    {
        customer.FirstName = Optional(request.FirstName);
        customer.LastName = Optional(request.LastName);
        customer.PhoneNumber = Optional(request.PhoneNumber);
        customer.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static CustomerProfileDto ToDto(Customer customer, string email) =>
        new(
            email,
            customer.FirstName,
            customer.LastName,
            customer.PhoneNumber,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
            [.. customer.RowVersion]);

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
