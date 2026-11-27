using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDisbursementDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyDocumentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuthorizedSignatoryName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AuthorizedSignatoryDesignation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FooterDisclaimer = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LogoData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LogoContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SignatureData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignatureContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    StampData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    StampContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDocumentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoanDisbursementDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanApplicationId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PdfContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanDisbursementDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanDisbursementDocuments_LoanApplications_LoanApplicationId",
                        column: x => x.LoanApplicationId,
                        principalTable: "LoanApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanDisbursementDocuments_DocumentNumber",
                table: "LoanDisbursementDocuments",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanDisbursementDocuments_LoanApplicationId",
                table: "LoanDisbursementDocuments",
                column: "LoanApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDocumentSettings");

            migrationBuilder.DropTable(
                name: "LoanDisbursementDocuments");
        }
    }
}
