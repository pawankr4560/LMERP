using System.ComponentModel.DataAnnotations;
using LMERP.Application.Abstractions;
using LMERP.Application.Contracts;
using LMERP.Application.Exceptions;
using LMERP.Server.Auth;
using LMERP.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.Controllers;

[ApiController]
[Authorize(Roles = Roles.DocumentReaders)]
[Route("api/documents")]
public sealed class LoanDocumentsController(ILoanDocumentService documents) : ControllerBase
{
    private const long MaxImageSize = 2 * 1024 * 1024;

    [HttpGet("settings")]
    [Authorize(Roles = Roles.LoanStaff + "," + Roles.CollectionsManager + "," + Roles.Analyst)]
    public async Task<ActionResult<BaseResponse<CompanyDocumentSettingsResponse>>> GetSettings(CancellationToken ct) =>
        Ok(BaseResponse<CompanyDocumentSettingsResponse>.Succeeded(await documents.GetCompanySettingsAsync(ct)));

    [HttpPut("settings")]
    [Authorize(Roles = Roles.Administrator)]
    [RequestSizeLimit(6_500_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6_500_000)]
    public async Task<ActionResult<BaseResponse<CompanyDocumentSettingsResponse>>> UpdateSettings([FromForm] CompanyDocumentSettingsForm form, CancellationToken ct)
    {
        var request = new CompanyDocumentSettingsRequest
        {
            CompanyName = form.CompanyName, Address = form.Address, Phone = form.Phone, Email = form.Email, Website = form.Website,
            AuthorizedSignatoryName = form.AuthorizedSignatoryName, AuthorizedSignatoryDesignation = form.AuthorizedSignatoryDesignation,
            FooterDisclaimer = form.FooterDisclaimer
        };
        if (!TryValidateModel(request)) return ValidationProblem(ModelState);
        var updated = await documents.UpdateCompanySettingsAsync(request, await ReadImageAsync(form.Logo, ct),
            await ReadImageAsync(form.Signature, ct), await ReadImageAsync(form.Stamp, ct), ct);
        return Ok(BaseResponse<CompanyDocumentSettingsResponse>.Succeeded(updated, "Company document settings saved."));
    }

    [HttpGet("settings/assets/{assetName}")]
    [Authorize(Roles = Roles.LoanStaff + "," + Roles.CollectionsManager + "," + Roles.Analyst)]
    public async Task<IActionResult> GetCompanyAsset(string assetName, CancellationToken ct)
    {
        var asset = await documents.GetCompanyAssetAsync(assetName, ct);
        if (asset is null) return NotFound(BaseResponse<bool>.Failed("Company document image was not found."));
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(asset.Content, asset.ContentType);
    }

    [HttpGet("loans/{loanId:int}/disbursement-advice")]
    public async Task<IActionResult> GetDisbursementAdvice(int loanId, CancellationToken ct)
    {
        var file = await documents.GetDisbursementAdviceAsync(loanId, ct)
            ?? throw new NotFoundException("The immutable disbursement advice is not available for this loan.");
        if (!LoanDocumentAuthorization.CanRead(User, file.CustomerId)) return Forbid();
        return Pdf(file);
    }

    [HttpGet("loans/{loanId:int}/emi-statement")]
    public async Task<IActionResult> GetLatestEmiStatement(int loanId, CancellationToken ct)
    {
        var file = await documents.GenerateLatestEmiStatementAsync(loanId, ct);
        if (!LoanDocumentAuthorization.CanRead(User, file.CustomerId)) return Forbid();
        return Pdf(file);
    }

    private IActionResult Pdf(LoanPdfFile file)
    {
        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, "application/pdf", file.FileName);
    }

    private static async Task<CompanyDocumentAssetUpload?> ReadImageAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > MaxImageSize) throw new DomainValidationException("Each company image must be no larger than 2 MB.");
        if (file.ContentType is not ("image/png" or "image/jpeg"))
            throw new DomainValidationException("Upload a PNG or JPEG image.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var content = stream.ToArray();
        var isPng = content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var isJpeg = content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff;
        if (!(file.ContentType == "image/png" && isPng) && !(file.ContentType == "image/jpeg" && isJpeg))
            throw new DomainValidationException("The uploaded image content does not match its PNG or JPEG type.");
        return new CompanyDocumentAssetUpload(content, file.ContentType);
    }
}

public sealed class CompanyDocumentSettingsForm
{
    [Required, StringLength(160, MinimumLength = 2)] public string CompanyName { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(32)] public string? Phone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [Url, StringLength(200)] public string? Website { get; set; }
    [StringLength(120)] public string? AuthorizedSignatoryName { get; set; }
    [StringLength(120)] public string? AuthorizedSignatoryDesignation { get; set; }
    [StringLength(2000)] public string? FooterDisclaimer { get; set; }
    public IFormFile? Logo { get; set; }
    public IFormFile? Signature { get; set; }
    public IFormFile? Stamp { get; set; }
}

[ApiController]
[Authorize(Roles = Roles.Administrator)]
[Route("api/documents/customer-links")]
public sealed class CustomerDocumentAccessController(ICustomerDocumentAccessService access) : ControllerBase
{
    [HttpPut("{userId:int}")]
    public async Task<ActionResult<BaseResponse<bool>>> Link(int userId, LinkCustomerUserRequest request, CancellationToken ct)
    {
        await access.LinkUserToCustomerAsync(userId, request.CustomerId, ct);
        return Ok(BaseResponse<bool>.Succeeded(true, "Customer document access linked. The user must sign in again for the new claim to take effect."));
    }
}
