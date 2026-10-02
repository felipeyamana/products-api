using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProductsApi.Common.Cqrs;
using ProductsApi.Data;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Auth.Shared;

namespace ProductsApi.Features.Auth.RegisterUser;

public sealed class RegisterUserHandler(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    ILogger<RegisterUserHandler> logger) : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<RegisterUserResult> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (request.Password != request.ConfirmPassword)
        {
            logger.LogWarning(
                "Registration rejected with error codes {RegistrationErrorCodes}.",
                "PasswordMismatch");

            return RegisterUserResult.Failed(
                [new RegisterUserError("PasswordMismatch", "Passwords do not match.")]);
        }

        var email = request.Email.Trim();
        var strategy = db.Database.CreateExecutionStrategy();
        var outcome = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var identityResult = await users.CreateAsync(user, request.Password);
            if (!identityResult.Succeeded)
            {
                return RegistrationOutcome.Failed(identityResult.Errors);
            }

            db.Customers.Add(new Customer
            {
                UserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return RegistrationOutcome.Succeeded(user);
        });

        if (outcome.Errors is not null)
        {
            var errorCodes = outcome.Errors
                .Select(error => error.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal);

            logger.LogWarning(
                "Registration rejected with error codes {RegistrationErrorCodes}.",
                string.Join(",", errorCodes));

            return RegisterUserResult.Failed(
                outcome.Errors.Select(error => new RegisterUserError(error.Code, error.Description)));
        }

        var roles = await users.GetRolesAsync(outcome.User!);
        return RegisterUserResult.Succeeded(
            new AuthenticatedUserDto(
                outcome.User!.Id,
                outcome.User.Email!,
                roles.ToArray()));
    }

    private sealed record RegistrationOutcome(
        ApplicationUser? User,
        IReadOnlyCollection<IdentityError>? Errors)
    {
        public static RegistrationOutcome Succeeded(ApplicationUser user) => new(user, null);

        public static RegistrationOutcome Failed(IEnumerable<IdentityError> errors) =>
            new(null, errors.ToArray());
    }
}
