using System.Security.Claims;
using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Application.Exceptions;
using LMERP.Domain;
using LMERP.Domain.Entities;
using LMERP.Infrastructure.Identity;
using LMERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Documents;

public sealed class LoanDocumentService(LoanDbContext db, IBusinessClock clock) : ILoanDocumentService
{
    public async Task<byte[]> GenerateDisbursementAdviceAsync(LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedules, CancellationToken ct)
    {
        if (loan.Status != LoanStatuses.Disbursed || loan.DisbursedAt is null || schedules.Count == 0)
            throw new ConflictException("A disbursement advice can only be generated for a disbursed loan with its EMI schedule.");
        var settings = await GetOrCreateSettingsAsync(ct);
        return LoanPdfRenderer.Render(settings, loan, schedules, latestStatement: false, DateTimeOffset.UtcNow, clock.TimeZone);
    }

    public async Task<LoanPdfFile?> GetDisbursementAdviceAsync(int loanId, CancellationToken ct)
    {
        return await db.LoanDisbursementDocuments.AsNoTracking()
            .Where(document => document.LoanApplicationId == loanId && document.LoanApplication!.DisbursedAt != null)
            .Select(document => new LoanPdfFile(document.LoanApplication!.CustomerId,
                $"{document.DocumentNumber}.pdf", document.PdfContent))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<LoanPdfFile> GenerateLatestEmiStatementAsync(int loanId, CancellationToken ct)
    {
        var loan = await db.LoanApplications.AsNoTracking().Include(item => item.Customer)
            .Include(item => item.EmiSchedules).ThenInclude(schedule => schedule.Payments).ThenInclude(payment => payment.Allocations)
            .SingleOrDefaultAsync(item => item.Id == loanId, ct)
            ?? throw new NotFoundException("Loan application was not found.");
        if (loan.DisbursedAt is null || loan.Status is not (LoanStatuses.Disbursed or LoanStatuses.Closed or LoanStatuses.Foreclosed))
            throw new ConflictException("An EMI statement is available only after successful loan disbursement.");
        if (loan.EmiSchedules.Count == 0) throw new ConflictException("This loan does not have a persisted EMI schedule.");
        var settings = await GetOrCreateSettingsAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var pdf = LoanPdfRenderer.Render(settings, loan, loan.EmiSchedules.ToArray(), latestStatement: true, now, clock.TimeZone);
        return new LoanPdfFile(loan.CustomerId, $"EMI-{loan.ApplicationNumber}-{now:yyyyMMddHHmm}.pdf", pdf);
    }

    public async Task<CompanyDocumentSettingsResponse> GetCompanySettingsAsync(CancellationToken ct) =>
        ToResponse(await GetOrCreateSettingsAsync(ct));

    public async Task<CompanyDocumentSettingsResponse> UpdateCompanySettingsAsync(CompanyDocumentSettingsRequest request,
        CompanyDocumentAssetUpload? logo, CompanyDocumentAssetUpload? signature, CompanyDocumentAssetUpload? stamp, CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        settings.CompanyName = request.CompanyName.Trim();
        settings.Address = Clean(request.Address);
        settings.Phone = Clean(request.Phone);
        settings.Email = Clean(request.Email);
        settings.Website = Clean(request.Website);
        settings.AuthorizedSignatoryName = Clean(request.AuthorizedSignatoryName);
        settings.AuthorizedSignatoryDesignation = Clean(request.AuthorizedSignatoryDesignation);
        settings.FooterDisclaimer = Clean(request.FooterDisclaimer);
        ReplaceAsset(logo, content => settings.LogoData = content, contentType => settings.LogoContentType = contentType);
        ReplaceAsset(signature, content => settings.SignatureData = content, contentType => settings.SignatureContentType = contentType);
        ReplaceAsset(stamp, content => settings.StampData = content, contentType => settings.StampContentType = contentType);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(settings);
    }

    public async Task<CompanyDocumentAsset?> GetCompanyAssetAsync(string assetName, CancellationToken ct)
    {
        var settings = await db.CompanyDocumentSettings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == 1, ct);
        if (settings is null) return null;
        var (content, contentType) = assetName.ToLowerInvariant() switch
        {
            "logo" => (settings.LogoData, settings.LogoContentType),
            "signature" => (settings.SignatureData, settings.SignatureContentType),
            "stamp" => (settings.StampData, settings.StampContentType),
            _ => (null, null)
        };
        return content is { Length: > 0 } && contentType is not null ? new CompanyDocumentAsset(content, contentType) : null;
    }

    private async Task<CompanyDocumentSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await db.CompanyDocumentSettings.SingleOrDefaultAsync(item => item.Id == 1, ct);
        if (settings is not null) return settings;
        settings = new CompanyDocumentSettings { Id = 1 };
        db.CompanyDocumentSettings.Add(settings);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(settings).State = EntityState.Detached;
            return await db.CompanyDocumentSettings.SingleAsync(item => item.Id == 1, ct);
        }
        return settings;
    }

    private static void ReplaceAsset(CompanyDocumentAssetUpload? asset, Action<byte[]> setContent, Action<string?> setContentType)
    {
        if (asset is null) return;
        setContent(asset.Content);
        setContentType(asset.ContentType);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CompanyDocumentSettingsResponse ToResponse(CompanyDocumentSettings settings) => new(settings.CompanyName, settings.Address,
        settings.Phone, settings.Email, settings.Website, settings.AuthorizedSignatoryName, settings.AuthorizedSignatoryDesignation,
        settings.FooterDisclaimer, settings.LogoData is { Length: > 0 }, settings.SignatureData is { Length: > 0 },
        settings.StampData is { Length: > 0 }, settings.UpdatedAt);
}

public sealed class CustomerDocumentAccessService(LoanDbContext db, UserManager<LoanUser> users, RoleManager<IdentityRole<int>> roles) : ICustomerDocumentAccessService
{
    public async Task<bool> LinkUserToCustomerAsync(int userId, int customerId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!await db.Customers.AnyAsync(customer => customer.Id == customerId, ct)) throw new NotFoundException("Customer was not found.");
        var user = await users.FindByIdAsync(userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ?? throw new NotFoundException("User account was not found.");
        if (!user.IsActive) throw new ConflictException("An inactive user account cannot be linked to a customer.");
        var staffRoles = new[] { IdentityRoles.Administrator, IdentityRoles.LoanOfficer, IdentityRoles.CollectionsManager, IdentityRoles.Analyst };
        foreach (var role in staffRoles)
            if (await users.IsInRoleAsync(user, role)) throw new ConflictException("A staff account cannot be linked as a customer account.");

        var customerIdValue = customerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var matches = await users.GetUsersForClaimAsync(new Claim(IdentityClaims.CustomerId, customerIdValue));
        if (matches.Any(match => match.Id != user.Id)) throw new ConflictException("This customer is already linked to another login account.");
        var existingClaims = (await users.GetClaimsAsync(user)).Where(claim => claim.Type == IdentityClaims.CustomerId).ToArray();
        if (existingClaims.Length > 0)
        {
            var removal = await users.RemoveClaimsAsync(user, existingClaims);
            if (!removal.Succeeded) throw new InvalidOperationException("Could not replace the account's existing customer link.");
        }
        var result = await users.AddClaimAsync(user, new Claim(IdentityClaims.CustomerId, customerIdValue));
        if (!result.Succeeded) throw new InvalidOperationException("Could not link the user account to this customer.");
        if (!await users.IsInRoleAsync(user, IdentityRoles.Customer))
        {
            if (!await roles.RoleExistsAsync(IdentityRoles.Customer)) await roles.CreateAsync(new IdentityRole<int>(IdentityRoles.Customer));
            result = await users.AddToRoleAsync(user, IdentityRoles.Customer);
            if (!result.Succeeded) throw new InvalidOperationException("Could not assign the Customer role to this account.");
        }
        result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("Could not refresh the account security state after linking.");
        return true;
    }
}
