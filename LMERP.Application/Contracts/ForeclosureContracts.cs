using System.ComponentModel.DataAnnotations;

namespace LMERP.Application.Contracts;

public sealed record ForeclosureChargeRuleResponse(int Id, string Name, bool IsEnabled, bool IsTax, bool IsServiceFee, string CalculationType, string CalculationBasis, decimal Percentage, decimal FixedAmount, int SortOrder);
public sealed class ForeclosureChargeRuleRequest
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public bool IsTax { get; init; }
    public bool IsServiceFee { get; init; }
    [Required, RegularExpression("^(Percentage|Fixed)$")] public string CalculationType { get; init; } = "Percentage";
    [Required, RegularExpression("^(RemainingEmiTotal|OutstandingPrincipal|AccruedInterest|LateFees|SettlementBeforeCharges|ServiceFee|PreviousCharges)$")] public string CalculationBasis { get; init; } = "RemainingEmiTotal";
    [Range(typeof(decimal), "0", "100")] public decimal Percentage { get; init; }
    [Range(typeof(decimal), "0", "1000000000")] public decimal FixedAmount { get; init; }
    [Range(0, 1000)] public int SortOrder { get; init; }
}
public sealed record ForeclosureSettingsResponse(bool IsForeclosureAllowed, decimal ServiceFeePercentage, string FeeCalculationType, DateTimeOffset UpdatedAt, IReadOnlyList<ForeclosureChargeRuleResponse> Charges);
public sealed class ForeclosureSettingsRequest
{
    public bool IsForeclosureAllowed { get; init; } = true;
    [Range(typeof(decimal), "0", "100")] public decimal ServiceFeePercentage { get; init; } = 8m;
    [Required, RegularExpression("^RemainingEmiTotal$")] public string FeeCalculationType { get; init; } = "RemainingEmiTotal";
    public IReadOnlyList<ForeclosureChargeRuleRequest>? Charges { get; init; }
}
public sealed record ForeclosureScheduleResponse(int InstallmentNumber, DateTimeOffset DueDate, decimal EmiAmount, decimal PrincipalAmount, decimal InterestAmount, decimal PaidAmount, decimal OutstandingAmount, string Status);
public sealed record ForeclosureExistingPaymentResponse(int PaymentId, int InstallmentNumber, decimal Amount, DateTimeOffset ReceivedAt, string Method, string? Reference, IReadOnlyList<PaymentAllocationResponse> Allocations);
public sealed record ForeclosureLateFeeResponse(int LateFeeId, int InstallmentNumber, DateOnly PenaltyDate, decimal BaseAmount, decimal PenaltyRate, decimal PenaltyAmount, decimal PaidAmount, decimal OutstandingAmount, string Status, string? WaiverReason);
public sealed record ForeclosurePaymentResponse(int Id, decimal Amount, DateTimeOffset ReceivedAt, string Method, string? Reference, string? CollectedBy);
public sealed record ForeclosureChargeResponse(string Name, bool IsTax, bool IsServiceFee, string CalculationType, string CalculationBasis, decimal Percentage, decimal FixedAmount, decimal BaseAmount, decimal Amount);
public sealed record ForeclosureHistoryResponse(int Id, string ForeclosureNumber, DateTimeOffset ForeclosureDate, decimal RemainingEmiAmount, decimal OutstandingLateFees, decimal ServiceFeePercentage, decimal ServiceFeeAmount, decimal TotalForeclosureAmount, decimal AmountPaid, decimal Balance, string PaymentStatus, string ForeclosureStatus, string PaymentMode, string? TransactionReference, string? Remarks, IReadOnlyList<ForeclosurePaymentResponse> Payments, IReadOnlyList<ForeclosureChargeResponse> Charges);
public sealed record ForeclosureLoanResponse(int LoanApplicationId, string LoanNumber, string CustomerName, string CustomerPhone, string Product, decimal LoanAmount, decimal? ApprovedLoanAmount, decimal? DisbursedAmount, decimal InterestRate, int LoanTenureMonths, DateTimeOffset LoanStartDate, DateTimeOffset? DisbursementDate, decimal TotalEmiAmount, int TotalInstallments, int PaidInstallments, int PendingInstallments, decimal TotalPaidEmiAmount, decimal RemainingEmiAmount, decimal OutstandingPrincipal, decimal OutstandingInterest, decimal ExistingOutstandingLateFees, decimal ServiceFeePercentage, decimal ServiceFeeAmount, decimal OtherCharges, decimal Adjustments, decimal TotalForeclosurePayable, DateTimeOffset CalculationDate, string InterestBreakdownNote, IReadOnlyList<ForeclosureScheduleResponse> EmiSchedule, IReadOnlyList<ForeclosureExistingPaymentResponse> ExistingPayments, IReadOnlyList<ForeclosureLateFeeResponse> ExistingLateFees, IReadOnlyList<ForeclosureChargeResponse> Charges);
public sealed class ForeclosurePaymentRequest
{
    [Range(typeof(decimal), "0.01", "1000000000")] public decimal Amount { get; init; }
    [Required, StringLength(32)] public string PaymentMode { get; init; } = "Other";
    [StringLength(128)] public string? TransactionReference { get; init; }
    [StringLength(500)] public string? Remarks { get; init; }
}
