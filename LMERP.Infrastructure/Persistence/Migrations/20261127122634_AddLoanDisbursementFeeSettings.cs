using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDisbursementFeeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualDisbursedAmount",
                table: "LoanApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                table: "LoanApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeePercentage",
                table: "LoanApplications",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessingFeeAmount",
                table: "LoanApplications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessingFeePercentage",
                table: "LoanApplications",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoanDisbursementSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ProcessingFeePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PlatformFeePercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanDisbursementSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanDisbursementSettings");

            migrationBuilder.DropColumn(
                name: "ActualDisbursedAmount",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "PlatformFeePercentage",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "ProcessingFeeAmount",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "ProcessingFeePercentage",
                table: "LoanApplications");
        }
    }
}
