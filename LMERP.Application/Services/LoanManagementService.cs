using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Domain.Entities;

namespace LMERP.Application.Services;

public sealed class LoanManagementService(ILoanManagementRepository repository) : ILoanManagementService
{
    public async Task<IReadOnlyList<CustomerResponse>> GetCustomersAsync(CancellationToken cancellationToken) =>
        (await repository.GetCustomersAsync(cancellationToken)).Select(ToResponse).ToArray();

    public async Task<OperationResult<CustomerResponse>> GetCustomerAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(id, cancellationToken);
        return customer is null ? OperationResult<CustomerResponse>.NotFound("Customer was not found.") : OperationResult<CustomerResponse>.Ok(ToResponse(customer));
    }

    public async Task<OperationResult<CustomerResponse>> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        var phone = request.Phone.Trim();
        if (await repository.PhoneExistsAsync(phone, null, cancellationToken)) return OperationResult<CustomerResponse>.Conflict("Phone number already exists.");
        var customer = new Customer { FullName = request.FullName.Trim(), Phone = phone, Email = request.Email?.Trim(), City = request.City?.Trim(), Status = request.Status };
        await repository.AddCustomerAsync(customer, cancellationToken);
        return OperationResult<CustomerResponse>.Ok(ToResponse(customer), "Customer created.");
    }

    public async Task<OperationResult<CustomerResponse>> UpdateCustomerAsync(int id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(id, cancellationToken);
        if (customer is null) return OperationResult<CustomerResponse>.NotFound("Customer was not found.");
        var phone = request.Phone.Trim();
        if (await repository.PhoneExistsAsync(phone, id, cancellationToken)) return OperationResult<CustomerResponse>.Conflict("Phone number already exists.");
        customer.FullName = request.FullName.Trim(); customer.Phone = phone; customer.Email = request.Email?.Trim(); customer.City = request.City?.Trim(); customer.Status = request.Status;
        await repository.UpdateCustomerAsync(customer, cancellationToken);
        return OperationResult<CustomerResponse>.Ok(ToResponse(customer), "Customer updated.");
    }

    public async Task<OperationResult<bool>> DeleteCustomerAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(id, cancellationToken);
        if (customer is null) return OperationResult<bool>.NotFound("Customer was not found.");
        if (await repository.CustomerHasLoansAsync(id, cancellationToken)) return OperationResult<bool>.Conflict("Customers with loan applications cannot be deleted. Mark the customer inactive instead.");
        await repository.DeleteCustomerAsync(customer, cancellationToken);
        return OperationResult<bool>.Ok(true, "Customer deleted.");
    }

    public async Task<IReadOnlyList<LoanResponse>> GetLoansAsync(CancellationToken cancellationToken) =>
        (await repository.GetLoansAsync(cancellationToken)).Select(ToResponse).ToArray();

    public async Task<OperationResult<LoanResponse>> GetLoanAsync(int id, CancellationToken cancellationToken)
    {
        var loan = await repository.GetLoanAsync(id, cancellationToken);
        return loan is null ? OperationResult<LoanResponse>.NotFound("Loan application was not found.") : OperationResult<LoanResponse>.Ok(ToResponse(loan));
    }

    public async Task<OperationResult<LoanResponse>> CreateLoanAsync(LoanRequest request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetActiveCustomerAsync(request.CustomerId, cancellationToken);
        if (customer is null) return OperationResult<LoanResponse>.BadRequest("Select an active customer before creating an application.");
        var loan = new LoanApplication
        {
            ApplicationNumber = $"LN-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CustomerId = customer.Id, Customer = customer, Product = request.Product.Trim(), RequestedAmount = request.RequestedAmount,
            TenureMonths = request.TenureMonths, Status = "Under review"
        };
        await repository.AddLoanAsync(loan, cancellationToken);
        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan application created.");
    }

    public Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken) => repository.GetDashboardSummaryAsync(cancellationToken);

    private static CustomerResponse ToResponse(Customer customer) => new(customer.Id, customer.FullName, customer.Phone, customer.Email, customer.City, customer.Status, customer.CreatedAt, customer.LoanApplications.Count);
    private static LoanResponse ToResponse(LoanApplication loan) => new(loan.Id, loan.ApplicationNumber, loan.CustomerId, loan.Customer?.FullName ?? string.Empty, loan.Product, loan.RequestedAmount, loan.TenureMonths, loan.Status, loan.AppliedAt);
}
