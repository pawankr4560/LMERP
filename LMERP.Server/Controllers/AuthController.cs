using LMERP.Server.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<LoanUser> users, SignInManager<LoanUser> signIn, JwtTokenIssuer tokens) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Email or password is incorrect.", Status = StatusCodes.Status401Unauthorized });

        var (token, expiresAt) = await tokens.IssueAsync(user);
        return Ok(new LoginResponse(token, expiresAt, user.FullName, user.Email ?? request.Email, (await users.GetRolesAsync(user)).ToArray()));
    }
}
