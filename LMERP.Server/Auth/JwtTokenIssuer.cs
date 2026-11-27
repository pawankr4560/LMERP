using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using LMERP.Infrastructure.Identity;

namespace LMERP.Server.Auth;

public sealed class JwtTokenIssuer(IConfiguration configuration, UserManager<LoanUser> userManager)
{
    public async Task<(string Token, DateTime ExpiresAt)> IssueAsync(LoanUser user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? user.UserName ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sstamp", user.SecurityStamp ?? string.Empty)
        };
        var customerIdClaim = (await userManager.GetClaimsAsync(user)).FirstOrDefault(claim => claim.Type == CustomerClaims.CustomerId);
        if (customerIdClaim is not null && int.TryParse(customerIdClaim.Value, out var customerId) && customerId > 0
            && await userManager.IsInRoleAsync(user, Roles.Customer))
            claims.Add(new Claim(CustomerClaims.CustomerId, customerId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        claims.AddRange((await userManager.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
