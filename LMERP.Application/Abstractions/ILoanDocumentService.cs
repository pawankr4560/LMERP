using LMERP.Application.Contracts;
using LMERP.Domain.Entities;

namespace LMERP.Application.Abstractions;

public interface ILoanDocumentService
{
    Task<byte[]> GenerateDisbursementAdviceAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedules, CancellationToken ct);
    Task<LoanPdfFile?> GetDisbursementAdviceAsync(int loanId, CancellationToken ct);
    Task<LoanPdfFile> GenerateLatestEmiStatementAsync(int loanId, CancellationToken ct);
    Task<CompanyDocumentSettingsResponse> GetCompanySettingsAsync(CancellationToken ct);
    Task<CompanyDocumentSettingsResponse> UpdateCompanySettingsAsync(CompanyDocumentSettingsRequest request,
        CompanyDocumentAssetUpload? logo, CompanyDocumentAssetUpload? signature, CompanyDocumentAssetUpload? stamp, CancellationToken ct);
    Task<CompanyDocumentAsset?> GetCompanyAssetAsync(string assetName, CancellationToken ct);
}

public interface ICustomerDocumentAccessService
{
    Task<bool> LinkUserToCustomerAsync(int userId, int customerId, CancellationToken ct);
}
