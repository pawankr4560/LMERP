using System.ComponentModel.DataAnnotations;

namespace LMERP.Application.Contracts;

public sealed record LoanDisbursementSettingsResponse(decimal ProcessingFeePercentage, decimal PlatformFeePercentage, DateTimeOffset UpdatedAt, string? UpdatedBy);

public sealed class LoanDisbursementSettingsRequest
{
    [Range(typeof(decimal), "0", "99.99")] public decimal ProcessingFeePercentage { get; init; }
    [Range(typeof(decimal), "0", "99.99")] public decimal PlatformFeePercentage { get; init; }
}
