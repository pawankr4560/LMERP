using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Domain.Entities;
using LMERP.Application.Exceptions;

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
            TenureMonths = request.TenureMonths, InterestRate = request.InterestRate, IsFlatInterest = request.IsFlatInterest, Status = "Under review"
        };
        await repository.AddLoanAsync(loan, cancellationToken);
        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan application created.");
    }

    public async Task<OperationResult<LoanResponse>> UpdateLoanAsync(int id, LoanUpdateRequest request, CancellationToken cancellationToken)
    {
        var loan = await repository.GetLoanForUpdateAsync(id, cancellationToken);
        if (loan is null) return OperationResult<LoanResponse>.NotFound("Loan application was not found.");

        var status = request.Status.Trim();
        if (status is not ("Under review" or "Documents needed" or "Approved"))
            return OperationResult<LoanResponse>.BadRequest("Status must be Under review, Documents needed, or Approved.");

        var hasEmiSchedule = await repository.HasEmiScheduleAsync(id, cancellationToken);
        loan.RowVersion = ParseRowVersion(request.RowVersion);
        var normalizedProduct = request.Product.Trim();
        if (loan.Product != normalizedProduct || status == "Approved" && loan.Status != "Approved")
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
            if (loan.Status == "Approved" && status != "Approved")
                return OperationResult<LoanResponse>.Conflict("An approved loan with an EMI schedule cannot be moved back to another status.");
        }

        loan.Product = normalizedProduct;
        loan.RequestedAmount = request.RequestedAmount;
        loan.TenureMonths = request.TenureMonths;
        loan.InterestRate = request.InterestRate;
        loan.IsFlatInterest = request.IsFlatInterest;
        loan.Status = status;
        if (status == "Approved" && !hasEmiSchedule)
        {
            var product = await repository.GetLoanProductAsync(loan.Product, cancellationToken);
            var schedule = BuildEmiSchedule(loan, DateTimeOffset.UtcNow, product);
            await repository.UpdateLoanAndAddEmiScheduleAsync(loan, schedule, cancellationToken);
        }
        else
        {
            await repository.UpdateLoanAsync(loan, cancellationToken);
        }

        return OperationResult<LoanResponse>.Ok(ToResponse(loan), "Loan application updated.");
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

    private static IReadOnlyList<EmiSchedule> BuildEmiSchedule(LoanApplication loan, DateTimeOffset approvedAtUtc, LoanProduct? product)
    {
        var principal = loan.RequestedAmount;
        var monthlyRate = loan.InterestRate / 1200m;
        var months = loan.TenureMonths;
        var flatInterestTotal = Round(principal * loan.InterestRate / 100m * months / 12m);
        var flatInterestPerInstallment = Round(flatInterestTotal / months);
        var flatPrincipalPerInstallment = Round(principal / months);
        var reducingEmi = CalculateReducingEmi(principal, monthlyRate, months);
        var remainingBalance = principal;
        var interestCharged = 0m;
        var schedule = new List<EmiSchedule>(months);

        for (var installmentNumber = 1; installmentNumber <= months; installmentNumber++)
        {
            var isFinalInstallment = installmentNumber == months;
            decimal principalAmount;
            decimal interestAmount;

            if (loan.IsFlatInterest)
            {
                principalAmount = isFinalInstallment
                    ? remainingBalance
                    : Math.Min(remainingBalance, flatPrincipalPerInstallment);
                interestAmount = isFinalInstallment
                    ? flatInterestTotal - interestCharged
                    : flatInterestPerInstallment;
            }
            else
            {
                interestAmount = Round(remainingBalance * monthlyRate);
                principalAmount = isFinalInstallment
                    ? remainingBalance
                    : Math.Min(remainingBalance, Round(reducingEmi - interestAmount));
            }

            principalAmount = Round(principalAmount);
            interestAmount = Round(interestAmount);
            remainingBalance = Math.Max(0m, Round(remainingBalance - principalAmount));
            interestCharged += interestAmount;

            schedule.Add(new EmiSchedule
            {
                LoanApplicationId = loan.Id,
                InstallmentNumber = installmentNumber,
                DueDate = approvedAtUtc.AddMonths(installmentNumber),
                PrincipalAmount = principalAmount,
                InterestAmount = interestAmount,
                EmiAmount = Round(principalAmount + interestAmount),
                RemainingBalance = remainingBalance,
                Status = "Pending",
                IsLateFeeApplicable = product?.IsLateFeeApplicable ?? false,
                LateFeeRate = product?.LateFeeRate ?? 0m,
                LateFeeCalculationType = product?.LateFeeCalculationType ?? "DailyPercentageOfEmi",
                GracePeriodDays = product?.GracePeriodDays ?? 0,
                MaximumLateFeeAmount = product?.MaximumLateFeeAmount,
                MaximumPenaltyDays = product?.MaximumPenaltyDays
            });
        }

        return schedule;
    }

    private static decimal CalculateReducingEmi(decimal principal, decimal monthlyRate, int months)
    {
        if (monthlyRate == 0m) return Round(principal / months);
        var factor = (decimal)Math.Pow((double)(1m + monthlyRate), months);
        return Round(principal * monthlyRate * factor / (factor - 1m));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
