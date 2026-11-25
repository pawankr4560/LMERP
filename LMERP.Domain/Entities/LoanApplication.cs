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
    public decimal InterestRate { get; set; }
    public bool IsFlatInterest { get; set; } = true;
    public string Status { get; set; } = "Under review";
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public ICollection<EmiSchedule> EmiSchedules { get; set; } = new List<EmiSchedule>();
}
