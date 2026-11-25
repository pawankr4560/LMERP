using LMERP.Application.Contracts;
using LMERP.Application.Abstractions;
using LMERP.Application.Services;
using LMERP.Application.Exceptions;
using LMERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace LMERP.Infrastructure.Persistence;

public sealed class EmiRepaymentService(LoanDbContext db, IBusinessClock businessClock) : IEmiRepaymentService
{
    public async Task<IReadOnlyList<OverdueEmiResponse>> GetOverdueAsync(CancellationToken ct)
    {
        var today = BusinessToday();
        var schedules = await db.EmiSchedules.AsNoTracking().Include(s => s.LoanApplication).ThenInclude(l => l!.Customer)
            .Include(s => s.LateFees).Include(s => s.Payments).ThenInclude(p => p.Allocations)
            .Where(s => s.Status != "Paid")
            .OrderBy(s => s.DueDate).ToListAsync(ct);
        return schedules.Where(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.DueDate, BusinessTimeZone()).DateTime) < today)
            .Select(ToResponse).Where(x => x.TotalOutstanding > 0).ToArray();
    }

    public async Task<OverdueEmiResponse?> GetDetailsAsync(int scheduleId, CancellationToken ct)
    {
        var schedule = await LoadSchedule(scheduleId, ct);
        return schedule is null ? null : ToResponse(schedule);
    }

    public async Task<IReadOnlyList<EmiPaymentResponse>> GetPaymentsAsync(int scheduleId, CancellationToken ct)
    {
        if (!await db.EmiSchedules.AnyAsync(schedule => schedule.Id == scheduleId, ct))
            throw new NotFoundException("EMI schedule was not found.");
        var payments = await db.EmiPayments.AsNoTracking().Include(payment => payment.Allocations)
            .Where(payment => payment.EmiScheduleId == scheduleId).OrderByDescending(payment => payment.ReceivedAt).ToListAsync(ct);
        return payments.Select(ToPaymentResponse).ToArray();
    }

    public async Task<RepaymentSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var items = await GetOverdueAsync(ct);
        return new RepaymentSummaryResponse(items.Count, items.Sum(x => x.EmiOutstanding), items.Sum(x => x.LateFeesOutstanding), items.Sum(x => x.TotalOutstanding));
    }

    public async Task<IReadOnlyList<LoanProductResponse>> GetProductsAsync(CancellationToken ct) =>
        (await db.LoanProducts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct)).Select(ToResponse).ToArray();

    public async Task<LoanProductResponse> SaveProductAsync(int? id, LoanProductLateFeeRequest request, CancellationToken ct)
    {
        if (request.IsLateFeeApplicable && request.LateFeeRate <= 0) throw new DomainValidationException("A positive late fee rate is required when late fees are enabled.");
        if (request.IsLateFeeApplicable && request.MaximumLateFeeAmount is null && request.MaximumPenaltyDays is null) throw new DomainValidationException("Configure a maximum fee amount or maximum penalty days before enabling late fees.");
        var name = request.Name.Trim();
        var product = id.HasValue ? await db.LoanProducts.FindAsync([id.Value], ct) : null;
        if (id.HasValue && product is null) throw new NotFoundException("Loan product was not found.");
        if (product is not null && product.Name != name && await db.LoanApplications.AnyAsync(loan => loan.Product == product.Name, ct))
            throw new ConflictException("This product name is already referenced by loan applications and cannot be changed.");
        if (await db.LoanProducts.AnyAsync(x => x.Name == name && (!id.HasValue || x.Id != id.Value), ct)) throw new ConflictException("A loan product with this name already exists.");
        product ??= new LoanProduct();
        product.Name = name; product.IsActive = request.IsActive; product.IsLateFeeApplicable = request.IsLateFeeApplicable;
        product.LateFeeRate = request.LateFeeRate; product.LateFeeCalculationType = request.LateFeeCalculationType;
        product.GracePeriodDays = request.GracePeriodDays; product.MaximumLateFeeAmount = request.MaximumLateFeeAmount; product.MaximumPenaltyDays = request.MaximumPenaltyDays;
        if (product.Id == 0) db.LoanProducts.Add(product);
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<EmiPaymentResponse> CollectPaymentAsync(int scheduleId, EmiPaymentRequest request, string actor, string? idempotencyKey, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (idempotencyKey is not null)
        {
            var replay = await db.EmiPayments.Include(p => p.Allocations).SingleOrDefaultAsync(p => p.EmiScheduleId == scheduleId && p.IdempotencyKey == idempotencyKey, ct);
            if (replay is not null) return ToPaymentResponse(replay);
        }
        var schedule = await LoadSchedule(scheduleId, ct) ?? throw new NotFoundException("EMI schedule was not found.");
        var outstandingEmi = EmiOutstanding(schedule);
        var fees = schedule.LateFees.Where(f => f.Status == "Pending").OrderBy(f => f.PenaltyDate).ToList();
        var outstandingFees = fees.ToDictionary(f => f, f => Math.Max(0m, f.PenaltyAmount - f.PaymentAllocations.Sum(a => a.Amount)));
        var totalOutstanding = outstandingEmi + outstandingFees.Values.Sum();
        if (request.Amount > totalOutstanding) throw new DomainValidationException("Payment cannot exceed the EMI and late fee outstanding balance.");

        var payment = new EmiPayment { EmiScheduleId = scheduleId, Amount = request.Amount, Method = request.Method.Trim(), Reference = request.Reference?.Trim(), CollectedBy = actor, IdempotencyKey = idempotencyKey };
        var allocations = EmiPaymentAllocator.Allocate(request.Amount, outstandingEmi, fees.Select(f => new FeeBalance(f.Id, f.PenaltyDate, outstandingFees[f])));
        foreach (var allocation in allocations)
        {
            var fee = allocation.LateFeeId is null ? null : fees.Single(f => f.Id == allocation.LateFeeId.Value);
            payment.Allocations.Add(new EmiPaymentAllocation { EmiScheduleId = scheduleId, EmiLateFeeId = fee?.Id, EmiLateFee = fee, Component = allocation.Component, Amount = allocation.Amount });
        }
        var emiAllocation = allocations.Where(a => a.Component == "Emi").Sum(a => a.Amount);
        db.EmiPayments.Add(payment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (idempotencyKey is not null && exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            db.ChangeTracker.Clear();
            var replay = await db.EmiPayments.Include(p => p.Allocations).SingleOrDefaultAsync(p => p.EmiScheduleId == scheduleId && p.IdempotencyKey == idempotencyKey, ct);
            if (replay is not null) return ToPaymentResponse(replay);
            throw;
        }
        foreach (var fee in fees)
            if (outstandingFees[fee] - payment.Allocations.Where(a => a.EmiLateFeeId == fee.Id).Sum(a => a.Amount) <= 0) fee.Status = "Paid";
        var remainingFeeBalance = fees.Sum(f => Math.Max(0m, outstandingFees[f] - payment.Allocations.Where(a => a.EmiLateFeeId == f.Id).Sum(a => a.Amount)));
        schedule.Status = outstandingEmi - emiAllocation <= 0 && remainingFeeBalance <= 0 ? "Paid" : outstandingEmi - emiAllocation < schedule.EmiAmount ? "Partially paid" : schedule.Status;
        await db.SaveChangesAsync(ct);
        await CloseLoanWhenFullyPaidAsync(schedule.LoanApplicationId, ct);
        await transaction.CommitAsync(ct);
        return ToPaymentResponse(payment);
    }

    public async Task WaiveFeeAsync(int feeId, string reason, string actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var fee = await db.EmiLateFees.Include(f => f.PaymentAllocations).SingleOrDefaultAsync(f => f.Id == feeId, ct) ?? throw new NotFoundException("Late fee was not found.");
        var outstanding = fee.PenaltyAmount - fee.PaymentAllocations.Sum(a => a.Amount);
        if (outstanding <= 0) throw new DomainValidationException("This late fee is already fully paid.");
        fee.Status = "Waived"; fee.WaivedAmount = outstanding; fee.WaivedAt = DateTimeOffset.UtcNow; fee.WaiverReason = reason.Trim(); fee.WaivedBy = actor;
        var schedule = await db.EmiSchedules.Include(s => s.Payments).ThenInclude(p => p.Allocations).Include(s => s.LateFees).ThenInclude(f => f.PaymentAllocations).SingleAsync(s => s.Id == fee.EmiScheduleId, ct);
        var emiOutstanding = EmiOutstanding(schedule);
        var feesOutstanding = schedule.LateFees.Where(f => f.Status == "Pending" && f.Id != fee.Id).Sum(f => Math.Max(0m, f.PenaltyAmount - f.PaymentAllocations.Sum(a => a.Amount)));
        if (emiOutstanding <= 0 && feesOutstanding <= 0) schedule.Status = "Paid";
        await db.SaveChangesAsync(ct);
        await CloseLoanWhenFullyPaidAsync(schedule.LoanApplicationId, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ExemptScheduleAsync(int scheduleId, string reason, string actor, CancellationToken ct)
    {
        var schedule = await db.EmiSchedules.SingleOrDefaultAsync(s => s.Id == scheduleId, ct) ?? throw new NotFoundException("EMI schedule was not found.");
        if (schedule.IsLateFeeExempt) throw new ConflictException("This EMI schedule is already exempt from late fees.");
        schedule.IsLateFeeExempt = true;
        schedule.LateFeeExemptedAt = DateTimeOffset.UtcNow;
        schedule.LateFeeExemptedBy = actor;
        schedule.LateFeeExemptionReason = reason.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> ProcessDailyFeesAsync(CancellationToken ct)
    {
        var today = BusinessToday();
        var schedules = await db.EmiSchedules.Include(s => s.LoanApplication).Include(s => s.LateFees).ThenInclude(f => f.PaymentAllocations)
            .Where(s => s.Status != "Paid").ToListAsync(ct);
        var created = 0;
        foreach (var schedule in schedules)
        {
            var outstanding = await EmiOutstandingAsync(schedule.Id, ct);
            if (outstanding <= 0)
            {
                var openFees = schedule.LateFees.Sum(f => RepaymentBalances.OpenLateFee(f.PenaltyAmount, f.PaymentAllocations.Sum(a => a.Amount), f.Status));
                schedule.Status = openFees == 0 ? "Paid" : "Partially paid";
                continue;
            }
            var dueDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(schedule.DueDate, BusinessTimeZone()).DateTime);
            if (dueDate < today) schedule.Status = "Overdue";
            if (schedule.IsLateFeeExempt || !schedule.IsLateFeeApplicable || schedule.LateFeeRate <= 0 || dueDate >= today) continue;
            var existing = (await db.EmiLateFees.Where(f => f.EmiScheduleId == schedule.Id).ToListAsync(ct)).ToDictionary(f => f.PenaltyDate, f => f.PenaltyAmount);
            var feesToAdd = LateFeeCalculator.Calculate(schedule.EmiAmount, schedule.LateFeeRate, dueDate, schedule.GracePeriodDays,
                today, outstanding, schedule.IsLateFeeExempt, schedule.MaximumLateFeeAmount, schedule.MaximumPenaltyDays, existing);
            db.EmiLateFees.AddRange(feesToAdd.Select(fee => new EmiLateFee { EmiScheduleId = schedule.Id, PenaltyDate = fee.PenaltyDate, BaseAmount = fee.BaseAmount, PenaltyRate = fee.Rate, PenaltyAmount = fee.Amount }));
            created += feesToAdd.Count;
        }
        await db.SaveChangesAsync(ct);
        return created;
    }

    private async Task<EmiSchedule?> LoadSchedule(int id, CancellationToken ct) => await db.EmiSchedules.Include(s => s.LoanApplication).ThenInclude(l => l!.Customer).Include(s => s.LateFees).ThenInclude(f => f.PaymentAllocations).Include(s => s.Payments).ThenInclude(p => p.Allocations).SingleOrDefaultAsync(s => s.Id == id, ct);
    private async Task<decimal> EmiOutstandingAsync(int scheduleId, CancellationToken ct)
    {
        var balance = await db.EmiSchedules.Where(s => s.Id == scheduleId)
            .Select(s => s.EmiAmount - db.EmiPaymentAllocations.Where(a => a.EmiScheduleId == scheduleId && a.Component == "Emi").Sum(a => (decimal?)a.Amount).GetValueOrDefault())
            .SingleAsync(ct);
        return Math.Max(0m, balance);
    }
    private static decimal EmiOutstanding(EmiSchedule s) => Math.Max(0m, s.EmiAmount - s.Payments.SelectMany(p => p.Allocations).Where(a => a.Component == "Emi").Sum(a => a.Amount));
    private static EmiPaymentResponse ToPaymentResponse(EmiPayment payment) => new(payment.Id, payment.EmiScheduleId, payment.Amount, payment.ReceivedAt, payment.Method, payment.Reference, payment.Allocations.Select(a => new PaymentAllocationResponse(a.Component, a.Amount, a.EmiLateFeeId)).ToArray());
    private OverdueEmiResponse ToResponse(EmiSchedule s)
    {
        var fees = s.LateFees.OrderBy(f => f.PenaltyDate).Select(f => new LateFeeResponse(f.Id, f.PenaltyDate, f.BaseAmount, f.PenaltyRate, f.PenaltyAmount, f.Status, f.CreatedAt, f.WaiverReason)).ToArray();
        var emiOutstanding = EmiOutstanding(s);
        var feesOutstanding = s.LateFees.Sum(f => RepaymentBalances.OpenLateFee(f.PenaltyAmount, s.Payments.SelectMany(p => p.Allocations).Where(a => a.EmiLateFeeId == f.Id).Sum(a => a.Amount), f.Status));
        var payments = s.Payments.OrderByDescending(p => p.ReceivedAt).Select(p => new EmiPaymentResponse(p.Id, s.Id, p.Amount, p.ReceivedAt, p.Method, p.Reference, p.Allocations.Select(a => new PaymentAllocationResponse(a.Component, a.Amount, a.EmiLateFeeId)).ToArray())).ToArray();
        return new OverdueEmiResponse(s.Id, s.LoanApplicationId, s.LoanApplication?.ApplicationNumber ?? "", s.LoanApplication?.Customer?.FullName ?? "", s.InstallmentNumber, s.DueDate, s.EmiAmount, emiOutstanding, feesOutstanding, emiOutstanding + feesOutstanding, s.Status, fees, payments);
    }
    private static LoanProductResponse ToResponse(LoanProduct p) => new(p.Id, p.Name, p.IsActive, p.IsLateFeeApplicable, p.LateFeeRate, p.LateFeeCalculationType, p.GracePeriodDays, p.MaximumLateFeeAmount, p.MaximumPenaltyDays);
    private TimeZoneInfo BusinessTimeZone() => businessClock.TimeZone;
    private DateOnly BusinessToday() => businessClock.Today;

    private async Task CloseLoanWhenFullyPaidAsync(int loanId, CancellationToken ct)
    {
        var hasUnpaidSchedules = await db.EmiSchedules.AnyAsync(schedule => schedule.LoanApplicationId == loanId && schedule.Status != "Paid", ct);
        if (hasUnpaidSchedules) return;
        var loan = await db.LoanApplications.SingleOrDefaultAsync(item => item.Id == loanId, ct);
        if (loan is null || loan.Status != LMERP.Domain.LoanStatuses.Disbursed) return;
        loan.Status = LMERP.Domain.LoanStatuses.Closed;
        loan.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
