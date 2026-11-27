namespace LMERP.Domain.Entities;

public sealed class LoanForeclosureSettings
{
    public int Id { get; set; } = 1;
    public bool IsForeclosureAllowed { get; set; } = true;
    public decimal ServiceFeePercentage { get; set; } = 8m;
    public string FeeCalculationType { get; set; } = "RemainingEmiTotal";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public ICollection<LoanForeclosureChargeRule> ChargeRules { get; set; } = new List<LoanForeclosureChargeRule>();
}

public sealed class LoanForeclosureChargeRule
{
    public int Id { get; set; }
    public int SettingsId { get; set; }
    public LoanForeclosureSettings? Settings { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsTax { get; set; }
    public bool IsServiceFee { get; set; }
    public string CalculationType { get; set; } = "Percentage";
    public string CalculationBasis { get; set; } = "RemainingEmiTotal";
    public decimal Percentage { get; set; }
    public decimal FixedAmount { get; set; }
    public int SortOrder { get; set; }
}

public sealed class LoanForeclosure
{
    public int Id { get; set; }
    public int LoanApplicationId { get; set; }
    public LoanApplication? LoanApplication { get; set; }
    public string ForeclosureNumber { get; set; } = string.Empty;
    public DateTimeOffset ForeclosureDate { get; set; } = DateTimeOffset.UtcNow;
    public decimal OutstandingPrincipal { get; set; }
    public decimal OutstandingInterest { get; set; }
    public decimal OutstandingLateFees { get; set; }
    public decimal RemainingEmiAmount { get; set; }
    public decimal ServiceFeePercentage { get; set; }
    public decimal ServiceFeeAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal Adjustments { get; set; }
    public ICollection<LoanForeclosureCharge> Charges { get; set; } = new List<LoanForeclosureCharge>();
    public decimal TotalForeclosureAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string ForeclosureStatus { get; set; } = "Pending";
    public string PaymentMode { get; set; } = "Other";
    public string? TransactionReference { get; set; }
    public string? Remarks { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<LoanForeclosurePayment> Payments { get; set; } = new List<LoanForeclosurePayment>();
}

public sealed class LoanForeclosureCharge
{
    public int Id { get; set; }
    public int LoanForeclosureId { get; set; }
    public LoanForeclosure? LoanForeclosure { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsTax { get; set; }
    public bool IsServiceFee { get; set; }
    public string CalculationType { get; set; } = "Percentage";
    public string CalculationBasis { get; set; } = "RemainingEmiTotal";
    public decimal Percentage { get; set; }
    public decimal FixedAmount { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Amount { get; set; }
}

public sealed class LoanForeclosurePayment
{
    public int Id { get; set; }
    public int LoanForeclosureId { get; set; }
    public LoanForeclosure? LoanForeclosure { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Method { get; set; } = "Other";
    public string? Reference { get; set; }
    public string? CollectedBy { get; set; }
    public string? IdempotencyKey { get; set; }
}
