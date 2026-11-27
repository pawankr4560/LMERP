using LMERP.Application.Contracts;

namespace LMERP.Application.Services;

public interface ILoanForeclosureService
{
    Task<ForeclosureSettingsResponse> GetSettingsAsync(CancellationToken ct);
    Task<ForeclosureSettingsResponse> UpdateSettingsAsync(ForeclosureSettingsRequest request, string actor, CancellationToken ct);
    Task<ForeclosureLoanResponse> GetCalculationAsync(string loanNumber, CancellationToken ct);
    Task<IReadOnlyList<ForeclosureHistoryResponse>> GetHistoryAsync(string? loanNumber, CancellationToken ct);
    Task<ForeclosureHistoryResponse> ConfirmAsync(string loanNumber, ForeclosurePaymentRequest request, string actor, string? idempotencyKey, CancellationToken ct);
    Task<ForeclosureHistoryResponse> CollectPaymentAsync(int foreclosureId, ForeclosurePaymentRequest request, string actor, string? idempotencyKey, CancellationToken ct);
}
