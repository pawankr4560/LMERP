namespace LMERP.Domain.Entities;

public sealed class EmiLateFee
{
    public int Id { get; set; }
    public int EmiScheduleId { get; set; }
    public EmiSchedule? EmiSchedule { get; set; }
    public DateOnly PenaltyDate { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal PenaltyRate { get; set; }
    public decimal PenaltyAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? WaivedAt { get; set; }
    public string? WaiverReason { get; set; }
    public string? WaivedBy { get; set; }
    public ICollection<EmiPaymentAllocation> PaymentAllocations { get; set; } = new List<EmiPaymentAllocation>();
}
