namespace LMERP.Domain.Entities;

public sealed class EmiPaymentAllocation
{
    public int Id { get; set; }
    public int EmiPaymentId { get; set; }
    public EmiPayment? EmiPayment { get; set; }
    public int EmiScheduleId { get; set; }
    public EmiSchedule? EmiSchedule { get; set; }
    public int? EmiLateFeeId { get; set; }
    public EmiLateFee? EmiLateFee { get; set; }
    public string Component { get; set; } = "Emi";
    public decimal Amount { get; set; }
}
