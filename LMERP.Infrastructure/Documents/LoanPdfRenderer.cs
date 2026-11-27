using System.Globalization;
using LMERP.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LMERP.Infrastructure.Documents;

internal sealed record EmiPdfLine(int Number, DateTimeOffset DueDate, decimal OpeningBalance, decimal Principal,
    decimal Interest, decimal Emi, decimal Paid, decimal Outstanding, string Status);

public static class LoanPdfRenderer
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private const string Navy = "#18324A";
    private const string Muted = "#657789";
    private const string Border = "#DCE4EA";
    private const string Pale = "#F2F6F8";

    static LoanPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(CompanyDocumentSettings company, LoanApplication loan, IReadOnlyCollection<EmiSchedule> schedules,
        bool latestStatement, DateTimeOffset generatedAt, TimeZoneInfo timeZone)
    {
        var ordered = schedules.OrderBy(row => row.InstallmentNumber).ToArray();
        var lines = ordered.Select(schedule =>
        {
            var paid = schedule.Payments.SelectMany(payment => payment.Allocations)
                .Where(allocation => allocation.Component == "Emi").Sum(allocation => allocation.Amount);
            var outstanding = Math.Max(0m, schedule.EmiAmount - paid);
            var status = latestStatement ? CurrentStatus(schedule, generatedAt, timeZone) : schedule.Status;
            return new EmiPdfLine(schedule.InstallmentNumber, TimeZoneInfo.ConvertTime(schedule.DueDate, timeZone),
                schedule.RemainingBalance + schedule.PrincipalAmount, schedule.PrincipalAmount, schedule.InterestAmount,
                schedule.EmiAmount, paid, outstanding, status);
        }).ToArray();

        var firstDue = lines.Length == 0 ? (DateTimeOffset?)null : lines.Min(row => row.DueDate);
        var maturity = lines.Length == 0 ? (DateTimeOffset?)null : lines.Max(row => row.DueDate);
        var documentNumber = latestStatement ? $"EMI-{loan.ApplicationNumber}-{generatedAt:yyyyMMddHHmm}" : $"DA-{loan.ApplicationNumber}";
        var title = latestStatement ? "Latest EMI Repayment Statement" : "Loan Disbursement Advice & EMI Repayment Schedule";

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(8).FontColor(Navy));
            page.Header().Element(container => DrawHeader(container, company, title, documentNumber, generatedAt));
            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(10);
                column.Item().Element(container => DrawSectionHeading(container, "CUSTOMER AND LOAN DETAILS"));
                column.Item().Element(container => DrawDetails(container, company, loan, firstDue, maturity, lines, timeZone));
                column.Item().Element(container => DrawSectionHeading(container, "EMI REPAYMENT SCHEDULE"));
                column.Item().Element(container => DrawSchedule(container, lines));
                if (!latestStatement)
                    column.Item().Element(container => DrawSignature(container, company));
            });
            page.Footer().Element(container => DrawFooter(container, company, latestStatement));
        })).GeneratePdf();
    }

    public static string CurrentStatus(EmiSchedule schedule, DateTimeOffset generatedAt, TimeZoneInfo timeZone)
    {
        var paid = schedule.Payments.SelectMany(payment => payment.Allocations)
            .Where(allocation => allocation.Component == "Emi").Sum(allocation => allocation.Amount);
        if (paid >= schedule.EmiAmount) return "Paid";
        var dueDate = TimeZoneInfo.ConvertTime(schedule.DueDate, timeZone).Date;
        var today = TimeZoneInfo.ConvertTime(generatedAt, timeZone).Date;
        if (dueDate < today) return "Overdue";
        return paid > 0m ? "Partially paid" : "Pending";
    }

    private static void DrawHeader(IContainer container, CompanyDocumentSettings company, string title, string documentNumber, DateTimeOffset generatedAt)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                if (company.LogoData is { Length: > 0 })
                    row.ConstantItem(72).Height(46).PaddingRight(12).Image(company.LogoData).FitArea();
                row.RelativeItem().Column(identity =>
                {
                    identity.Item().Text(company.CompanyName).FontSize(17).SemiBold().FontColor(Navy);
                    identity.Item().Text(JoinNonEmpty(company.Address, company.Phone, company.Email, company.Website)).FontSize(8).FontColor(Muted);
                });
                row.ConstantItem(230).AlignRight().Column(meta =>
                {
                    meta.Item().Text(title).FontSize(12).SemiBold().FontColor(Navy).AlignRight();
                    meta.Item().Text($"Document No. {documentNumber}").FontSize(8).FontColor(Muted).AlignRight();
                    meta.Item().Text($"Generated {generatedAt.ToString("dd MMM yyyy HH:mm", India)} UTC").FontSize(8).FontColor(Muted).AlignRight();
                });
            });
            column.Item().PaddingTop(8).BorderBottom(1).BorderColor(Navy);
        });
    }

    private static void DrawDetails(IContainer container, CompanyDocumentSettings company, LoanApplication loan,
        DateTimeOffset? firstDue, DateTimeOffset? maturity, IReadOnlyCollection<EmiPdfLine> lines, TimeZoneInfo timeZone)
    {
        var firstEmi = lines.OrderBy(line => line.Number).FirstOrDefault()?.Emi ?? 0m;
        var rows = new (string Label, string Value)[]
        {
            ("Customer name", loan.Customer?.FullName ?? "—"), ("Customer ID", loan.CustomerId.ToString(India)),
            ("Contact number", loan.Customer?.Phone ?? "—"), ("Address", JoinNonEmpty(loan.Customer?.City)),
            ("Loan account number", loan.ApplicationNumber), ("Application number", loan.ApplicationNumber),
            ("Loan principal", Money(loan.RequestedAmount)),
            ($"Processing fee ({(loan.ProcessingFeePercentage ?? 0m).ToString("0.##", India)}%)", Money(loan.ProcessingFeeAmount ?? 0m)),
            ($"Platform fee ({(loan.PlatformFeePercentage ?? 0m).ToString("0.##", India)}%)", Money(loan.PlatformFeeAmount ?? 0m)),
            ("Actual disbursed amount", Money(loan.ActualDisbursedAmount ?? loan.RequestedAmount)),
            ("Interest rate / month", FormatMonthlyInterestRate(loan.InterestRate)), ("Interest type", loan.IsFlatInterest ? "Flat" : "Reducing balance"),
            ("Loan tenure", $"{loan.TenureMonths} months"), ("EMI amount", Money(firstEmi)),
            ("Disbursement date", Date(loan.DisbursedAt.HasValue ? TimeZoneInfo.ConvertTime(loan.DisbursedAt.Value, timeZone) : null)),
            ("First EMI due date", Date(firstDue)), ("Maturity date", Date(maturity)),
            ("EMI count", lines.Count.ToString(India))
        };
        container.Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
            foreach (var row in rows)
            {
                table.Cell().Element(DetailCell).Column(cell =>
                {
                    cell.Item().Text(row.Label.ToUpperInvariant()).FontSize(6.5f).FontColor(Muted);
                    cell.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(row.Value) ? "—" : row.Value).SemiBold().FontSize(8);
                });
            }
        });
    }

    private static void DrawSchedule(IContainer container, IReadOnlyCollection<EmiPdfLine> lines)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(32); columns.ConstantColumn(62); columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1); columns.RelativeColumn(1); columns.RelativeColumn(1);
                columns.RelativeColumn(1); columns.RelativeColumn(1.1f); columns.ConstantColumn(74);
            });
            table.Header(header =>
            {
                HeaderCell(header, "#"); HeaderCell(header, "DUE DATE"); HeaderCell(header, "OPENING BALANCE", true);
                HeaderCell(header, "PRINCIPAL", true); HeaderCell(header, "INTEREST", true); HeaderCell(header, "EMI AMOUNT", true);
                HeaderCell(header, "PAID AMOUNT", true); HeaderCell(header, "OUTSTANDING", true); HeaderCell(header, "STATUS");
            });
            foreach (var line in lines)
            {
                var background = line.Number % 2 == 0 ? Pale : "#FFFFFF";
                BodyCell(table, line.Number.ToString(India), background, false);
                BodyCell(table, Date(line.DueDate), background, false);
                BodyCell(table, Money(line.OpeningBalance), background, true);
                BodyCell(table, Money(line.Principal), background, true);
                BodyCell(table, Money(line.Interest), background, true);
                BodyCell(table, Money(line.Emi), background, true);
                BodyCell(table, Money(line.Paid), background, true);
                BodyCell(table, Money(line.Outstanding), background, true);
                BodyCell(table, line.Status, background, false);
            }
        });
    }

    private static void DrawSignature(IContainer container, CompanyDocumentSettings company)
    {
        container.PaddingTop(12).Row(row =>
        {
            row.RelativeItem().Column(terms =>
            {
                terms.Item().Text("IMPORTANT INFORMATION").FontSize(7).SemiBold().FontColor(Navy);
                terms.Item().PaddingTop(3).Text("Repay each EMI on or before its due date. This schedule reflects the loan terms and installment values recorded at disbursement; the executed loan agreement governs the facility.").FontSize(7).FontColor(Muted);
            });
            row.ConstantItem(210).PaddingLeft(16).Column(signature =>
            {
                if (company.SignatureData is { Length: > 0 }) signature.Item().Height(42).Image(company.SignatureData).FitArea();
                else signature.Item().Height(42).AlignBottom().Text(" ");
                signature.Item().BorderTop(1).BorderColor(Border).PaddingTop(4).Text(company.AuthorizedSignatoryName ?? "Authorized signatory").SemiBold();
                signature.Item().Text(company.AuthorizedSignatoryDesignation ?? string.Empty).FontSize(7).FontColor(Muted);
                signature.Item().Text(company.CompanyName).FontSize(7).FontColor(Muted);
                if (company.StampData is { Length: > 0 }) signature.Item().AlignRight().Height(48).Image(company.StampData).FitArea();
            });
        });
    }

    private static void DrawFooter(IContainer container, CompanyDocumentSettings company, bool latestStatement)
    {
        var disclaimer = string.IsNullOrWhiteSpace(company.FooterDisclaimer)
            ? "System-generated document. No handwritten signature is required unless applicable company policy states otherwise."
            : company.FooterDisclaimer!;
        container.BorderTop(0.5f).BorderColor(Border).PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(latestStatement ? "Current statement generated from live EMI and payment records." : "Original disbursement advice archived at disbursement; this copy is unchanged by later repayments.").FontSize(6.5f).FontColor(Muted);
                column.Item().Text(disclaimer).FontSize(6.5f).FontColor(Muted);
            });
            row.ConstantItem(68).AlignRight().AlignMiddle().Text(text =>
            {
                text.Span("Page ").FontSize(7).FontColor(Muted);
                text.CurrentPageNumber().FontSize(7).FontColor(Muted);
            });
        });
    }

    private static void DrawSectionHeading(IContainer container, string text) =>
        container.Background(Pale).PaddingVertical(5).PaddingHorizontal(7).Text(text).FontSize(7).SemiBold().FontColor(Navy);

    private static IContainer DetailCell(IContainer container) => container.Border(0.5f).BorderColor(Border).Padding(6);
    private static void HeaderCell(TableCellDescriptor header, string text, bool numeric = false)
    {
        var cell = header.Cell().Background(Navy).PaddingVertical(6).PaddingHorizontal(4);
        if (numeric) cell.AlignRight().Text(text).FontSize(6.5f).SemiBold().FontColor(Colors.White);
        else cell.Text(text).FontSize(6.5f).SemiBold().FontColor(Colors.White);
    }
    private static void BodyCell(TableDescriptor table, string text, string background, bool numeric)
    {
        var cell = table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(4);
        if (numeric) cell.AlignRight().Text(text).FontSize(6.5f).FontColor(Navy);
        else cell.Text(text).FontSize(6.5f).FontColor(Navy);
    }

    public static string FormatMoney(decimal amount) => $"₹ {amount.ToString("N2", India)}";
    public static string FormatMonthlyInterestRate(decimal annualRate) => $"{(annualRate / 12m).ToString("0.##", India)}% per month";
    public static string FormatDate(DateTimeOffset? date) => date.HasValue ? date.Value.ToString("dd MMM yyyy", India) : "—";
    private static string Money(decimal amount) => FormatMoney(amount);
    private static string Date(DateTimeOffset? date) => FormatDate(date);
    private static string JoinNonEmpty(params string?[] values) => string.Join("  |  ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
}
