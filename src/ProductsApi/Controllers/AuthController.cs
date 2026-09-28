using ProductsApi.Common.Cqrs;
using ProductsApi.Data.Entities;
using ProductsApi.Features.Auth.RegisterUser;
using ProductsApi.Features.Auth.Shared;
using ProductsApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController(
    IOptions<JwtOptions> jwtOptions,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private static readonly string[] AllowedRoles = ["Admin", "ProductManager"];
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticatedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasValidApiKey())
        {
            return Unauthorized(new { message = "Invalid API key." });
        }

        var outcome = await commandDispatcher.Dispatch<RegisterUserCommand, RegisterUserResult>(
            new RegisterUserCommand(request),
            cancellationToken);

        if (!outcome.IsSuccess)
        {
            foreach (var error in outcome.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(outcome.User);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthenticatedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!HasValidApiKey())
        {
            return Unauthorized(new { message = "Invalid API key." });
        }

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var result = await signIn.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(await ToResponseAsync(user));
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult CreateToken([FromBody] TokenRequest request)
    {
        if (!HasValidApiKey())
        {
            return Unauthorized(new { message = "Invalid API key." });
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200 || request.Subject != request.Subject.Trim())
        {
            return BadRequest(new { message = "Subject must contain 1 to 200 characters without surrounding whitespace." });
        }

        var roles = request.Roles.Count == 0
            ? ["ProductManager"]
            : request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (roles.Any(role => !AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "One or more requested roles are not allowed." });
        }

        roles = roles.Select(role => AllowedRoles.Single(allowed => allowed.Equals(role, StringComparison.OrdinalIgnoreCase))).ToArray();

        var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes);
        var token = CreateJwt(request.Subject, roles, expires);

        return Ok(new TokenResponse(token, "Bearer", expires));
    }

    private bool HasValidApiKey()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var suppliedValue))
        {
            return false;
        }

        var supplied = Encoding.UTF8.GetBytes(suppliedValue.ToString());
        var expected = Encoding.UTF8.GetBytes(_jwtOptions.ApiKey);

        return supplied.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private async Task<AuthenticatedUserDto> ToResponseAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new AuthenticatedUserDto(user.Id, user.Email!, roles.ToArray());
    }

    private string CreateJwt(string subject, IEnumerable<string> roles, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(_jwtOptions.RoleClaimType, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);


public sealed record TokenRequest(string Subject = "api-client", IReadOnlyCollection<string> Roles = null!)
{
    public IReadOnlyCollection<string> Roles { get; init; } = Roles ?? [];
}

public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);
