namespace LMERP.Domain.Entities;

public sealed class LoanApplication
{
    public int Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Product { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public int TenureMonths { get; set; }
    public string Status { get; set; } = "Under review";
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
}
