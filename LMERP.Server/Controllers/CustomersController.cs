using LMERP.Server.Contracts;
using LMERP.Server.Data;
using LMERP.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController(LoanDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var customers = await db.Customers.AsNoTracking()
            .OrderByDescending(customer => customer.CreatedAt)
            .Select(customer => new CustomerResponse(customer.Id, customer.FullName, customer.Phone, customer.Email, customer.City, customer.Status, customer.CreatedAt, customer.LoanApplications.Count))
            .ToListAsync(cancellationToken);
        return Ok(customers);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new CustomerResponse(item.Id, item.FullName, item.Phone, item.Email, item.City, item.Status, item.CreatedAt, item.LoanApplications.Count))
            .SingleOrDefaultAsync(cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [Authorize(Roles = "Administrator,LoanOfficer")]
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CustomerRequest request, CancellationToken cancellationToken)
    {
        if (await db.Customers.AnyAsync(customer => customer.Phone == request.Phone, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Phone number already exists.", Status = StatusCodes.Status409Conflict });

        var customer = new Customer { FullName = request.FullName.Trim(), Phone = request.Phone.Trim(), Email = request.Email?.Trim(), City = request.City?.Trim(), Status = request.Status };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        var response = new CustomerResponse(customer.Id, customer.FullName, customer.Phone, customer.Email, customer.City, customer.Status, customer.CreatedAt, 0);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, response);
    }

    [Authorize(Roles = "Administrator,LoanOfficer")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerResponse>> Update(int id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.Include(item => item.LoanApplications).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (customer is null) return NotFound();
        if (await db.Customers.AnyAsync(item => item.Id != id && item.Phone == request.Phone, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Phone number already exists.", Status = StatusCodes.Status409Conflict });
        customer.FullName = request.FullName.Trim(); customer.Phone = request.Phone.Trim(); customer.Email = request.Email?.Trim(); customer.City = request.City?.Trim(); customer.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new CustomerResponse(customer.Id, customer.FullName, customer.Phone, customer.Email, customer.City, customer.Status, customer.CreatedAt, customer.LoanApplications.Count));
    }

    [Authorize(Roles = "Administrator")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (customer is null) return NotFound();
        if (await db.LoanApplications.AnyAsync(loan => loan.CustomerId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Customers with loan applications cannot be deleted. Mark the customer inactive instead.", Status = StatusCodes.Status409Conflict });
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
