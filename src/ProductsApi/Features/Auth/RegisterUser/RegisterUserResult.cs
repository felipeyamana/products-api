using ProductsApi.Features.Auth.Shared;

namespace ProductsApi.Features.Auth.RegisterUser;

public sealed record RegisterUserError(string Code, string Description);

public sealed record RegisterUserResult(
    AuthenticatedUserDto? User,
    IReadOnlyCollection<RegisterUserError> Errors)
{
    public bool IsSuccess => User is not null && Errors.Count == 0;

    public static RegisterUserResult Succeeded(AuthenticatedUserDto user) =>
        new(user, []);

    public static RegisterUserResult Failed(IEnumerable<RegisterUserError> errors) =>
        new(null, errors.ToArray());
}
