namespace LMERP.Domain;

public static class IdentityRoles
{
    public const string Administrator = "Administrator";
    public const string LoanOfficer = "LoanOfficer";
    public const string CollectionsManager = "CollectionsManager";
    public const string Analyst = "Analyst";
    public const string Customer = "Customer";
}

public static class IdentityClaims
{
    public const string CustomerId = "customer_id";
}
