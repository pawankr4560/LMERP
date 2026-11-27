using System.Security.Claims;
using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Server.Auth;
using LMERP.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize(Roles = Roles.CollectionsStaff)]
[Route("api/foreclosures")]
public sealed class LoanForeclosureController(ILoanForeclosureService service) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<BaseResponse<ForeclosureSettingsResponse>>> GetSettings(CancellationToken ct) =>
        Ok(BaseResponse<ForeclosureSettingsResponse>.Succeeded(await service.GetSettingsAsync(ct)));

    [Authorize(Roles = Roles.Administrator)]
    [HttpPut("settings")]
    public async Task<ActionResult<BaseResponse<ForeclosureSettingsResponse>>> UpdateSettings(ForeclosureSettingsRequest request, CancellationToken ct) =>
        Ok(BaseResponse<ForeclosureSettingsResponse>.Succeeded(await service.UpdateSettingsAsync(request, Actor(), ct), "Foreclosure settings updated."));

    [HttpGet("loans/{loanNumber}")]
    public async Task<ActionResult<BaseResponse<ForeclosureLoanResponse>>> Calculate(string loanNumber, CancellationToken ct) =>
        Ok(BaseResponse<ForeclosureLoanResponse>.Succeeded(await service.GetCalculationAsync(loanNumber, ct)));

    [HttpGet]
    public async Task<ActionResult<BaseResponse<IReadOnlyList<ForeclosureHistoryResponse>>>> GetHistory([FromQuery] string? loanNumber, CancellationToken ct) =>
        Ok(BaseResponse<IReadOnlyList<ForeclosureHistoryResponse>>.Succeeded(await service.GetHistoryAsync(loanNumber, ct)));

    [HttpPost("loans/{loanNumber}")]
    public async Task<ActionResult<BaseResponse<ForeclosureHistoryResponse>>> Confirm(string loanNumber, ForeclosurePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        ValidateKey(idempotencyKey);
        var result = await service.ConfirmAsync(loanNumber, request, Actor(), idempotencyKey, ct);
        return StatusCode(StatusCodes.Status201Created, BaseResponse<ForeclosureHistoryResponse>.Succeeded(result, "Foreclosure payment recorded."));
    }

    [HttpPost("{foreclosureId:int}/payments")]
    public async Task<ActionResult<BaseResponse<ForeclosureHistoryResponse>>> CollectPayment(int foreclosureId, ForeclosurePaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        ValidateKey(idempotencyKey);
        return Ok(BaseResponse<ForeclosureHistoryResponse>.Succeeded(
            await service.CollectPaymentAsync(foreclosureId, request, Actor(), idempotencyKey, ct), "Foreclosure payment recorded."));
    }

    private string Actor() => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "unknown";
    private static void ValidateKey(string? key)
    {
        if (key is { Length: > 64 } or { Length: 0 }) throw new LMERP.Application.Exceptions.DomainValidationException("Idempotency-Key must contain between 1 and 64 characters.");
    }
}
