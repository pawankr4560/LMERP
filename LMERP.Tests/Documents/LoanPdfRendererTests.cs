using System.Security.Claims;
using LMERP.Domain;
using LMERP.Domain.Entities;
using LMERP.Application.Services;
using LMERP.Infrastructure.Documents;
using LMERP.Server.Auth;
using Xunit;

namespace LMERP.Tests.Documents;

public sealed class LoanPdfRendererTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(360)]
    public void RenderDisbursementAdvice_UsesPersistedScheduleAndWorksWithoutCompanyImages(int tenureMonths)
    {
        var loan = new LoanApplication
        {
            Id = 8, ApplicationNumber = "LN-2026-000008", CustomerId = 3,
            Customer = new Customer { Id = 3, FullName = "Sample Customer", Phone = "+91 90000 00000", City = "Mumbai" },
            RequestedAmount = 120000m, TenureMonths = tenureMonths, InterestRate = 12m,
            IsFlatInterest = true, Status = LoanStatuses.Disbursed, DisbursedAt = DateTimeOffset.UtcNow
        };
        var schedules = LoanEmiScheduleBuilder.Build(loan, loan.DisbursedAt.Value, null);

        var company = new CompanyDocumentSettings { CompanyName = "Example Finance" };
        var pdf = LoanPdfRenderer.Render(company, loan, schedules,
            latestStatement: false, DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        Assert.Equal(tenureMonths, schedules.Count);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void CurrentStatus_DistinguishesPendingPartiallyPaidPaidAndOverdue()
    {
        var now = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.Utc;
        var pending = new EmiSchedule { EmiAmount = 100m, DueDate = now.AddDays(1) };
        var partial = NewWithPayment(100m, 40m, now.AddDays(1));
        var paid = NewWithPayment(100m, 100m, now.AddDays(-1));
        var overdue = NewWithPayment(100m, 40m, now.AddDays(-1));

        Assert.Equal("Pending", LoanPdfRenderer.CurrentStatus(pending, now, zone));
        Assert.Equal("Partially paid", LoanPdfRenderer.CurrentStatus(partial, now, zone));
        Assert.Equal("Paid", LoanPdfRenderer.CurrentStatus(paid, now, zone));
        Assert.Equal("Overdue", LoanPdfRenderer.CurrentStatus(overdue, now, zone));
    }

    [Fact]
    public void CurrencyAndDateFormatting_UseIndianRupeesAndReadableDates()
    {
        Assert.Equal("₹ 1,23,456.78", LoanPdfRenderer.FormatMoney(123456.78m));
        Assert.Equal("27 Nov 2026", LoanPdfRenderer.FormatDate(new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2.5% per month", LoanPdfRenderer.FormatMonthlyInterestRate(30m));
    }

    [Fact]
    public void CustomerDocumentAuthorization_AllowsOnlyLinkedCustomerAndStaff()
    {
        var customer = Principal((ClaimTypes.Role, Roles.Customer), (CustomerClaims.CustomerId, "18"));
        var staff = Principal((ClaimTypes.Role, Roles.LoanOfficer));
        var otherCustomer = Principal((ClaimTypes.Role, Roles.Customer), (CustomerClaims.CustomerId, "19"));

        Assert.True(LoanDocumentAuthorization.CanRead(customer, 18));
        Assert.False(LoanDocumentAuthorization.CanRead(customer, 19));
        Assert.True(LoanDocumentAuthorization.CanRead(staff, 19));
        Assert.False(LoanDocumentAuthorization.CanRead(otherCustomer, 18));
    }

    private static EmiSchedule NewWithPayment(decimal emi, decimal paid, DateTimeOffset dueDate) => new()
    {
        EmiAmount = emi, DueDate = dueDate,
        Payments = [new EmiPayment { Allocations = [new EmiPaymentAllocation { Component = "Emi", Amount = paid }] }]
    };

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) => new(new ClaimsIdentity(
        claims.Select(claim => new Claim(claim.Type, claim.Value)), "test"));
}
