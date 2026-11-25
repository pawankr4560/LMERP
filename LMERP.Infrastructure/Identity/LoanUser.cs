using Microsoft.AspNetCore.Identity;

namespace LMERP.Infrastructure.Identity;

public sealed class LoanUser : IdentityUser<int>
{
    // Rotate the Identity security stamp whenever deactivating this user or changing roles/passwords to revoke issued access tokens.
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
