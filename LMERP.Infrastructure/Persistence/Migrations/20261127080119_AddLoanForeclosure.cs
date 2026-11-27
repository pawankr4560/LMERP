using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanForeclosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanForeclosures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanApplicationId = table.Column<int>(type: "int", nullable: false),
                    ForeclosureNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ForeclosureDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OutstandingPrincipal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingLateFees = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RemainingEmiAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceFeePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ServiceFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Adjustments = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalForeclosureAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ForeclosureStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanForeclosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanForeclosures_LoanApplications_LoanApplicationId",
                        column: x => x.LoanApplicationId,
                        principalTable: "LoanApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanForeclosureSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IsForeclosureAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ServiceFeePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FeeCalculationType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanForeclosureSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoanForeclosurePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanForeclosureId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CollectedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanForeclosurePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanForeclosurePayments_LoanForeclosures_LoanForeclosureId",
                        column: x => x.LoanForeclosureId,
                        principalTable: "LoanForeclosures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosurePayments_IdempotencyKey",
                table: "LoanForeclosurePayments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosurePayments_LoanForeclosureId",
                table: "LoanForeclosurePayments",
                column: "LoanForeclosureId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosurePayments_ReceivedAt",
                table: "LoanForeclosurePayments",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosures_ForeclosureNumber",
                table: "LoanForeclosures",
                column: "ForeclosureNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosures_LoanApplicationId",
                table: "LoanForeclosures",
                column: "LoanApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanForeclosurePayments");

            migrationBuilder.DropTable(
                name: "LoanForeclosureSettings");

            migrationBuilder.DropTable(
                name: "LoanForeclosures");
        }
    }
}
