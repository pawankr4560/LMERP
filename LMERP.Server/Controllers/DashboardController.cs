using LMERP.Server.Contracts;
using LMERP.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize(Roles = "Administrator,LoanOfficer,CollectionsManager,Analyst")]
[Route("api/dashboard")]
public sealed class DashboardController(LoanDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary(CancellationToken cancellationToken)
    {
        var loans = await db.LoanApplications.AsNoTracking().ToListAsync(cancellationToken);
        var summary = new DashboardSummary(
            await db.Customers.CountAsync(cancellationToken),
            loans.Count,
            loans.Count(loan => loan.Status is "Approved" or "Disbursed"),
            loans.Count(loan => loan.Status is "Under review" or "Documents needed"),
            loans.Where(loan => loan.Status == "Disbursed").Sum(loan => loan.RequestedAmount),
            loans.Where(loan => loan.Status is "Approved" or "Disbursed" or "Overdue").Sum(loan => loan.RequestedAmount),
            loans.Count(loan => loan.Status == "Overdue"));
        return Ok(summary);
    }
}
