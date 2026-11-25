namespace LMERP.Application.Services;

public sealed record DailyLateFee(DateOnly PenaltyDate, decimal BaseAmount, decimal Rate, decimal Amount);

/// <summary>Pure daily fee rules, suitable for replaying missed processing dates safely.</summary>
public static class LateFeeCalculator
{
    public static IReadOnlyList<DailyLateFee> Calculate(
        decimal originalEmiAmount,
        decimal rate,
        DateOnly dueDate,
        int gracePeriodDays,
        DateOnly businessToday,
        decimal emiOutstanding,
        bool isExempt,
        decimal? maximumFeeAmount,
        int? maximumPenaltyDays,
        IReadOnlyDictionary<DateOnly, decimal> existingFees)
    {
        if (isExempt || emiOutstanding <= 0 || originalEmiAmount <= 0 || rate <= 0) return Array.Empty<DailyLateFee>();
        var firstDate = dueDate.AddDays(gracePeriodDays + 1);
        var lastDate = businessToday.AddDays(-1); // Due date and current business day are fee-free.
        if (lastDate < firstDate) return Array.Empty<DailyLateFee>();

        if (maximumPenaltyDays is <= 0) return Array.Empty<DailyLateFee>();
        var amountLeft = maximumFeeAmount is null
            ? decimal.MaxValue
            : Math.Max(0m, maximumFeeAmount.Value - existingFees.Values.Sum());
        var lastAllowedDate = maximumPenaltyDays is null
            ? lastDate
            : firstDate.AddDays(maximumPenaltyDays.Value - 1);
        var dailyAmount = Math.Round(originalEmiAmount * rate / 100m, 2, MidpointRounding.AwayFromZero);
        if (dailyAmount <= 0) return Array.Empty<DailyLateFee>();

        var result = new List<DailyLateFee>();
        for (var date = firstDate; date <= lastDate && date <= lastAllowedDate && amountLeft > 0; date = date.AddDays(1))
        {
            if (existingFees.ContainsKey(date)) continue;
            var amount = Math.Min(dailyAmount, amountLeft);
            if (amount <= 0) break;
            result.Add(new DailyLateFee(date, originalEmiAmount, rate, amount));
            amountLeft -= amount;
        }
        return result;
    }
}

public sealed record EmiAllocation(string Component, decimal Amount, int? LateFeeId = null);
public sealed record FeeBalance(int LateFeeId, DateOnly PenaltyDate, decimal Outstanding);

public static class RepaymentBalances
{
    public static decimal OpenLateFee(decimal amount, decimal amountPaid, string status) =>
        status == "Pending" ? Math.Max(0m, amount - amountPaid) : 0m;
}

public static class EmiPaymentAllocator
{
    public static IReadOnlyList<EmiAllocation> Allocate(decimal paymentAmount, decimal emiOutstanding, IEnumerable<FeeBalance> feeBalances)
    {
        if (paymentAmount <= 0) throw new ArgumentOutOfRangeException(nameof(paymentAmount));
        var left = paymentAmount;
        var results = new List<EmiAllocation>();
        var emi = Math.Min(left, Math.Max(0m, emiOutstanding));
        if (emi > 0) { results.Add(new EmiAllocation("Emi", emi)); left -= emi; }
        foreach (var fee in feeBalances.OrderBy(f => f.PenaltyDate).ThenBy(f => f.LateFeeId))
        {
            if (left <= 0) break;
            var amount = Math.Min(left, Math.Max(0m, fee.Outstanding));
            if (amount <= 0) continue;
            results.Add(new EmiAllocation("LateFee", amount, fee.LateFeeId));
            left -= amount;
        }
        if (left > 0) throw new ArgumentException("Payment exceeds the total outstanding balance.", nameof(paymentAmount));
        return results;
    }
}
