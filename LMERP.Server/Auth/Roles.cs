namespace LMERP.Server.Auth;

public static class Roles
{
    public const string Administrator = LMERP.Domain.IdentityRoles.Administrator;
    public const string LoanOfficer = LMERP.Domain.IdentityRoles.LoanOfficer;
    public const string CollectionsManager = LMERP.Domain.IdentityRoles.CollectionsManager;
    public const string Analyst = LMERP.Domain.IdentityRoles.Analyst;
    public const string Customer = LMERP.Domain.IdentityRoles.Customer;
    public const string LoanStaff = Administrator + "," + LoanOfficer;
    public const string CollectionsStaff = Administrator + "," + LoanOfficer + "," + CollectionsManager;
    public const string DocumentReaders = Administrator + "," + LoanOfficer + "," + CollectionsManager + "," + Analyst + "," + Customer;
}

public static class CustomerClaims
{
    public const string CustomerId = LMERP.Domain.IdentityClaims.CustomerId;
}
