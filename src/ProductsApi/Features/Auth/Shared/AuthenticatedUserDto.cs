namespace ProductsApi.Features.Auth.Shared;

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Email,
    IReadOnlyCollection<string> Roles);
