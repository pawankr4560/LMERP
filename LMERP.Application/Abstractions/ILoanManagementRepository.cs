using LMERP.Domain.Entities;
using LMERP.Application.Contracts;

namespace LMERP.Application.Abstractions;

public interface ILoanManagementRepository
{
    Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken);
    Task<Customer?> GetCustomerAsync(int id, CancellationToken cancellationToken);
    Task<bool> PhoneExistsAsync(string phone, int? excludingId, CancellationToken cancellationToken);
    Task<bool> CustomerHasLoansAsync(int id, CancellationToken cancellationToken);
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken);
    Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken);
    Task DeleteCustomerAsync(Customer customer, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanApplication>> GetLoansAsync(CancellationToken cancellationToken);
    Task<LoanApplication?> GetLoanAsync(int id, CancellationToken cancellationToken);
    Task<LoanApplication?> GetLoanForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<Customer?> GetActiveCustomerAsync(int id, CancellationToken cancellationToken);
    Task AddLoanAsync(LoanApplication loan, CancellationToken cancellationToken);
    Task UpdateLoanAsync(LoanApplication loan, CancellationToken cancellationToken);
    Task UpdateLoanAndAddEmiScheduleAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedule, CancellationToken cancellationToken);
    Task SoftDeleteLoanAsync(LoanApplication loan, CancellationToken cancellationToken);
    Task<bool> HasEmiScheduleAsync(int loanId, CancellationToken cancellationToken);
    Task<LoanProduct?> GetLoanProductAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmiSchedule>> GetEmiScheduleAsync(int loanId, CancellationToken cancellationToken);
    Task<Contracts.DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken);
}
