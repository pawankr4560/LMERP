namespace LMERP.Server.Auth;

public static class Roles
{
    public const string Administrator = LMERP.Domain.IdentityRoles.Administrator;
    public const string LoanOfficer = LMERP.Domain.IdentityRoles.LoanOfficer;
    public const string CollectionsManager = LMERP.Domain.IdentityRoles.CollectionsManager;
    public const string Analyst = LMERP.Domain.IdentityRoles.Analyst;
    public const string LoanStaff = Administrator + "," + LoanOfficer;
    public const string CollectionsStaff = Administrator + "," + LoanOfficer + "," + CollectionsManager;
}
