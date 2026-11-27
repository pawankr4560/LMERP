namespace LMERP.Domain.Entities;

public sealed class CompanyDocumentSettings
{
    public int Id { get; set; } = 1;
    public string CompanyName { get; set; } = "Loan Management System";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AuthorizedSignatoryName { get; set; }
    public string? AuthorizedSignatoryDesignation { get; set; }
    public string? FooterDisclaimer { get; set; }
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public byte[]? SignatureData { get; set; }
    public string? SignatureContentType { get; set; }
    public byte[]? StampData { get; set; }
    public string? StampContentType { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The immutable initial PDF produced with a successful disbursement.</summary>
public sealed class LoanDisbursementDocument
{
    public int Id { get; set; }
    public int LoanApplicationId { get; set; }
    public LoanApplication? LoanApplication { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public byte[] PdfContent { get; set; } = [];
}
