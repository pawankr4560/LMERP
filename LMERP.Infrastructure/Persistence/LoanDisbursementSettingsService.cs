using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Application.Exceptions;
using LMERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

public sealed class LoanDisbursementSettingsService(LoanDbContext db) : ILoanDisbursementSettingsService
{
    public async Task<LoanDisbursementSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await db.LoanDisbursementSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        return ToResponse(settings ?? new LoanDisbursementSettings());
    }

    public async Task<LoanDisbursementSettingsResponse> UpdateAsync(LoanDisbursementSettingsRequest request, string updatedBy, CancellationToken cancellationToken)
    {
        if (request.ProcessingFeePercentage + request.PlatformFeePercentage >= 100m)
            throw new DomainValidationException("Processing and platform fees combined must be less than 100%.");

        var settings = await db.LoanDisbursementSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (settings is null)
        {
            settings = new LoanDisbursementSettings { Id = 1 };
            db.LoanDisbursementSettings.Add(settings);
        }
        settings.ProcessingFeePercentage = request.ProcessingFeePercentage;
        settings.PlatformFeePercentage = request.PlatformFeePercentage;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedBy = updatedBy.Length <= 256 ? updatedBy : updatedBy[..256];
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(settings);
    }

    private static LoanDisbursementSettingsResponse ToResponse(LoanDisbursementSettings settings) =>
        new(settings.ProcessingFeePercentage, settings.PlatformFeePercentage, settings.UpdatedAt, settings.UpdatedBy);
}
