using System.ComponentModel.DataAnnotations;

namespace ProductsApi.Features.Auth.RegisterUser;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string ConfirmPassword);
