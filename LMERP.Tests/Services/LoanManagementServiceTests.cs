using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Application.Services;
using LMERP.Domain.Entities;
using LMERP.Domain;
using Xunit;

namespace LMERP.Tests.Services;

public sealed class LoanManagementServiceTests
{
    private static LoanManagementService CreateService(ILoanManagementRepository repository) => new(repository, new FakeLoanDocumentService());

    [Fact]
    public async Task DeleteLoanAsync_WithoutScheduleOrPayments_SoftDeletesLoan()
    {
        var repository = new FakeLoanManagementRepository { Loan = new LoanApplication { Id = 10 } };
        var result = await CreateService(repository).DeleteLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteLoanAsync_WithEmiSchedule_ReturnsConflictAndDoesNotDelete()
    {
        var repository = new FakeLoanManagementRepository { Loan = new LoanApplication { Id = 10 }, HasSchedule = true };
        var result = await CreateService(repository).DeleteLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("Loans with a generated EMI schedule cannot be deleted.", result.Message);
        Assert.False(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteLoanAsync_WithPayments_ReturnsStrongerConflictAndDoesNotDelete()
    {
        var repository = new FakeLoanManagementRepository { Loan = new LoanApplication { Id = 10 }, HasSchedule = true, HasPayments = true };
        var result = await CreateService(repository).DeleteLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("Loans with recorded payments cannot be deleted.", result.Message);
        Assert.False(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task DeleteLoanAsync_UnknownLoan_ReturnsNotFound()
    {
        var repository = new FakeLoanManagementRepository();
        var result = await CreateService(repository).DeleteLoanAsync(404, CancellationToken.None);

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.False(repository.SoftDeleteCalled);
    }

    [Fact]
    public async Task CreateLoanAsync_UsesSequenceNumberInApplicationFormat()
    {
        var repository = new FakeLoanManagementRepository { ActiveCustomer = new Customer { Id = 3, FullName = "Test" }, NextNumber = 1000 };
        var result = await CreateService(repository).CreateLoanAsync(new LoanRequest { CustomerId = 3, Product = "Personal loan", RequestedAmount = 10000, TenureMonths = 12, InterestRate = 12 }, CancellationToken.None);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal($"LN-{DateTime.UtcNow:yyyy}-001000", result.Value!.ApplicationNumber);
    }

    [Fact]
    public async Task CreateLoanAsync_UnknownProduct_ReturnsBadRequest()
    {
        var repository = new FakeLoanManagementRepository { ActiveCustomer = new Customer { Id = 3 }, ActiveProducts = [] };
        var result = await CreateService(repository).CreateLoanAsync(new LoanRequest { CustomerId = 3, Product = "Mystery loan", RequestedAmount = 10000, TenureMonths = 12, InterestRate = 12 }, CancellationToken.None);
        Assert.Equal(OperationStatus.BadRequest, result.Status);
        Assert.Equal("Select an active loan product.", result.Message);
    }

    [Fact]
    public async Task CreateLoanAsync_InactiveProduct_ReturnsBadRequest()
    {
        var repository = new FakeLoanManagementRepository { ActiveCustomer = new Customer { Id = 3 }, ActiveProducts = [] };
        var result = await CreateService(repository).CreateLoanAsync(new LoanRequest { CustomerId = 3, Product = "Personal loan", RequestedAmount = 10000, TenureMonths = 12, InterestRate = 12 }, CancellationToken.None);
        Assert.Equal(OperationStatus.BadRequest, result.Status);
    }

    [Theory]
    [InlineData(LoanStatuses.UnderReview, LoanStatuses.UnderReview)]
    [InlineData(LoanStatuses.UnderReview, LoanStatuses.DocumentsNeeded)]
    [InlineData(LoanStatuses.UnderReview, LoanStatuses.Approved)]
    [InlineData(LoanStatuses.UnderReview, LoanStatuses.Rejected)]
    [InlineData(LoanStatuses.DocumentsNeeded, LoanStatuses.UnderReview)]
    [InlineData(LoanStatuses.DocumentsNeeded, LoanStatuses.DocumentsNeeded)]
    [InlineData(LoanStatuses.DocumentsNeeded, LoanStatuses.Approved)]
    [InlineData(LoanStatuses.DocumentsNeeded, LoanStatuses.Rejected)]
    [InlineData(LoanStatuses.Approved, LoanStatuses.Approved)]
    [InlineData(LoanStatuses.Approved, LoanStatuses.Rejected)]
    public async Task UpdateLoanAsync_AllowsSpecifiedTransitions(string current, string next)
    {
        var repository = new FakeLoanManagementRepository { Loan = NewLoan(current) };
        var result = await CreateService(repository).UpdateLoanAsync(10, UpdateRequest(next), CancellationToken.None);
        Assert.Equal(OperationStatus.Success, result.Status);
    }

    [Theory]
    [InlineData(LoanStatuses.Approved, LoanStatuses.DocumentsNeeded)]
    [InlineData(LoanStatuses.Rejected, LoanStatuses.UnderReview)]
    [InlineData(LoanStatuses.Closed, LoanStatuses.Rejected)]
    [InlineData(LoanStatuses.Disbursed, LoanStatuses.Rejected)]
    public async Task UpdateLoanAsync_RejectsForbiddenTransitions(string current, string next)
    {
        var repository = new FakeLoanManagementRepository { Loan = NewLoan(current) };
        var result = await CreateService(repository).UpdateLoanAsync(10, UpdateRequest(next), CancellationToken.None);
        Assert.Equal(OperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task DisburseLoanAsync_GeneratesOneScheduleRowPerTenureMonth()
    {
        var repository = new FakeLoanManagementRepository { Loan = NewLoan(LoanStatuses.Approved) };
        var result = await CreateService(repository).DisburseLoanAsync(10, CancellationToken.None);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(LoanStatuses.Disbursed, repository.Loan!.Status);
        Assert.NotNull(repository.Loan.DisbursedAt);
        Assert.Equal(6, repository.ScheduleRows.Count);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(repository.InitialAdvicePdf));
    }

    [Fact]
    public async Task DisburseLoanAsync_DeductsConfiguredFeesButBuildsEmiOnPrincipal()
    {
        var repository = new FakeLoanManagementRepository { Loan = NewLoan(LoanStatuses.Approved) };
        var service = new LoanManagementService(repository, new FakeLoanDocumentService(), null, new FakeDisbursementSettings(5m, 2m));

        var result = await service.DisburseLoanAsync(10, CancellationToken.None);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(1200m, repository.Loan!.RequestedAmount);
        Assert.Equal(60m, repository.Loan.ProcessingFeeAmount);
        Assert.Equal(24m, repository.Loan.PlatformFeeAmount);
        Assert.Equal(1116m, repository.Loan.ActualDisbursedAmount);
        Assert.Equal(1200m, repository.ScheduleRows.Sum(row => row.PrincipalAmount));
    }

    [Fact]
    public async Task DisburseLoanAsync_CannotDisburseTwice()
    {
        var repository = new FakeLoanManagementRepository { Loan = NewLoan(LoanStatuses.Approved) };
        var service = CreateService(repository);
        Assert.Equal(OperationStatus.Success, (await service.DisburseLoanAsync(10, CancellationToken.None)).Status);
        Assert.Equal(OperationStatus.Conflict, (await service.DisburseLoanAsync(10, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task RejectLoanAsync_RequiresReason()
    {
        var result = await CreateService(new FakeLoanManagementRepository { Loan = NewLoan(LoanStatuses.UnderReview) })
            .RejectLoanAsync(10, "bad", CancellationToken.None);
        Assert.Equal(OperationStatus.BadRequest, result.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EmiScheduleBuilder_ClosesPrincipalAndClampsMonthEnd(bool isFlat)
    {
        var loan = NewLoan(LoanStatuses.Approved);
        loan.IsFlatInterest = isFlat;
        var schedule = LoanEmiScheduleBuilder.Build(loan, new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), null);
        Assert.Equal(6, schedule.Count);
        Assert.Equal(loan.RequestedAmount, schedule.Sum(item => item.PrincipalAmount));
        Assert.Equal(0m, schedule[^1].RemainingBalance);
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), schedule[0].DueDate);
    }

    private static LoanApplication NewLoan(string status) => new() { Id = 10, Product = "Personal loan", RequestedAmount = 1200m, TenureMonths = 6, InterestRate = 12m, Status = status };
    private static LoanUpdateRequest UpdateRequest(string status) => new() { Product = "Personal loan", RequestedAmount = 1200m, TenureMonths = 6, InterestRate = 12m, Status = status, RowVersion = string.Empty };

    private sealed class FakeLoanManagementRepository : ILoanManagementRepository
    {
        public LoanApplication? Loan { get; init; }
        public bool HasSchedule { get; init; }
        public bool HasPayments { get; init; }
        public bool SoftDeleteCalled { get; private set; }
        public Customer? ActiveCustomer { get; init; }
        public long NextNumber { get; init; } = 1000;
        public HashSet<string> ActiveProducts { get; init; } = ["Personal loan"];
        public IReadOnlyList<EmiSchedule> ScheduleRows { get; private set; } = [];
        public byte[] InitialAdvicePdf { get; private set; } = [];

        public Task<LoanApplication?> GetLoanForUpdateAsync(int id, CancellationToken cancellationToken) => Task.FromResult(Loan?.Id == id ? Loan : null);
        public Task<bool> LoanHasPaymentsAsync(int loanId, CancellationToken cancellationToken) => Task.FromResult(HasPayments);
        public Task<bool> HasUnpaidEmiSchedulesAsync(int loanId, CancellationToken cancellationToken) => Task.FromResult(HasSchedule);
        public Task<bool> HasEmiScheduleAsync(int loanId, CancellationToken cancellationToken) => Task.FromResult(HasSchedule || ScheduleRows.Count > 0);
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
        public Task<Customer?> GetActiveCustomerAsync(int id, CancellationToken cancellationToken) => Task.FromResult(ActiveCustomer?.Id == id ? ActiveCustomer : null);
        public Task AddLoanAsync(LoanApplication loan, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<long> GetNextApplicationNumberAsync(CancellationToken cancellationToken) => Task.FromResult(NextNumber);
        public Task UpdateLoanAsync(LoanApplication loan, CancellationToken cancellationToken) => Task.CompletedTask;
        public async Task UpdateLoanAndAddEmiScheduleAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedule, ILoanDocumentService documentService, CancellationToken cancellationToken)
        {
            ScheduleRows = schedule.ToArray();
            InitialAdvicePdf = await documentService.GenerateDisbursementAdviceAsync(loan, schedule, cancellationToken);
        }
        public Task<LoanProduct?> GetLoanProductAsync(string name, CancellationToken cancellationToken) => Task.FromResult(ActiveProducts.Contains(name) ? new LoanProduct { Name = name, IsActive = true } : null);
        public Task<IReadOnlyList<EmiSchedule>> GetEmiScheduleAsync(int loanId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<DashboardTransactionResponse>> GetRecentDashboardTransactionsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeLoanDocumentService : ILoanDocumentService
    {
        public Task<byte[]> GenerateDisbursementAdviceAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedules, CancellationToken ct) =>
            Task.FromResult(System.Text.Encoding.ASCII.GetBytes("%PDF-fake"));
        public Task<LoanPdfFile?> GetDisbursementAdviceAsync(int loanId, CancellationToken ct) => Task.FromResult<LoanPdfFile?>(null);
        public Task<LoanPdfFile> GenerateLatestEmiStatementAsync(int loanId, CancellationToken ct) => throw new NotImplementedException();
        public Task<CompanyDocumentSettingsResponse> GetCompanySettingsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<CompanyDocumentSettingsResponse> UpdateCompanySettingsAsync(CompanyDocumentSettingsRequest request, CompanyDocumentAssetUpload? logo, CompanyDocumentAssetUpload? signature, CompanyDocumentAssetUpload? stamp, CancellationToken ct) => throw new NotImplementedException();
        public Task<CompanyDocumentAsset?> GetCompanyAssetAsync(string assetName, CancellationToken ct) => Task.FromResult<CompanyDocumentAsset?>(null);
    }

    private sealed class FakeDisbursementSettings(decimal processing, decimal platform) : ILoanDisbursementSettingsService
    {
        public Task<LoanDisbursementSettingsResponse> GetAsync(CancellationToken cancellationToken) => Task.FromResult(new LoanDisbursementSettingsResponse(processing, platform, DateTimeOffset.UtcNow, null));
        public Task<LoanDisbursementSettingsResponse> UpdateAsync(LoanDisbursementSettingsRequest request, string updatedBy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
