using System.ComponentModel.DataAnnotations;

namespace LMERP.Server.Contracts;

public sealed record CustomerResponse(int Id, string FullName, string Phone, string? Email, string? City, string Status, DateTimeOffset CreatedAt, int LoanCount);

public sealed class CustomerRequest
{
    [Required, StringLength(160, MinimumLength = 2)] public string FullName { get; init; } = string.Empty;
    [Required, Phone, StringLength(24)] public string Phone { get; init; } = string.Empty;
    [EmailAddress, StringLength(254)] public string? Email { get; init; }
    [StringLength(100)] public string? City { get; init; }
    [Required, RegularExpression("^(Active|Inactive|Review)$")] public string Status { get; init; } = "Active";
}

public sealed record LoanResponse(int Id, string ApplicationNumber, int CustomerId, string CustomerName, string Product, decimal RequestedAmount, int TenureMonths, string Status, DateTimeOffset AppliedAt);

public sealed class LoanRequest
{
    [Required] public int CustomerId { get; init; }
    [Required, StringLength(80)] public string Product { get; init; } = string.Empty;
    [Range(1, 100000000)] public decimal RequestedAmount { get; init; }
    [Range(1, 360)] public int TenureMonths { get; init; }
}

public sealed record DashboardSummary(int CustomerCount, int TotalLoans, int ActiveLoans, int PendingApplications, decimal TotalDisbursed, decimal OutstandingAmount, int OverdueLoans);
