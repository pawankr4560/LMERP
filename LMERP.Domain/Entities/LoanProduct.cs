namespace LMERP.Domain.Entities;

public sealed class LoanProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsLateFeeApplicable { get; set; }
    public decimal LateFeeRate { get; set; }
    public string LateFeeCalculationType { get; set; } = "DailyPercentageOfEmi";
    public int GracePeriodDays { get; set; }
    public decimal? MaximumLateFeeAmount { get; set; }
    public int? MaximumPenaltyDays { get; set; }
}
