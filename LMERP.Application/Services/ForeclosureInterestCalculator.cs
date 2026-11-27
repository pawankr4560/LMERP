namespace LMERP.Application.Services;

public sealed record ForeclosureEmiPayment(DateOnly ReceivedDate, decimal Amount, decimal ScheduledPrincipal, decimal ScheduledInterest, decimal ScheduledEmi);
public sealed record ForeclosureInterestResult(decimal OutstandingPrincipal, decimal AccruedInterest);

/// <summary>Applies historical EMI payments pro rata to installment principal/interest, then accrues simple daily interest on the remaining principal.</summary>
public static class ForeclosureInterestCalculator
{
    public static ForeclosureInterestResult Calculate(decimal scheduledPrincipal, decimal annualRatePercent, DateOnly accrualStart, DateOnly asOf, IEnumerable<ForeclosureEmiPayment> payments)
    {
        var principal = Math.Max(0m, scheduledPrincipal);
        var accruedInterest = 0m;
        var lastDate = accrualStart;
        foreach (var payment in payments.Where(p => p.Amount > 0 && p.ScheduledEmi > 0).OrderBy(p => p.ReceivedDate))
        {
            var paymentDate = payment.ReceivedDate;
            var days = Math.Max(0, paymentDate.DayNumber - lastDate.DayNumber);
            accruedInterest += principal * annualRatePercent / 100m * days / 365m;
            var interestPaid = payment.Amount * payment.ScheduledInterest / payment.ScheduledEmi;
            var principalPaid = payment.Amount * payment.ScheduledPrincipal / payment.ScheduledEmi;
            accruedInterest = Math.Max(0m, accruedInterest - interestPaid);
            principal = Math.Max(0m, principal - principalPaid);
            if (paymentDate > lastDate) lastDate = paymentDate;
        }
        var finalDays = Math.Max(0, asOf.DayNumber - lastDate.DayNumber);
        accruedInterest += principal * annualRatePercent / 100m * finalDays / 365m;
        return new ForeclosureInterestResult(
            decimal.Round(principal, 2, MidpointRounding.AwayFromZero),
            decimal.Round(accruedInterest, 2, MidpointRounding.AwayFromZero));
    }
}
