using LMERP.Application.Services;
using Xunit;

namespace LMERP.Tests.Services;

public sealed class ForeclosureInterestCalculatorTests
{
    [Theory]
    [InlineData(8, 608)]
    [InlineData(5, 380)]
    public void CalculatesServiceFeeFromRemainingEmiTotal(decimal percentage, decimal expectedFee)
    {
        var result = ForeclosureAmountCalculator.Calculate(2_000m, 50m, 100m, 7_600m, percentage);
        Assert.Equal(expectedFee, result.ServiceFeeAmount);
        Assert.Equal(2_150m + expectedFee, result.TotalPayable);
    }

    [Fact]
    public void CalculatesSimpleDailyInterestWhenThereAreNoPayments()
    {
        var result = ForeclosureInterestCalculator.Calculate(10_000m, 12m, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), []);
        Assert.Equal(10_000m, result.OutstandingPrincipal);
        Assert.Equal(1_200m, result.AccruedInterest);
    }

    [Fact]
    public void SplitsPartialInstallmentPaymentProRataAndReducesPrincipal()
    {
        var payment = new ForeclosureEmiPayment(new DateOnly(2026, 2, 1), 550m, 900m, 100m, 1_000m);
        var result = ForeclosureInterestCalculator.Calculate(10_000m, 12m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), [payment]);
        Assert.Equal(9_505m, result.OutstandingPrincipal);
        Assert.Equal(46.92m, result.AccruedInterest);
    }

    [Fact]
    public void DoesNotProduceNegativeAccruedInterestWhenPaidInterestExceedsAccrual()
    {
        var payment = new ForeclosureEmiPayment(new DateOnly(2026, 1, 2), 1_000m, 900m, 100m, 1_000m);
        var result = ForeclosureInterestCalculator.Calculate(10_000m, 12m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), [payment]);
        Assert.Equal(9_100m, result.OutstandingPrincipal);
        Assert.Equal(0m, result.AccruedInterest);
    }
}
