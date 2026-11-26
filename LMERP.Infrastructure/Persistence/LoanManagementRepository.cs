using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Domain.Entities;
using LMERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

public sealed class LoanManagementRepository(LoanDbContext db, IBusinessClock businessClock) : ILoanManagementRepository
{
    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().Include(customer => customer.LoanApplications).OrderByDescending(customer => customer.CreatedAt).ToListAsync(cancellationToken);

    public Task<Customer?> GetCustomerAsync(int id, CancellationToken cancellationToken) =>
        db.Customers.Include(customer => customer.LoanApplications).SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<bool> PhoneExistsAsync(string phone, int? excludingId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(customer => customer.Phone == phone && (!excludingId.HasValue || customer.Id != excludingId.Value), cancellationToken);

    public Task<bool> CustomerHasLoansAsync(int id, CancellationToken cancellationToken) =>
        db.LoanApplications.IgnoreQueryFilters().AnyAsync(loan => loan.CustomerId == id, cancellationToken);

    public async Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        db.Entry(customer).Property(entity => entity.RowVersion).OriginalValue = customer.RowVersion;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanApplication>> GetLoansAsync(CancellationToken cancellationToken) =>
        await db.LoanApplications.AsNoTracking().Include(loan => loan.Customer).OrderByDescending(loan => loan.AppliedAt).ToListAsync(cancellationToken);

    public Task<LoanApplication?> GetLoanAsync(int id, CancellationToken cancellationToken) =>
        db.LoanApplications.AsNoTracking().Include(loan => loan.Customer).SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public Task<LoanApplication?> GetLoanForUpdateAsync(int id, CancellationToken cancellationToken) =>
        db.LoanApplications.Include(loan => loan.Customer).SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public Task<Customer?> GetActiveCustomerAsync(int id, CancellationToken cancellationToken) =>
        db.Customers.SingleOrDefaultAsync(customer => customer.Id == id && customer.Status == "Active", cancellationToken);

    public async Task AddLoanAsync(LoanApplication loan, CancellationToken cancellationToken)
    {
        db.LoanApplications.Add(loan);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLoanAsync(LoanApplication loan, CancellationToken cancellationToken)
    {
        db.Entry(loan).Property(entity => entity.RowVersion).OriginalValue = loan.RowVersion;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> GetNextApplicationNumberAsync(CancellationToken cancellationToken)
    {
        // Keep this as a non-composed query: SQL Server does not allow NEXT VALUE FOR inside a subquery,
        // and SingleAsync() composes the scalar query into one.
        var values = await db.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR [dbo].[LoanApplicationNumbers] AS [Value]")
            .ToListAsync(cancellationToken);

        return values.Single();
    }

    public async Task UpdateLoanAndAddEmiScheduleAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedule, CancellationToken cancellationToken)
    {
        db.EmiSchedules.AddRange(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteLoanAsync(LoanApplication loan, CancellationToken cancellationToken)
    {
        loan.IsDeleted = true;
        loan.DeletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasEmiScheduleAsync(int loanId, CancellationToken cancellationToken) =>
        db.EmiSchedules.AnyAsync(schedule => schedule.LoanApplicationId == loanId, cancellationToken);

    public Task<bool> LoanHasPaymentsAsync(int loanId, CancellationToken cancellationToken) =>
        db.EmiPayments.AnyAsync(payment => payment.EmiSchedule!.LoanApplicationId == loanId, cancellationToken);

    public Task<bool> HasUnpaidEmiSchedulesAsync(int loanId, CancellationToken cancellationToken) =>
        db.EmiSchedules.AnyAsync(schedule => schedule.LoanApplicationId == loanId && schedule.Status != "Paid", cancellationToken);

    public Task<LoanProduct?> GetLoanProductAsync(string name, CancellationToken cancellationToken) =>
        db.LoanProducts.SingleOrDefaultAsync(product => product.Name == name && product.IsActive, cancellationToken);

    public async Task<IReadOnlyList<EmiSchedule>> GetEmiScheduleAsync(int loanId, CancellationToken cancellationToken) =>
        await db.EmiSchedules.AsNoTracking()
            .Where(schedule => schedule.LoanApplicationId == loanId)
            .OrderBy(schedule => schedule.InstallmentNumber)
            .ToListAsync(cancellationToken);

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var totalLoans = await db.LoanApplications.CountAsync(loan=>!loan.IsDeleted,cancellationToken);
        var activeLoans = await db.LoanApplications.CountAsync(loan => !loan.IsDeleted && loan.Status == LoanStatuses.Disbursed, cancellationToken);
        var pendingApplications = await db.LoanApplications.CountAsync(loan =>!loan.IsDeleted && loan.Status == LoanStatuses.UnderReview || loan.Status == LoanStatuses.DocumentsNeeded, cancellationToken);
        var totalDisbursed = await db.LoanApplications
            .Where(loan =>!loan.IsDeleted && loan.Status == LoanStatuses.Disbursed || loan.Status == LoanStatuses.Closed)
            .SumAsync(loan => (decimal?)loan.RequestedAmount, cancellationToken) ?? 0m;
        var schedules = await db.EmiSchedules.AsNoTracking()
            .Where(schedule => schedule.LoanApplication!.Status == LoanStatuses.Disbursed)
            .Select(schedule => new
            {
                schedule.Id,
                schedule.LoanApplicationId,
                schedule.DueDate,
                schedule.EmiAmount,
                EmiPaid = db.EmiPaymentAllocations
                    .Where(allocation => allocation.EmiScheduleId == schedule.Id && allocation.Component == "Emi")
                    .Sum(allocation => (decimal?)allocation.Amount) ?? 0m
            }).ToListAsync(cancellationToken);
        var pendingFees = await db.EmiLateFees.AsNoTracking()
            .Where(fee => fee.Status == "Pending")
            .Select(fee => new
            {
                fee.EmiScheduleId,
                Balance = fee.PenaltyAmount - db.EmiPaymentAllocations
                    .Where(allocation => allocation.EmiLateFeeId == fee.Id && allocation.Component == "LateFee")
                    .Sum(allocation => (decimal?)allocation.Amount) ?? 0m
            }).ToListAsync(cancellationToken);
        var feesBySchedule = pendingFees.GroupBy(fee => fee.EmiScheduleId)
            .ToDictionary(group => group.Key, group => group.Sum(fee => Math.Max(0m, fee.Balance)));
        var scheduleBalances = schedules.Select(schedule => new
        {
            schedule.LoanApplicationId,
            schedule.DueDate,
            Balance = Math.Max(0m, schedule.EmiAmount - schedule.EmiPaid)
                + feesBySchedule.GetValueOrDefault(schedule.Id)
        }).Where(schedule => schedule.Balance > 0m).ToArray();
        var outstanding = scheduleBalances.Sum(schedule => schedule.Balance);
        var localMidnight = businessClock.Today.ToDateTime(TimeOnly.MinValue);
        var businessDayStart = new DateTimeOffset(localMidnight, businessClock.TimeZone.GetUtcOffset(localMidnight));
        var overdueSchedules = scheduleBalances.Where(schedule => schedule.DueDate < businessDayStart);
        var overdueLoans = overdueSchedules.Select(schedule => schedule.LoanApplicationId).Distinct().Count();
        var overdueAmount = overdueSchedules.Sum(schedule => schedule.Balance);
        return new DashboardSummary(
            await db.Customers.CountAsync(cancellationToken), totalLoans, activeLoans, pendingApplications,
            totalDisbursed, outstanding, overdueLoans, overdueAmount);
    }

    public async Task<IReadOnlyList<DashboardTransactionResponse>> GetRecentDashboardTransactionsAsync(CancellationToken cancellationToken)
    {
        var payments = await db.EmiPayments.AsNoTracking()
            .OrderByDescending(payment => payment.ReceivedAt)
            .Take(10)
            .Select(payment => new DashboardTransactionResponse(
                payment.Id,
                payment.EmiSchedule!.LoanApplication!.Customer!.FullName,
                payment.EmiSchedule.LoanApplication.ApplicationNumber,
                "Repayment",
                payment.Amount,
                payment.ReceivedAt,
                payment.Method))
            .ToListAsync(cancellationToken);

        var disbursements = await db.LoanApplications.AsNoTracking()
            .Where(loan => loan.DisbursedAt.HasValue)
            .OrderByDescending(loan => loan.DisbursedAt)
            .Take(10)
            .Select(loan => new DashboardTransactionResponse(
                -loan.Id,
                loan.Customer!.FullName,
                loan.ApplicationNumber,
                "Disbursement",
                loan.RequestedAmount,
                loan.DisbursedAt!.Value,
                null))
            .ToListAsync(cancellationToken);

        return payments.Concat(disbursements)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Take(5)
            .ToArray();
    }
}
