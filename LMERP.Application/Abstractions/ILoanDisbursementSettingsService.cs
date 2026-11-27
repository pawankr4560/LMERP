using LMERP.Application.Contracts;

namespace LMERP.Application.Abstractions;

public interface ILoanDisbursementSettingsService
{
    Task<LoanDisbursementSettingsResponse> GetAsync(CancellationToken cancellationToken);
    Task<LoanDisbursementSettingsResponse> UpdateAsync(LoanDisbursementSettingsRequest request, string updatedBy, CancellationToken cancellationToken);
}
