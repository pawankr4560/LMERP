using System.ComponentModel.DataAnnotations;

namespace LMERP.Application.Contracts;

public sealed record LateFeeResponse(int Id, DateOnly PenaltyDate, decimal BaseAmount, decimal PenaltyRate, decimal PenaltyAmount, string Status, DateTimeOffset CreatedAt, string? WaiverReason);
public sealed record OverdueEmiResponse(int EmiScheduleId, int LoanApplicationId, string ApplicationNumber, string CustomerName, int InstallmentNumber, DateTimeOffset DueDate, decimal EmiAmount, decimal EmiOutstanding, decimal LateFeesOutstanding, decimal TotalOutstanding, string Status, IReadOnlyList<LateFeeResponse> LateFees, IReadOnlyList<EmiPaymentResponse> Payments);
public sealed record RepaymentSummaryResponse(int OverdueInstallmentCount, decimal EmiOutstanding, decimal LateFeesOutstanding, decimal TotalOutstanding);
public sealed record EmiPaymentResponse(int PaymentId, int EmiScheduleId, decimal Amount, DateTimeOffset ReceivedAt, string Method, string? Reference, IReadOnlyList<PaymentAllocationResponse> Allocations);
public sealed record PaymentAllocationResponse(string Component, decimal Amount, int? LateFeeId);

public sealed class EmiPaymentRequest
{
    [Range(typeof(decimal), "0.01", "1000000000")] public decimal Amount { get; init; }
    [Required, StringLength(32)] public string Method { get; init; } = "Other";
    [StringLength(128)] public string? Reference { get; init; }
}

public sealed class WaiveLateFeeRequest
{
    [Required, StringLength(500, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class LoanProductLateFeeRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public bool IsLateFeeApplicable { get; init; }
    [Range(typeof(decimal), "0", "999.99")] public decimal LateFeeRate { get; init; }
    [Required, RegularExpression("^DailyPercentageOfEmi$")] public string LateFeeCalculationType { get; init; } = "DailyPercentageOfEmi";
    [Range(0, 3650)] public int GracePeriodDays { get; init; }
    [Range(typeof(decimal), "0.01", "1000000000")] public decimal? MaximumLateFeeAmount { get; init; }
    [Range(1, 3650)] public int? MaximumPenaltyDays { get; init; }
}

public sealed record LoanProductResponse(int Id, string Name, bool IsActive, bool IsLateFeeApplicable, decimal LateFeeRate, string LateFeeCalculationType, int GracePeriodDays, decimal? MaximumLateFeeAmount, int? MaximumPenaltyDays);
