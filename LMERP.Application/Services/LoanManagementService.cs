using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Domain.Entities;
using LMERP.Application.Exceptions;
using LMERP.Domain;

namespace LMERP.Application.Services;

public sealed class LoanManagementService(ILoanManagementRepository repository, CustomerPhoneOptions? phoneOptions = null) : ILoanManagementService
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
        var phone = PhoneNumber.Normalize(request.Phone, phoneOptions);
        if (await repository.PhoneExistsAsync(phone, null, cancellationToken)) return OperationResult<CustomerResponse>.Conflict("Phone number already exists.");
        var customer = new Customer { FullName = request.FullName.Trim(), Phone = phone, Email = request.Email?.Trim(), City = request.City?.Trim(), Status = request.Status };
        await repository.AddCustomerAsync(customer, cancellationToken);
        return OperationResult<CustomerResponse>.Ok(ToResponse(customer), "Customer created.");
    }

    public async Task<OperationResult<CustomerResponse>> UpdateCustomerAsync(int id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetCustomerAsync(id, cancellationToken);
        if (customer is null) return OperationResult<CustomerResponse>.NotFound("Customer was not found.");
        var phone = PhoneNumber.Normalize(request.Phone, phoneOptions);
        if (await repository.PhoneExistsAsync(phone, id, cancellationToken)) return OperationResult<CustomerResponse>.Conflict("Phone number already exists.");
        customer.FullName = request.FullName.Trim(); customer.Phone = phone; customer.Email = request.Email?.Trim(); customer.City = request.City?.Trim(); customer.Status = request.Status;
        customer.RowVersion = ParseRowVersion(request.RowVersion);
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
        if (await repository.GetLoanProductAsync(request.Product.Trim(), cancellationToken) is null)
            return OperationResult<LoanResponse>.BadRequest("Select an active loan product.");
        var customer = await repository.GetActiveCustomerAsync(request.CustomerId, cancellationToken);
        if (customer is null) return OperationResult<LoanResponse>.BadRequest("Select an active customer before creating an application.");
        var loan = new LoanApplication
        {
            ApplicationNumber = $"LN-{DateTime.UtcNow:yyyy}-{await repository.GetNextApplicationNumberAsync(cancellationToken):D6}",
            CustomerId = customer.Id, Customer = customer, Product = request.Product.Trim(), RequestedAmount = request.RequestedAmount,
            TenureMonths = request.TenureMonths, InterestRate = request.InterestRate, IsFlatInterest = request.IsFlatInterest, Status = LoanStatuses.UnderReview
        };
        await repository.AddLoanAsync(loan, cancellationToken);
        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan application created.");
    }

    public async Task<OperationResult<LoanResponse>> UpdateLoanAsync(int id, LoanUpdateRequest request, CancellationToken cancellationToken)
    {
        var loan = await repository.GetLoanForUpdateAsync(id, cancellationToken);
        if (loan is null) return OperationResult<LoanResponse>.NotFound("Loan application was not found.");

        var status = request.Status.Trim();
        if (status is not (LoanStatuses.UnderReview or LoanStatuses.DocumentsNeeded or LoanStatuses.Approved or LoanStatuses.Rejected))
            return OperationResult<LoanResponse>.BadRequest("Status must be Under review, Documents needed, Approved, or Rejected.");

        if (loan.Status is LoanStatuses.Disbursed or LoanStatuses.Closed or LoanStatuses.Rejected)
            return OperationResult<LoanResponse>.Conflict("This loan is in a terminal or disbursed state and cannot be edited.");
        if (!CanTransition(loan.Status, status))
            return OperationResult<LoanResponse>.Conflict($"Loan status cannot transition from {loan.Status} to {status}.");

        var hasEmiSchedule = await repository.HasEmiScheduleAsync(id, cancellationToken);
        loan.RowVersion = ParseRowVersion(request.RowVersion);
        var normalizedProduct = request.Product.Trim();
        if (loan.Product != normalizedProduct || status == LoanStatuses.Approved && loan.Status != LoanStatuses.Approved)
        {
            if (await repository.GetLoanProductAsync(normalizedProduct, cancellationToken) is null)
                return OperationResult<LoanResponse>.BadRequest("Select an active loan product.");
        }
        if (hasEmiSchedule)
        {
            var loanTermsChanged = loan.Product != request.Product.Trim()
                || loan.RequestedAmount != request.RequestedAmount
                || loan.TenureMonths != request.TenureMonths
                || loan.InterestRate != request.InterestRate
                || loan.IsFlatInterest != request.IsFlatInterest;
            if (loanTermsChanged)
                return OperationResult<LoanResponse>.Conflict("Loan terms cannot be changed after the EMI schedule has been generated.");
            if (loan.Status == LoanStatuses.Approved && status != LoanStatuses.Approved)
                return OperationResult<LoanResponse>.Conflict("An approved loan with an EMI schedule cannot be moved back to another status.");
        }

        loan.Product = normalizedProduct;
        loan.RequestedAmount = request.RequestedAmount;
        loan.TenureMonths = request.TenureMonths;
        loan.InterestRate = request.InterestRate;
        loan.IsFlatInterest = request.IsFlatInterest;
        loan.Status = status;
        await repository.UpdateLoanAsync(loan, cancellationToken);

        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan application updated.");
    }

    public async Task<OperationResult<LoanResponse>> DisburseLoanAsync(int id, CancellationToken cancellationToken)
    {
        var loan = await repository.GetLoanForUpdateAsync(id, cancellationToken);
        if (loan is null) return OperationResult<LoanResponse>.NotFound("Loan application was not found.");
        if (loan.Status != LoanStatuses.Approved || await repository.HasEmiScheduleAsync(id, cancellationToken))
            return OperationResult<LoanResponse>.Conflict("Only an approved loan without an EMI schedule can be disbursed.");

        var product = await repository.GetLoanProductAsync(loan.Product, cancellationToken);
        if (product is null) return OperationResult<LoanResponse>.Conflict("The loan product is unavailable for disbursement.");
        loan.DisbursedAt = DateTimeOffset.UtcNow;
        loan.Status = LoanStatuses.Disbursed;
        var schedule = LoanEmiScheduleBuilder.Build(loan, loan.DisbursedAt.Value, product);
        await repository.UpdateLoanAndAddEmiScheduleAsync(loan, schedule, cancellationToken);
        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan disbursed and EMI schedule generated.");
    }

    public async Task<OperationResult<LoanResponse>> RejectLoanAsync(int id, string reason, CancellationToken cancellationToken)
    {
        var rejectionReason = reason.Trim();
        if (rejectionReason.Length is < 5 or > 500)
            return OperationResult<LoanResponse>.BadRequest("Rejection reason must contain between 5 and 500 characters.");
        var loan = await repository.GetLoanForUpdateAsync(id, cancellationToken);
        if (loan is null) return OperationResult<LoanResponse>.NotFound("Loan application was not found.");
        if (!CanTransition(loan.Status, LoanStatuses.Rejected))
            return OperationResult<LoanResponse>.Conflict($"Loan status cannot transition from {loan.Status} to {LoanStatuses.Rejected}.");
        if (await repository.HasEmiScheduleAsync(id, cancellationToken))
            return OperationResult<LoanResponse>.Conflict("A loan with an EMI schedule cannot be rejected.");
        loan.Status = LoanStatuses.Rejected;
        loan.RejectionReason = rejectionReason;
        await repository.UpdateLoanAsync(loan, cancellationToken);
        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan application rejected.");
    }

    public async Task<OperationResult<bool>> DeleteLoanAsync(int id, CancellationToken cancellationToken)
    {
        var loan = await repository.GetLoanForUpdateAsync(id, cancellationToken);
        if (loan is null) return OperationResult<bool>.NotFound("Loan application was not found.");

        if (await repository.LoanHasPaymentsAsync(id, cancellationToken))
            return OperationResult<bool>.Conflict("Loans with recorded payments cannot be deleted.");
        if (await repository.HasEmiScheduleAsync(id, cancellationToken))
            return OperationResult<bool>.Conflict("Loans with a generated EMI schedule cannot be deleted.");

        await repository.SoftDeleteLoanAsync(loan, cancellationToken);
        return OperationResult<bool>.Ok(true, "Loan application archived.");
    }

    public async Task<OperationResult<IReadOnlyList<EmiScheduleResponse>>> GetEmiScheduleAsync(int id, CancellationToken cancellationToken)
    {
        var loan = await repository.GetLoanAsync(id, cancellationToken);
        if (loan is null) return OperationResult<IReadOnlyList<EmiScheduleResponse>>.NotFound("Loan application was not found.");

        var schedule = await repository.GetEmiScheduleAsync(id, cancellationToken);
        return OperationResult<IReadOnlyList<EmiScheduleResponse>>.Ok(schedule.Select(ToResponse).ToArray());
    }

    public Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken) => repository.GetDashboardSummaryAsync(cancellationToken);

    private static CustomerResponse ToResponse(Customer customer) => new(customer.Id, customer.FullName, customer.Phone, customer.Email, customer.City, customer.Status, customer.CreatedAt, customer.LoanApplications.Count, Convert.ToBase64String(customer.RowVersion));
    private static LoanResponse ToResponse(LoanApplication loan) => new(loan.Id, loan.ApplicationNumber, loan.CustomerId, loan.Customer?.FullName ?? string.Empty, loan.Product, loan.RequestedAmount, loan.TenureMonths, loan.InterestRate, loan.IsFlatInterest, loan.Status, loan.AppliedAt, Convert.ToBase64String(loan.RowVersion));
    private static byte[] ParseRowVersion(string? rowVersion) { try { return Convert.FromBase64String(rowVersion ?? string.Empty); } catch (FormatException) { throw new DomainValidationException("The record version is invalid. Reload and try again."); } }
    private static EmiScheduleResponse ToResponse(EmiSchedule schedule) => new(schedule.Id, schedule.LoanApplicationId, schedule.InstallmentNumber, schedule.DueDate, schedule.PrincipalAmount, schedule.InterestAmount, schedule.EmiAmount, schedule.RemainingBalance, schedule.Status);

    private static bool CanTransition(string current, string next) => current == next || current switch
    {
        LoanStatuses.UnderReview => next is LoanStatuses.DocumentsNeeded or LoanStatuses.Approved or LoanStatuses.Rejected,
        LoanStatuses.DocumentsNeeded => next is LoanStatuses.UnderReview or LoanStatuses.Approved or LoanStatuses.Rejected,
        LoanStatuses.Approved => next == LoanStatuses.Rejected,
        _ => false
    };
}
