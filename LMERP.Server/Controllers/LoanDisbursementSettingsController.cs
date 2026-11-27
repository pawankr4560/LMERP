using System.Security.Claims;
using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Server.Auth;
using LMERP.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize(Roles = Roles.Administrator)]
[Route("api/settings/disbursement-fees")]
public sealed class LoanDisbursementSettingsController(ILoanDisbursementSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BaseResponse<LoanDisbursementSettingsResponse>>> Get(CancellationToken ct) =>
        Ok(BaseResponse<LoanDisbursementSettingsResponse>.Succeeded(await service.GetAsync(ct)));

    [HttpPut]
    public async Task<ActionResult<BaseResponse<LoanDisbursementSettingsResponse>>> Update(LoanDisbursementSettingsRequest request, CancellationToken ct)
    {
        var actor = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "unknown";
        return Ok(BaseResponse<LoanDisbursementSettingsResponse>.Succeeded(await service.UpdateAsync(request, actor, ct), "Loan disbursement fees saved."));
    }
}
