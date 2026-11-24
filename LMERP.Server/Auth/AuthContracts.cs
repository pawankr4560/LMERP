using System.ComponentModel.DataAnnotations;

namespace LMERP.Server.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 1)] public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, string FullName, string Email, IReadOnlyList<string> Roles);
