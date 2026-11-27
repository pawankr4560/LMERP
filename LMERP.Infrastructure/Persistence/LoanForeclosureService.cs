using LMERP.Application.Contracts;
using LMERP.Application.Abstractions;
using LMERP.Application.Exceptions;
using LMERP.Application.Services;
using LMERP.Domain;
using LMERP.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

public sealed class LoanForeclosureService(LoanDbContext db, IEmiRepaymentService repayments, IBusinessClock businessClock) : ILoanForeclosureService
{
    public async Task<ForeclosureSettingsResponse> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        return ToResponse(settings);
    }

    public async Task<ForeclosureSettingsResponse> UpdateSettingsAsync(ForeclosureSettingsRequest request, string actor, CancellationToken ct)
    {
        if (request.FeeCalculationType != "RemainingEmiTotal") throw new DomainValidationException("Only the remaining EMI total calculation basis is supported.");
        if (request.Charges is null && (request.ServiceFeePercentage < 0 || request.ServiceFeePercentage > 100)) throw new DomainValidationException("Foreclosure fee percentage must be between 0 and 100.");
        if (request.Charges is not null) ValidateChargeRules(request.Charges);
        var settings = await GetOrCreateSettingsAsync(ct);
        settings.IsForeclosureAllowed = request.IsForeclosureAllowed;
        settings.FeeCalculationType = request.FeeCalculationType;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedBy = actor;
        if (request.Charges is null)
        {
            var serviceFee = settings.ChargeRules.FirstOrDefault(x => x.IsServiceFee);
            if (serviceFee is null)
            {
                serviceFee = new LoanForeclosureChargeRule { SettingsId = settings.Id, Name = "Foreclosure service fee", IsServiceFee = true, CalculationType = "Percentage", CalculationBasis = "RemainingEmiTotal", SortOrder = 0 };
                settings.ChargeRules.Add(serviceFee);
            }
            serviceFee.IsEnabled = true;
            serviceFee.Percentage = request.ServiceFeePercentage;
            settings.ServiceFeePercentage = request.ServiceFeePercentage;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.LoanForeclosureChargeRules.RemoveRange(settings.ChargeRules);
            settings.ChargeRules.Clear();
            await db.SaveChangesAsync(ct);
            foreach (var rule in request.Charges)
            {
                settings.ChargeRules.Add(new LoanForeclosureChargeRule
                {
                    SettingsId = settings.Id, Name = rule.Name.Trim(), IsEnabled = rule.IsEnabled, IsTax = rule.IsTax,
                    IsServiceFee = rule.IsServiceFee, CalculationType = rule.CalculationType, CalculationBasis = rule.CalculationBasis,
                    Percentage = rule.Percentage, FixedAmount = rule.FixedAmount, SortOrder = rule.SortOrder
                });
            }
            settings.ServiceFeePercentage = settings.ChargeRules.SingleOrDefault(x => x.IsServiceFee)?.Percentage ?? 0m;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return ToResponse(settings);
    }

    public async Task<ForeclosureLoanResponse> GetCalculationAsync(string loanNumber, CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        if (!settings.IsForeclosureAllowed) throw new ConflictException("Loan foreclosure is currently disabled by an administrator.");
        var loan = await LoadLoanAsync(loanNumber, ct) ?? throw new NotFoundException("Loan number was not found.");
        EnsureEligible(loan);
        if (await db.LoanForeclosures.AnyAsync(x => x.LoanApplicationId == loan.Id && x.ForeclosureStatus != "Paid" && x.ForeclosureStatus != "Cancelled", ct))
            throw new ConflictException("An active foreclosure settlement already exists for this loan. Open its history record to collect the balance.");
        foreach (var schedule in loan.EmiSchedules.Where(s => s.Status != "Paid" && s.Status != "Foreclosed"))
            await repayments.GetDetailsAsync(schedule.Id, ct);
        loan = await LoadLoanAsync(loanNumber, ct) ?? throw new NotFoundException("Loan number was not found.");
        return Calculate(loan, settings);
    }

    public async Task<IReadOnlyList<ForeclosureHistoryResponse>> GetHistoryAsync(string? loanNumber, CancellationToken ct)
    {
        var query = db.LoanForeclosures.AsNoTracking().Include(x => x.Payments).Include(x => x.Charges).AsQueryable();
        if (!string.IsNullOrWhiteSpace(loanNumber)) query = query.Where(x => x.LoanApplication!.ApplicationNumber == loanNumber.Trim());
        var records = await query.OrderByDescending(x => x.ForeclosureDate).ToListAsync(ct);
        return records.Select(ToResponse).ToArray();
    }

    public async Task<ForeclosureHistoryResponse> ConfirmAsync(string loanNumber, ForeclosurePaymentRequest request, string actor, string? idempotencyKey, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var settings = await GetOrCreateSettingsAsync(ct);
        if (!settings.IsForeclosureAllowed) throw new ConflictException("Loan foreclosure is currently disabled by an administrator.");
        var loan = await LoadLoanAsync(loanNumber, ct) ?? throw new NotFoundException("Loan number was not found.");
        if (idempotencyKey is not null)
        {
            var replay = await db.LoanForeclosurePayments.Include(p => p.LoanForeclosure).ThenInclude(f => f!.Payments)
                .SingleOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);
            if (replay is not null) return ToResponse(replay.LoanForeclosure!);
        }
        EnsureEligible(loan);
        if (await db.LoanForeclosures.AnyAsync(x => x.LoanApplicationId == loan.Id && x.ForeclosureStatus != "Cancelled", ct))
            throw new ConflictException("A foreclosure record already exists for this loan.");
        foreach (var schedule in loan.EmiSchedules.Where(s => s.Status != "Paid" && s.Status != "Foreclosed")) await repayments.GetDetailsAsync(schedule.Id, ct);
        await db.Entry(loan).ReloadAsync(ct);
        loan = await LoadLoanAsync(loanNumber, ct) ?? throw new NotFoundException("Loan number was not found.");
        EnsureEligible(loan);
        var calculation = Calculate(loan, settings);
        if (calculation.TotalForeclosurePayable <= 0) throw new ConflictException("There is no outstanding balance to foreclose.");
        if (request.Amount > calculation.TotalForeclosurePayable) throw new DomainValidationException("Payment cannot exceed the foreclosure amount due.");
        var record = new LoanForeclosure
        {
            LoanApplicationId = loan.Id,
            ForeclosureNumber = $"FC-{DateTime.UtcNow:yyyyMMddHHmmss}-{loan.Id:D6}",
            ForeclosureDate = DateTimeOffset.UtcNow,
            OutstandingPrincipal = calculation.OutstandingPrincipal,
            OutstandingInterest = calculation.OutstandingInterest,
            OutstandingLateFees = calculation.ExistingOutstandingLateFees,
            RemainingEmiAmount = calculation.RemainingEmiAmount,
            ServiceFeePercentage = calculation.ServiceFeePercentage,
            ServiceFeeAmount = calculation.ServiceFeeAmount,
            OtherCharges = calculation.OtherCharges,
            Adjustments = calculation.Adjustments,
            TotalForeclosureAmount = calculation.TotalForeclosurePayable,
            PaymentMode = request.PaymentMode.Trim(),
            TransactionReference = request.TransactionReference?.Trim(),
            Remarks = request.Remarks?.Trim(),
            CreatedBy = actor
        };
        foreach (var charge in calculation.Charges)
        {
            record.Charges.Add(new LoanForeclosureCharge
            {
                Name = charge.Name, IsTax = charge.IsTax, IsServiceFee = charge.IsServiceFee, CalculationType = charge.CalculationType,
                CalculationBasis = charge.CalculationBasis, Percentage = charge.Percentage, FixedAmount = charge.FixedAmount,
                BaseAmount = charge.BaseAmount, Amount = charge.Amount
            });
        }
        db.LoanForeclosures.Add(record);
        await db.SaveChangesAsync(ct);
        AddPayment(record, request, actor, idempotencyKey);
        await SavePaymentAndFinalizeAsync(record, loan, request.Amount, ct);
        await tx.CommitAsync(ct);
        return ToResponse(record);
    }

    public async Task<ForeclosureHistoryResponse> CollectPaymentAsync(int foreclosureId, ForeclosurePaymentRequest request, string actor, string? idempotencyKey, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (idempotencyKey is not null)
        {
            var replay = await db.LoanForeclosurePayments.Include(p => p.LoanForeclosure).ThenInclude(f => f!.Payments).SingleOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);
            if (replay is not null) return ToResponse(replay.LoanForeclosure!);
        }
        var record = await db.LoanForeclosures.Include(f => f.Payments).Include(f => f.Charges).Include(f => f.LoanApplication).ThenInclude(l => l!.EmiSchedules)
            .SingleOrDefaultAsync(f => f.Id == foreclosureId, ct) ?? throw new NotFoundException("Foreclosure record was not found.");
        if (record.ForeclosureStatus == "Paid") throw new ConflictException("This foreclosure has already been fully paid.");
        var balance = Math.Max(0m, record.TotalForeclosureAmount - record.AmountPaid);
        if (request.Amount > balance) throw new DomainValidationException("Payment cannot exceed the remaining foreclosure balance.");
        AddPayment(record, request, actor, idempotencyKey);
        await SavePaymentAndFinalizeAsync(record, record.LoanApplication!, request.Amount, ct);
        await tx.CommitAsync(ct);
        return ToResponse(record);
    }

    private async Task SavePaymentAndFinalizeAsync(LoanForeclosure record, LoanApplication loan, decimal paymentAmount, CancellationToken ct)
    {
        record.AmountPaid += paymentAmount;
        record.PaymentStatus = record.AmountPaid >= record.TotalForeclosureAmount ? "Paid" : "Partially paid";
        record.ForeclosureStatus = record.PaymentStatus;
        if (record.PaymentStatus == "Paid")
        {
            loan.Status = LoanStatuses.Foreclosed;
            loan.ClosedAt = DateTimeOffset.UtcNow;
            foreach (var schedule in await db.EmiSchedules.Where(s => s.LoanApplicationId == loan.Id && s.Status != "Paid").ToListAsync(ct)) schedule.Status = "Foreclosed";
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ConflictException("This foreclosure payment was already recorded."); }
    }

    private static void AddPayment(LoanForeclosure record, ForeclosurePaymentRequest request, string actor, string? key)
    {
        var payment = new LoanForeclosurePayment { Amount = request.Amount, Method = request.PaymentMode.Trim(), Reference = request.TransactionReference?.Trim(), CollectedBy = actor, IdempotencyKey = key };
        record.Payments.Add(payment);
        record.PaymentMode = payment.Method;
        record.TransactionReference = payment.Reference ?? record.TransactionReference;
    }

    private ForeclosureLoanResponse Calculate(LoanApplication loan, LoanForeclosureSettings settings)
    {
        var schedules = loan.EmiSchedules.OrderBy(s => s.InstallmentNumber).ToArray();
        var paidBySchedule = schedules.ToDictionary(s => s.Id, s => s.Payments.SelectMany(p => p.Allocations).Where(a => a.Component == "Emi").Sum(a => a.Amount));
        var outstandingBySchedule = schedules.ToDictionary(s => s.Id, s => Math.Max(0m, s.EmiAmount - paidBySchedule[s.Id]));
        var remainingEmi = outstandingBySchedule.Values.Sum();
        var lateFees = schedules.SelectMany(s => s.LateFees).Where(f => f.Status == "Pending")
            .Sum(f => Math.Max(0m, f.PenaltyAmount - f.PaymentAllocations.Sum(a => a.Amount)));
        var calculationDate = businessClock.Today;
        var interestResult = ForeclosureInterestCalculator.Calculate(schedules.Sum(s => s.PrincipalAmount), loan.InterestRate,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(loan.DisbursedAt ?? loan.AppliedAt, businessClock.TimeZone).DateTime), calculationDate,
            schedules.SelectMany(schedule => schedule.Payments.Select(payment => new ForeclosureEmiPayment(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(payment.ReceivedAt, businessClock.TimeZone).DateTime),
                payment.Allocations.Where(allocation => allocation.Component == "Emi").Sum(allocation => allocation.Amount),
                schedule.PrincipalAmount, schedule.InterestAmount, schedule.EmiAmount))));
        var outstandingPrincipal = interestResult.OutstandingPrincipal;
        var accruedInterest = interestResult.AccruedInterest;
        var amountResult = ForeclosureAmountCalculator.CalculateWithCharges(outstandingPrincipal, accruedInterest, lateFees, remainingEmi,
            settings.ChargeRules.Select(rule => new ForeclosureChargeInput(rule.Name, rule.IsTax, rule.IsServiceFee, rule.IsEnabled,
                rule.CalculationType, rule.CalculationBasis, rule.Percentage, rule.FixedAmount, rule.SortOrder)));
        var serviceFeePercentage = settings.ChargeRules.FirstOrDefault(x => x.IsServiceFee)?.Percentage ?? 0m;
        var paidInstallments = schedules.Count(s => outstandingBySchedule[s.Id] == 0m);
        var totalEmi = schedules.Sum(s => s.EmiAmount);
        return new ForeclosureLoanResponse(loan.Id, loan.ApplicationNumber, loan.Customer?.FullName ?? string.Empty, loan.Customer?.Phone ?? string.Empty,
            loan.Product, loan.RequestedAmount, null, loan.ActualDisbursedAmount ?? loan.RequestedAmount, loan.InterestRate, loan.TenureMonths, loan.AppliedAt,
            loan.DisbursedAt, totalEmi, schedules.Length, paidInstallments, schedules.Length - paidInstallments,
            schedules.Sum(s => paidBySchedule[s.Id]), remainingEmi, outstandingPrincipal, accruedInterest, lateFees, serviceFeePercentage,
            amountResult.ServiceFeeAmount, amountResult.OtherCharges, 0m, amountResult.TotalPayable, DateTimeOffset.UtcNow,
            "Historical EMI payments are split pro rata using each installment’s scheduled principal and interest. Daily simple interest is accrued on outstanding principal at the annual loan rate divided by 365 from disbursement through this calculation date; interest portions already paid are deducted. Configured percentage and fixed foreclosure charges are shown individually and snapshotted when confirmed.",
            schedules.Select(s => new ForeclosureScheduleResponse(s.InstallmentNumber, s.DueDate, s.EmiAmount, s.PrincipalAmount, s.InterestAmount,
                paidBySchedule[s.Id], outstandingBySchedule[s.Id], s.Status)).ToArray(),
            schedules.SelectMany(s => s.Payments.Select(p => new ForeclosureExistingPaymentResponse(p.Id, s.InstallmentNumber, p.Amount,
                p.ReceivedAt, p.Method, p.Reference, p.Allocations.Select(a => new PaymentAllocationResponse(a.Component, a.Amount, a.EmiLateFeeId)).ToArray())))
                .OrderBy(p => p.ReceivedAt).ToArray(),
            schedules.SelectMany(s => s.LateFees.Select(f =>
            {
                var paid = f.PaymentAllocations.Sum(a => a.Amount);
                var outstanding = f.Status == "Pending" ? Math.Max(0m, f.PenaltyAmount - paid) : 0m;
                return new ForeclosureLateFeeResponse(f.Id, s.InstallmentNumber, f.PenaltyDate, f.BaseAmount, f.PenaltyRate, f.PenaltyAmount, paid, outstanding, f.Status, f.WaiverReason);
            })).OrderBy(f => f.PenaltyDate).ToArray(),
            amountResult.Charges.Select(ToResponse).ToArray());
    }

    private async Task<LoanApplication?> LoadLoanAsync(string number, CancellationToken ct) => await db.LoanApplications
        .Include(l => l.Customer).Include(l => l.EmiSchedules).ThenInclude(s => s.Payments).ThenInclude(p => p.Allocations)
        .Include(l => l.EmiSchedules).ThenInclude(s => s.LateFees).ThenInclude(f => f.PaymentAllocations)
        .SingleOrDefaultAsync(l => l.ApplicationNumber == number.Trim(), ct);

    private static void EnsureEligible(LoanApplication loan)
    {
        if (loan.Status != LoanStatuses.Disbursed) throw new ConflictException("Only active, disbursed loans can be foreclosed.");
        if (loan.EmiSchedules.Count == 0) throw new ConflictException("This loan does not have an EMI schedule.");
    }

    private async Task<LoanForeclosureSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await db.LoanForeclosureSettings.Include(s => s.ChargeRules).SingleOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is not null)
        {
            if (settings.ChargeRules.Count == 0)
            {
                settings.ChargeRules.Add(new LoanForeclosureChargeRule { SettingsId = settings.Id, Name = "Foreclosure service fee", IsServiceFee = true, CalculationType = "Percentage", CalculationBasis = settings.FeeCalculationType, Percentage = settings.ServiceFeePercentage, SortOrder = 0 });
                await db.SaveChangesAsync(ct);
            }
            return settings;
        }
        settings = new LoanForeclosureSettings { Id = 1 };
        settings.ChargeRules.Add(new LoanForeclosureChargeRule { SettingsId = settings.Id, Name = "Foreclosure service fee", IsServiceFee = true, CalculationType = "Percentage", CalculationBasis = "RemainingEmiTotal", Percentage = settings.ServiceFeePercentage, SortOrder = 0 });
        db.LoanForeclosureSettings.Add(settings);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.Entry(settings).State = EntityState.Detached; return await db.LoanForeclosureSettings.Include(s => s.ChargeRules).SingleAsync(s => s.Id == 1, ct); }
        return settings;
    }

    private static void ValidateChargeRules(IReadOnlyList<ForeclosureChargeRuleRequest> rules)
    {
        if (rules.Count > 30) throw new DomainValidationException("Configure no more than 30 foreclosure charge rules.");
        if (rules.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rules.Count)
            throw new DomainValidationException("Foreclosure charge names must be unique.");
        if (rules.Count(x => x.IsServiceFee) != 1) throw new DomainValidationException("Configure exactly one foreclosure service fee rule.");
    }

    private static ForeclosureSettingsResponse ToResponse(LoanForeclosureSettings x) => new(x.IsForeclosureAllowed,
        x.ChargeRules.SingleOrDefault(rule => rule.IsServiceFee)?.Percentage ?? 0m, x.FeeCalculationType, x.UpdatedAt,
        x.ChargeRules.OrderBy(rule => rule.SortOrder).ThenBy(rule => rule.Name).Select(rule => new ForeclosureChargeRuleResponse(rule.Id, rule.Name, rule.IsEnabled, rule.IsTax, rule.IsServiceFee, rule.CalculationType, rule.CalculationBasis, rule.Percentage, rule.FixedAmount, rule.SortOrder)).ToArray());
    private static ForeclosureChargeResponse ToResponse(ForeclosureChargeAmount charge) => new(charge.Name, charge.IsTax, charge.IsServiceFee, charge.CalculationType, charge.CalculationBasis, charge.Percentage, charge.FixedAmount, charge.BaseAmount, charge.Amount);
    private static ForeclosureChargeResponse ToResponse(LoanForeclosureCharge charge) => new(charge.Name, charge.IsTax, charge.IsServiceFee, charge.CalculationType, charge.CalculationBasis, charge.Percentage, charge.FixedAmount, charge.BaseAmount, charge.Amount);
    private static ForeclosureHistoryResponse ToResponse(LoanForeclosure x) => new(x.Id, x.ForeclosureNumber, x.ForeclosureDate, x.RemainingEmiAmount, x.OutstandingLateFees,
        x.ServiceFeePercentage, x.ServiceFeeAmount, x.TotalForeclosureAmount, x.AmountPaid, Math.Max(0m, x.TotalForeclosureAmount - x.AmountPaid), x.PaymentStatus,
        x.ForeclosureStatus, x.PaymentMode, x.TransactionReference, x.Remarks, x.Payments.OrderBy(p => p.ReceivedAt).Select(p => new ForeclosurePaymentResponse(p.Id, p.Amount, p.ReceivedAt, p.Method, p.Reference, p.CollectedBy)).ToArray(), x.Charges.OrderBy(c => c.Id).Select(ToResponse).ToArray());
}
