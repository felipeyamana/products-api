using System.ComponentModel.DataAnnotations;
using ProductsApi.Data.Entities;

namespace ProductsApi.Features.CustomerAddresses.Shared;

public sealed record CustomerAddressDto(
    Guid Id,
    string? Label,
    string RecipientName,
    string? PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string CountryCode,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] Version);

public sealed record CreateCustomerAddressRequest(
    [MaxLength(50)] string? Label,
    [Required, MaxLength(200)] string RecipientName,
    [MaxLength(32)] string? PhoneNumber,
    [Required, MaxLength(200)] string AddressLine1,
    [MaxLength(200)] string? AddressLine2,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(100)] string Region,
    [Required, MaxLength(30)] string PostalCode,
    [Required, RegularExpression("^[A-Za-z]{2}$")] string CountryCode,
    bool IsDefault = false);

public sealed record UpdateCustomerAddressRequest(
    [MaxLength(50)] string? Label,
    [Required, MaxLength(200)] string RecipientName,
    [MaxLength(32)] string? PhoneNumber,
    [Required, MaxLength(200)] string AddressLine1,
    [MaxLength(200)] string? AddressLine2,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(100)] string Region,
    [Required, MaxLength(30)] string PostalCode,
    [Required, RegularExpression("^[A-Za-z]{2}$")] string CountryCode,
    [Required, MinLength(8), MaxLength(8)] byte[] Version);

public sealed record SetDefaultCustomerAddressRequest(
    [Required, MinLength(8), MaxLength(8)] byte[] Version);

public enum CustomerAddressFailureKind
{
    None,
    NotFound,
    Conflict
}

public sealed record CustomerAddressResult<T>(
    T? Value,
    CustomerAddressFailureKind Failure,
    string? Error)
{
    public bool IsSuccess => Failure == CustomerAddressFailureKind.None;

    public static CustomerAddressResult<T> Success(T value) =>
        new(value, CustomerAddressFailureKind.None, null);

    public static CustomerAddressResult<T> NotFound(string error) =>
        new(default, CustomerAddressFailureKind.NotFound, error);

    public static CustomerAddressResult<T> Conflict(string error) =>
        new(default, CustomerAddressFailureKind.Conflict, error);
}

internal static class CustomerAddressMapper
{
    public static CustomerAddressDto ToDto(CustomerAddress address) =>
        new(
            address.PublicId,
            address.Label,
            address.RecipientName,
            address.PhoneNumber,
            address.AddressLine1,
            address.AddressLine2,
            address.City,
            address.Region,
            address.PostalCode,
            address.CountryCode,
            address.IsDefault,
            address.CreatedAtUtc,
            address.UpdatedAtUtc,
            [.. address.RowVersion]);

    public static CustomerAddress Create(
        long customerId,
        CreateCustomerAddressRequest request,
        bool isDefault,
        DateTime utcNow) =>
        new()
        {
            CustomerId = customerId,
            Label = Optional(request.Label),
            RecipientName = request.RecipientName.Trim(),
            PhoneNumber = Optional(request.PhoneNumber),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = Optional(request.AddressLine2),
            City = request.City.Trim(),
            Region = request.Region.Trim(),
            PostalCode = request.PostalCode.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            IsDefault = isDefault,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    public static void Apply(
        CustomerAddress address,
        UpdateCustomerAddressRequest request,
        DateTime utcNow)
    {
        address.Label = Optional(request.Label);
        address.RecipientName = request.RecipientName.Trim();
        address.PhoneNumber = Optional(request.PhoneNumber);
        address.AddressLine1 = request.AddressLine1.Trim();
        address.AddressLine2 = Optional(request.AddressLine2);
        address.City = request.City.Trim();
        address.Region = request.Region.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        address.UpdatedAtUtc = utcNow;
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
