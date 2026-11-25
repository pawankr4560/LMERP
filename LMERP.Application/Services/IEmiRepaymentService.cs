using LMERP.Application.Contracts;

namespace LMERP.Application.Services;

public interface IEmiRepaymentService
{
    Task<IReadOnlyList<OverdueEmiResponse>> GetOverdueAsync(CancellationToken cancellationToken);
    Task<RepaymentSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);
    Task<OverdueEmiResponse?> GetDetailsAsync(int scheduleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LoanProductResponse>> GetProductsAsync(CancellationToken cancellationToken);
    Task<LoanProductResponse> SaveProductAsync(int? id, LoanProductLateFeeRequest request, CancellationToken cancellationToken);
    Task<EmiPaymentResponse> CollectPaymentAsync(int scheduleId, EmiPaymentRequest request, CancellationToken cancellationToken);
    Task WaiveFeeAsync(int feeId, string reason, string actor, CancellationToken cancellationToken);
    Task ExemptScheduleAsync(int scheduleId, string reason, string actor, CancellationToken cancellationToken);
    Task<int> ProcessDailyFeesAsync(CancellationToken cancellationToken);
}
