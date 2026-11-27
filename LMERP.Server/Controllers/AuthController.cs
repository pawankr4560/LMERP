using LMERP.Server.Auth;
using LMERP.Server.Responses;
using LMERP.Application.Abstractions;
using LMERP.Server.ExceptionHandling;
using LMERP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;
using System.Text;

namespace LMERP.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<LoanUser> users,
    SignInManager<LoanUser> signIn,
    JwtTokenIssuer tokens,
    IRefreshTokenStore refreshTokens,
    IConfiguration configuration) : ControllerBase
{
    private const string RefreshCookieName = "lmerp.refresh";

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<BaseResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Unauthorized(ApiProblemDetails.Create(HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", "Email or password is incorrect."));

        var (token, expiresAt) = await tokens.IssueAsync(user);
        var refreshToken = CreateRefreshToken(user.Id);
        await refreshTokens.SaveAsync(user.Id, HashRefreshToken(refreshToken, user.SecurityStamp), cancellationToken);
        SetRefreshCookie(refreshToken);
        return Ok(BaseResponse<LoginResponse>.Succeeded(new LoginResponse(token, expiresAt, user.FullName, user.Email ?? request.Email, (await users.GetRolesAsync(user)).ToArray()), "Signed in."));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<BaseResponse<LoginResponse>>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || !TryGetUserId(refreshToken, out var userId))
            return Unauthorized(ApiProblemDetails.Create(HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", "Your session has expired. Sign in again."));

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            DeleteRefreshCookie();
            return Unauthorized(ApiProblemDetails.Create(HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", "Your session has expired. Sign in again."));
        }

        var nextRefreshToken = CreateRefreshToken(user.Id);
        var rotated = await refreshTokens.TryRotateAsync(
            user.Id,
            HashRefreshToken(refreshToken, user.SecurityStamp),
            HashRefreshToken(nextRefreshToken, user.SecurityStamp),
            cancellationToken);
        if (!rotated)
        {
            DeleteRefreshCookie();
            return Unauthorized(ApiProblemDetails.Create(HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", "Your session has expired. Sign in again."));
        }

        var (token, expiresAt) = await tokens.IssueAsync(user);
        SetRefreshCookie(nextRefreshToken);
        return Ok(BaseResponse<LoginResponse>.Succeeded(new LoginResponse(token, expiresAt, user.FullName, user.Email ?? user.UserName ?? string.Empty, (await users.GetRolesAsync(user)).ToArray()), "Session refreshed."));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<BaseResponse<bool>>> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && TryGetUserId(refreshToken, out var userId))
        {
            var user = await users.FindByIdAsync(userId.ToString());
            if (user is not null)
                await refreshTokens.RevokeAsync(user.Id, HashRefreshToken(refreshToken, user.SecurityStamp), cancellationToken);
        }
        DeleteRefreshCookie();
        return Ok(BaseResponse<bool>.Succeeded(true, "Signed out."));
    }

    private string CreateRefreshToken(int userId)
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(configuration.GetValue("Jwt:RefreshTokenDays", 14)).ToUnixTimeSeconds();
        var secret = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return $"{userId}.{expiry}.{secret}";
    }

    private static bool TryGetUserId(string token, out int userId)
    {
        userId = 0;
        var parts = token.Split('.', 3);
        return parts.Length == 3
            && int.TryParse(parts[0], out userId)
            && long.TryParse(parts[1], out var expiry)
            && expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            && parts[2].Length >= 32;
    }

    private static string HashRefreshToken(string token, string? securityStamp) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{securityStamp}:{token}")));

    private void SetRefreshCookie(string value)
    {
        Response.Cookies.Append(RefreshCookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            MaxAge = TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenDays", 14)),
            IsEssential = true
        });
    }

    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth"
    });
}
