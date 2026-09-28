using System.ComponentModel.DataAnnotations;

namespace ProductsApi.Features.Customers.Shared;

public sealed record CustomerProfileDto(
    string Email,
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] Version);

public sealed record UpdateCustomerProfileRequest(
    [MaxLength(100)] string? FirstName,
    [MaxLength(100)] string? LastName,
    [MaxLength(32)] string? PhoneNumber,
    [Required, MinLength(8), MaxLength(8)] byte[] Version);

public enum CustomerProfileFailureKind
{
    None,
    NotFound,
    Conflict
}

public sealed record CustomerProfileResult(
    CustomerProfileDto? Value,
    CustomerProfileFailureKind Failure,
    string? Error)
{
    public bool IsSuccess => Failure == CustomerProfileFailureKind.None;

    public static CustomerProfileResult Success(CustomerProfileDto value) =>
        new(value, CustomerProfileFailureKind.None, null);

    public static CustomerProfileResult NotFound(string error) =>
        new(null, CustomerProfileFailureKind.NotFound, error);

    public static CustomerProfileResult Conflict(string error) =>
        new(null, CustomerProfileFailureKind.Conflict, error);
}
