using System.Security.Claims;
using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Server.Responses;
using LMERP.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize(Roles = Roles.CollectionsStaff)]
[Route("api/repayments")]
public sealed class RepaymentsController(IEmiRepaymentService service) : ControllerBase
{
    [HttpGet("overdue")]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<OverdueEmiResponse>>>> GetOverdue(CancellationToken ct) =>
        Ok(BaseResponse<IReadOnlyList<OverdueEmiResponse>>.Succeeded(await service.GetOverdueAsync(ct)));

    [HttpGet("summary")]
    public async Task<ActionResult<BaseResponse<RepaymentSummaryResponse>>> GetSummary(CancellationToken ct) =>
        Ok(BaseResponse<RepaymentSummaryResponse>.Succeeded(await service.GetSummaryAsync(ct)));

    [HttpGet("emi/{scheduleId:int}")]
    public async Task<ActionResult<BaseResponse<OverdueEmiResponse>>> GetDetails(int scheduleId, CancellationToken ct)
    {
        var details = await service.GetDetailsAsync(scheduleId, ct);
        return details is null ? NotFound(BaseResponse<OverdueEmiResponse>.Failed("EMI schedule was not found.")) : Ok(BaseResponse<OverdueEmiResponse>.Succeeded(details));
    }

    [HttpPost("emi/{scheduleId:int}/payments")]
    public async Task<ActionResult<BaseResponse<EmiPaymentResponse>>> CollectPayment(int scheduleId, EmiPaymentRequest request, CancellationToken ct) =>
        Ok(BaseResponse<EmiPaymentResponse>.Succeeded(await service.CollectPaymentAsync(scheduleId, request, ct), "Payment recorded."));

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost("late-fees/{feeId:int}/waive")]
    public async Task<ActionResult<BaseResponse<bool>>> WaiveLateFee(int feeId, WaiveLateFeeRequest request, CancellationToken ct)
    {
        await service.WaiveFeeAsync(feeId, request.Reason, CurrentActor(), ct);
        return Ok(BaseResponse<bool>.Succeeded(true, "Late fee waived."));
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost("emi/{scheduleId:int}/exempt-late-fees")]
    public async Task<ActionResult<BaseResponse<bool>>> ExemptLateFees(int scheduleId, WaiveLateFeeRequest request, CancellationToken ct)
    {
        await service.ExemptScheduleAsync(scheduleId, request.Reason, CurrentActor(), ct);
        return Ok(BaseResponse<bool>.Succeeded(true, "EMI exempted from future late fees."));
    }

    [HttpGet("products")]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<LoanProductResponse>>>> GetProducts(CancellationToken ct) =>
        Ok(BaseResponse<IReadOnlyList<LoanProductResponse>>.Succeeded(await service.GetProductsAsync(ct)));

    [Authorize(Roles = Roles.Administrator)]
    [HttpPost("products")]
    public async Task<ActionResult<BaseResponse<LoanProductResponse>>> CreateProduct(LoanProductLateFeeRequest request, CancellationToken ct) =>
        Ok(BaseResponse<LoanProductResponse>.Succeeded(await service.SaveProductAsync(null, request, ct), "Loan product settings saved."));

    [Authorize(Roles = Roles.Administrator)]
    [HttpPut("products/{id:int}")]
    public async Task<ActionResult<BaseResponse<LoanProductResponse>>> UpdateProduct(int id, LoanProductLateFeeRequest request, CancellationToken ct) =>
        Ok(BaseResponse<LoanProductResponse>.Succeeded(await service.SaveProductAsync(id, request, ct), "Loan product settings updated."));

    private string CurrentActor() => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "unknown";
}
