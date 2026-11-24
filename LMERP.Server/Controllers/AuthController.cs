using LMERP.Server.Auth;
using LMERP.Server.Responses;
using LMERP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<LoanUser> users, SignInManager<LoanUser> signIn, JwtTokenIssuer tokens) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<BaseResponse<LoginResponse>>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Unauthorized(BaseResponse<LoginResponse>.Failed("Email or password is incorrect."));

        var (token, expiresAt) = await tokens.IssueAsync(user);
        return Ok(BaseResponse<LoginResponse>.Succeeded(new LoginResponse(token, expiresAt, user.FullName, user.Email ?? request.Email, (await users.GetRolesAsync(user)).ToArray()), "Signed in."));
    }
}
