using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize(Roles = "Administrator,LoanOfficer,CollectionsManager,Analyst")]
[Route("api/dashboard")]
public sealed class DashboardController(ILoanManagementService service) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<BaseResponse<DashboardSummary>>> GetSummary(CancellationToken cancellationToken) =>
        Ok(BaseResponse<DashboardSummary>.Succeeded(await service.GetDashboardSummaryAsync(cancellationToken)));
}
