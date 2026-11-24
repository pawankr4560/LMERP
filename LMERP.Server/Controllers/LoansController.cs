using LMERP.Server.Contracts;
using LMERP.Server.Data;
using LMERP.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/loans")]
public sealed class LoansController(LoanDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LoanResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var loans = await db.LoanApplications.AsNoTracking().Include(loan => loan.Customer)
            .OrderByDescending(loan => loan.AppliedAt)
            .Select(loan => new LoanResponse(loan.Id, loan.ApplicationNumber, loan.CustomerId, loan.Customer!.FullName, loan.Product, loan.RequestedAmount, loan.TenureMonths, loan.Status, loan.AppliedAt))
            .ToListAsync(cancellationToken);
        return Ok(loans);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoanResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var loan = await db.LoanApplications.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new LoanResponse(item.Id, item.ApplicationNumber, item.CustomerId, item.Customer!.FullName, item.Product, item.RequestedAmount, item.TenureMonths, item.Status, item.AppliedAt))
            .SingleOrDefaultAsync(cancellationToken);
        return loan is null ? NotFound() : Ok(loan);
    }

    [Authorize(Roles = "Administrator,LoanOfficer")]
    [HttpPost]
    public async Task<ActionResult<LoanResponse>> Create(LoanRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == request.CustomerId && item.Status == "Active", cancellationToken);
        if (customer is null) return BadRequest(new ProblemDetails { Title = "Select an active customer before creating an application.", Status = StatusCodes.Status400BadRequest });
        var loan = new LoanApplication
        {
            ApplicationNumber = $"LN-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CustomerId = customer.Id,
            Product = request.Product.Trim(),
            RequestedAmount = request.RequestedAmount,
            TenureMonths = request.TenureMonths,
            Status = "Under review"
        };
        db.LoanApplications.Add(loan);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = loan.Id }, new LoanResponse(loan.Id, loan.ApplicationNumber, customer.Id, customer.FullName, loan.Product, loan.RequestedAmount, loan.TenureMonths, loan.Status, loan.AppliedAt));
    }
}
