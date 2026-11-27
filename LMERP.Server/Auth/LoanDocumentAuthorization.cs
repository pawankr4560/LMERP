using System.Security.Claims;

namespace LMERP.Server.Auth;

public static class LoanDocumentAuthorization
{
    public static bool CanRead(ClaimsPrincipal user, int customerId)
    {
        if (user.IsInRole(Roles.Administrator) || user.IsInRole(Roles.LoanOfficer)
            || user.IsInRole(Roles.CollectionsManager) || user.IsInRole(Roles.Analyst)) return true;
        var claim = user.FindFirstValue(CustomerClaims.CustomerId);
        return user.IsInRole(Roles.Customer) && int.TryParse(claim, out var linkedCustomerId) && linkedCustomerId == customerId;
    }
}
