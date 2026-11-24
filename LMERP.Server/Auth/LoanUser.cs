using Microsoft.AspNetCore.Identity;

namespace LMERP.Server.Auth;

public sealed class LoanUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
