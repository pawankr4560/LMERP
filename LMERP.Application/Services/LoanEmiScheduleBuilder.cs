using LMERP.Domain.Entities;

namespace LMERP.Application.Services;

public static class LoanEmiScheduleBuilder
{
    public static IReadOnlyList<EmiSchedule> Build(LoanApplication loan, DateTimeOffset baseDate, LoanProduct? product)
    {
        var principal = loan.RequestedAmount;
        var monthlyRate = loan.InterestRate / 1200m;
        var months = loan.TenureMonths;
        var flatInterestTotal = Round(principal * loan.InterestRate / 100m * months / 12m);
        var flatInterestPerInstallment = Round(flatInterestTotal / months);
        var flatPrincipalPerInstallment = Round(principal / months);
        var reducingEmi = CalculateReducingEmi(principal, monthlyRate, months);
        var remainingBalance = principal;
        var interestCharged = 0m;
        var schedule = new List<EmiSchedule>(months);

        for (var installmentNumber = 1; installmentNumber <= months; installmentNumber++)
        {
            var isFinalInstallment = installmentNumber == months;
            decimal principalAmount;
            decimal interestAmount;
            if (loan.IsFlatInterest)
            {
                principalAmount = isFinalInstallment ? remainingBalance : Math.Min(remainingBalance, flatPrincipalPerInstallment);
                interestAmount = isFinalInstallment ? flatInterestTotal - interestCharged : flatInterestPerInstallment;
            }
            else
            {
                interestAmount = Round(remainingBalance * monthlyRate);
                principalAmount = isFinalInstallment ? remainingBalance : Math.Min(remainingBalance, Round(reducingEmi - interestAmount));
            }
            principalAmount = Round(principalAmount);
            interestAmount = Round(interestAmount);
            remainingBalance = Math.Max(0m, Round(remainingBalance - principalAmount));
            interestCharged += interestAmount;
            schedule.Add(new EmiSchedule
            {
                LoanApplicationId = loan.Id,
                InstallmentNumber = installmentNumber,
                DueDate = baseDate.AddMonths(installmentNumber),
                PrincipalAmount = principalAmount,
                InterestAmount = interestAmount,
                EmiAmount = Round(principalAmount + interestAmount),
                RemainingBalance = remainingBalance,
                Status = "Pending",
                IsLateFeeApplicable = product?.IsLateFeeApplicable ?? false,
                LateFeeRate = product?.LateFeeRate ?? 0m,
                LateFeeCalculationType = product?.LateFeeCalculationType ?? "DailyPercentageOfEmi",
                GracePeriodDays = product?.GracePeriodDays ?? 0,
                MaximumLateFeeAmount = product?.MaximumLateFeeAmount,
                MaximumPenaltyDays = product?.MaximumPenaltyDays
            });
        }
        return schedule;
    }

    private static decimal CalculateReducingEmi(decimal principal, decimal monthlyRate, int months)
    {
        if (monthlyRate == 0m) return Round(principal / months);
        var factor = (decimal)Math.Pow((double)(1m + monthlyRate), months);
        return Round(principal * monthlyRate * factor / (factor - 1m));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
