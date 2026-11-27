namespace LMERP.Application.Services;

public sealed record ForeclosureChargeInput(string Name, bool IsTax, bool IsServiceFee, bool IsEnabled, string CalculationType, string CalculationBasis, decimal Percentage, decimal FixedAmount, int SortOrder);
public sealed record ForeclosureChargeAmount(string Name, bool IsTax, bool IsServiceFee, string CalculationType, string CalculationBasis, decimal Percentage, decimal FixedAmount, decimal BaseAmount, decimal Amount);
public sealed record ForeclosureAmountResult(decimal ServiceFeeAmount, decimal OtherCharges, decimal TotalPayable, IReadOnlyList<ForeclosureChargeAmount> Charges);

public static class ForeclosureAmountCalculator
{
    public static ForeclosureAmountResult Calculate(decimal outstandingPrincipal, decimal accruedInterest, decimal unpaidLateFees,
        decimal remainingEmiTotal, decimal serviceFeePercentage, decimal otherCharges = 0m, decimal adjustments = 0m)
    {
        var fee = decimal.Round(Math.Max(0m, remainingEmiTotal) * serviceFeePercentage / 100m, 2, MidpointRounding.AwayFromZero);
        var total = outstandingPrincipal + accruedInterest + unpaidLateFees + fee + otherCharges - adjustments;
        return new ForeclosureAmountResult(fee, otherCharges, decimal.Round(Math.Max(0m, total), 2, MidpointRounding.AwayFromZero), []);
    }

    public static ForeclosureAmountResult CalculateWithCharges(decimal outstandingPrincipal, decimal accruedInterest, decimal unpaidLateFees,
        decimal remainingEmiTotal, IEnumerable<ForeclosureChargeInput> chargeInputs, decimal adjustments = 0m)
    {
        var settlementBeforeCharges = outstandingPrincipal + accruedInterest + unpaidLateFees;
        var charges = new List<ForeclosureChargeAmount>();
        foreach (var rule in chargeInputs.Where(x => x.IsEnabled).OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var baseAmount = rule.CalculationBasis switch
            {
                "RemainingEmiTotal" => remainingEmiTotal,
                "OutstandingPrincipal" => outstandingPrincipal,
                "AccruedInterest" => accruedInterest,
                "LateFees" => unpaidLateFees,
                "SettlementBeforeCharges" => settlementBeforeCharges,
                "ServiceFee" => charges.Where(x => x.IsServiceFee).Sum(x => x.Amount),
                "PreviousCharges" => charges.Sum(x => x.Amount),
                _ => 0m
            };
            var amount = rule.CalculationType == "Fixed"
                ? rule.FixedAmount
                : decimal.Round(Math.Max(0m, baseAmount) * rule.Percentage / 100m, 2, MidpointRounding.AwayFromZero);
            charges.Add(new ForeclosureChargeAmount(rule.Name, rule.IsTax, rule.IsServiceFee, rule.CalculationType, rule.CalculationBasis,
                rule.Percentage, rule.FixedAmount, decimal.Round(Math.Max(0m, baseAmount), 2, MidpointRounding.AwayFromZero), amount));
        }
        var serviceFee = charges.Where(x => x.IsServiceFee).Sum(x => x.Amount);
        var otherCharges = charges.Where(x => !x.IsServiceFee).Sum(x => x.Amount);
        var totalPayable = decimal.Round(Math.Max(0m, settlementBeforeCharges + charges.Sum(x => x.Amount) - adjustments), 2, MidpointRounding.AwayFromZero);
        return new ForeclosureAmountResult(serviceFee, otherCharges, totalPayable, charges);
    }
}
