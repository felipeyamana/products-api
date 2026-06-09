using ProductsApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ProductsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController(IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private static readonly string[] AllowedRoles = ["Admin", "ProductManager"];
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult CreateToken([FromBody] TokenRequest request)
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKey) ||
            !StringComparer.Ordinal.Equals(apiKey.ToString(), _jwtOptions.ApiKey))
        {
            return Unauthorized(new { message = "Invalid API key." });
        }

        var roles = request.Roles.Count == 0
            ? ["ProductManager"]
            : request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (roles.Any(role => !AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "One or more requested roles are not allowed." });
        }

        var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes);
        var token = CreateJwt(request.Subject, roles, expires);

        return Ok(new TokenResponse(token, "Bearer", expires));
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

public sealed record TokenRequest(string Subject = "api-client", IReadOnlyCollection<string> Roles = null!)
{
    public IReadOnlyCollection<string> Roles { get; init; } = Roles ?? [];
}

public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);
