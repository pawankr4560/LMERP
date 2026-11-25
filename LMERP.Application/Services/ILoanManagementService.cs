using LMERP.Application.Contracts;

namespace LMERP.Application.Services;

public interface ILoanManagementService
{
    Task<IReadOnlyList<CustomerResponse>> GetCustomersAsync(CancellationToken cancellationToken);
    Task<OperationResult<CustomerResponse>> GetCustomerAsync(int id, CancellationToken cancellationToken);
    Task<OperationResult<CustomerResponse>> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken);
    Task<OperationResult<CustomerResponse>> UpdateCustomerAsync(int id, CustomerRequest request, CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteCustomerAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanResponse>> GetLoansAsync(CancellationToken cancellationToken);
    Task<OperationResult<LoanResponse>> GetLoanAsync(int id, CancellationToken cancellationToken);
    Task<OperationResult<LoanResponse>> CreateLoanAsync(LoanRequest request, CancellationToken cancellationToken);
    Task<OperationResult<LoanResponse>> UpdateLoanAsync(int id, LoanUpdateRequest request, CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteLoanAsync(int id, CancellationToken cancellationToken);
    Task<OperationResult<IReadOnlyList<EmiScheduleResponse>>> GetEmiScheduleAsync(int id, CancellationToken cancellationToken);
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken);
}
