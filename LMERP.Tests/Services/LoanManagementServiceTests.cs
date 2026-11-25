using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Domain.Entities;
using Xunit;

namespace LMERP.Tests.Services;

public sealed class LoanManagementServiceTests
{
    [Fact]
    public async Task DeleteLoanAsync_WithoutScheduleOrPayments_SoftDeletesLoan()
    {
        var repository = new FakeLoanManagementRepository { Loan = new LoanApplication { Id = 10 } };
        var result = await new LoanManagementService(repository).DeleteLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteLoanAsync_WithEmiSchedule_ReturnsConflictAndDoesNotDelete()
    {
        var repository = new FakeLoanManagementRepository { Loan = new LoanApplication { Id = 10 }, HasSchedule = true };
        var result = await new LoanManagementService(repository).DeleteLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("Loans with a generated EMI schedule cannot be deleted.", result.Message);
        Assert.False(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteLoanAsync_WithPayments_ReturnsStrongerConflictAndDoesNotDelete()
    {
        var repository = new FakeLoanManagementRepository { Loan = new LoanApplication { Id = 10 }, HasSchedule = true, HasPayments = true };
        var result = await new LoanManagementService(repository).DeleteLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("Loans with recorded payments cannot be deleted.", result.Message);
        Assert.False(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteLoanAsync_UnknownLoan_ReturnsNotFound()
    {
        var repository = new FakeLoanManagementRepository();
        var result = await new LoanManagementService(repository).DeleteLoanAsync(404, CancellationToken.None);

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.False(repository.SoftDeleteCalled);
    }

    private sealed class FakeLoanManagementRepository : ILoanManagementRepository
    {
        public LoanApplication? Loan { get; init; }
        public bool HasSchedule { get; init; }
        public bool HasPayments { get; init; }
        public bool SoftDeleteCalled { get; private set; }

        public Task<LoanApplication?> GetLoanForUpdateAsync(int id, CancellationToken cancellationToken) => Task.FromResult(Loan?.Id == id ? Loan : null);
        public Task<bool> LoanHasPaymentsAsync(int loanId, CancellationToken cancellationToken) => Task.FromResult(HasPayments);
        public Task<bool> HasEmiScheduleAsync(int loanId, CancellationToken cancellationToken) => Task.FromResult(HasSchedule);
        public Task SoftDeleteLoanAsync(LoanApplication loan, CancellationToken cancellationToken) { SoftDeleteCalled = true; return Task.CompletedTask; }

        public Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Customer?> GetCustomerAsync(int id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> PhoneExistsAsync(string phone, int? excludingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> CustomerHasLoansAsync(int id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteCustomerAsync(Customer customer, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<LoanApplication>> GetLoansAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<LoanApplication?> GetLoanAsync(int id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Customer?> GetActiveCustomerAsync(int id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddLoanAsync(LoanApplication loan, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateLoanAsync(LoanApplication loan, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateLoanAndAddEmiScheduleAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedule, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<LoanProduct?> GetLoanProductAsync(string name, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<EmiSchedule>> GetEmiScheduleAsync(int loanId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
