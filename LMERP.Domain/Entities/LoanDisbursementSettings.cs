namespace LMERP.Domain.Entities;

public sealed class LoanDisbursementSettings
{
    public int Id { get; set; } = 1;
    public decimal ProcessingFeePercentage { get; set; }
    public decimal PlatformFeePercentage { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}
