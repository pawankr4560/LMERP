using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

public sealed class LoanManagementRepository(LoanDbContext db) : ILoanManagementRepository
{
    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().Include(customer => customer.LoanApplications).OrderByDescending(customer => customer.CreatedAt).ToListAsync(cancellationToken);

    public Task<Customer?> GetCustomerAsync(int id, CancellationToken cancellationToken) =>
        db.Customers.Include(customer => customer.LoanApplications).SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<bool> PhoneExistsAsync(string phone, int? excludingId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(customer => customer.Phone == phone && (!excludingId.HasValue || customer.Id != excludingId.Value), cancellationToken);

    public Task<bool> CustomerHasLoansAsync(int id, CancellationToken cancellationToken) =>
        db.LoanApplications.AnyAsync(loan => loan.CustomerId == id, cancellationToken);

    public async Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);

    public async Task DeleteCustomerAsync(Customer customer, CancellationToken cancellationToken)
    {
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanApplication>> GetLoansAsync(CancellationToken cancellationToken) =>
        await db.LoanApplications.AsNoTracking().Include(loan => loan.Customer).OrderByDescending(loan => loan.AppliedAt).ToListAsync(cancellationToken);

    public Task<LoanApplication?> GetLoanAsync(int id, CancellationToken cancellationToken) =>
        db.LoanApplications.AsNoTracking().Include(loan => loan.Customer).SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public Task<Customer?> GetActiveCustomerAsync(int id, CancellationToken cancellationToken) =>
        db.Customers.SingleOrDefaultAsync(customer => customer.Id == id && customer.Status == "Active", cancellationToken);

    public async Task AddLoanAsync(LoanApplication loan, CancellationToken cancellationToken)
    {
        db.LoanApplications.Add(loan);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var loans = await db.LoanApplications.AsNoTracking().ToListAsync(cancellationToken);
        return new DashboardSummary(
            await db.Customers.CountAsync(cancellationToken), loans.Count,
            loans.Count(loan => loan.Status is "Approved" or "Disbursed"),
            loans.Count(loan => loan.Status is "Under review" or "Documents needed"),
            loans.Where(loan => loan.Status == "Disbursed").Sum(loan => loan.RequestedAmount),
            loans.Where(loan => loan.Status is "Approved" or "Disbursed" or "Overdue").Sum(loan => loan.RequestedAmount),
            loans.Count(loan => loan.Status == "Overdue"));
    }
}
