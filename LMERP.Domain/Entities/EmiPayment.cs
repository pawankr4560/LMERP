namespace LMERP.Domain.Entities;

public sealed class EmiPayment
{
    public int Id { get; set; }
    public int EmiScheduleId { get; set; }
    public EmiSchedule? EmiSchedule { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Method { get; set; } = "Other";
    public string? Reference { get; set; }
    public ICollection<EmiPaymentAllocation> Allocations { get; set; } = new List<EmiPaymentAllocation>();
}
