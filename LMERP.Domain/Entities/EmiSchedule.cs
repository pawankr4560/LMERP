namespace LMERP.Domain.Entities;

public sealed class EmiSchedule
{
    public int Id { get; set; }
    public int LoanApplicationId { get; set; }
    public LoanApplication? LoanApplication { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal EmiAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = "Pending";
    public bool IsLateFeeExempt { get; set; }
    public DateTimeOffset? LateFeeExemptedAt { get; set; }
    public string? LateFeeExemptedBy { get; set; }
    public string? LateFeeExemptionReason { get; set; }
    public bool IsLateFeeApplicable { get; set; }
    public decimal LateFeeRate { get; set; }
    public string LateFeeCalculationType { get; set; } = "DailyPercentageOfEmi";
    public int GracePeriodDays { get; set; }
    public decimal? MaximumLateFeeAmount { get; set; }
    public int? MaximumPenaltyDays { get; set; }
    public ICollection<EmiLateFee> LateFees { get; set; } = new List<EmiLateFee>();
    public ICollection<EmiPayment> Payments { get; set; } = new List<EmiPayment>();
}
