using System.ComponentModel.DataAnnotations;

namespace LMERP.Application.Contracts;

public sealed record CompanyDocumentSettingsResponse(string CompanyName, string? Address, string? Phone, string? Email,
    string? Website, string? AuthorizedSignatoryName, string? AuthorizedSignatoryDesignation, string? FooterDisclaimer,
    bool HasLogo, bool HasSignature, bool HasStamp, DateTimeOffset UpdatedAt);

public sealed class CompanyDocumentSettingsRequest
{
    [Required, StringLength(160, MinimumLength = 2)] public string CompanyName { get; init; } = string.Empty;
    [StringLength(500)] public string? Address { get; init; }
    [StringLength(32)] public string? Phone { get; init; }
    [EmailAddress, StringLength(254)] public string? Email { get; init; }
    [Url, StringLength(200)] public string? Website { get; init; }
    [StringLength(120)] public string? AuthorizedSignatoryName { get; init; }
    [StringLength(120)] public string? AuthorizedSignatoryDesignation { get; init; }
    [StringLength(2000)] public string? FooterDisclaimer { get; init; }
}

public sealed record CompanyDocumentAssetUpload(byte[] Content, string ContentType);
public sealed record CompanyDocumentAsset(byte[] Content, string ContentType);
public sealed record LoanPdfFile(int CustomerId, string FileName, byte[] Content);
public sealed class LinkCustomerUserRequest { [Required, Range(1, int.MaxValue)] public int CustomerId { get; init; } }
