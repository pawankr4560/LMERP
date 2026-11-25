using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DataIntegrityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmiPayments_EmiScheduleId",
                table: "EmiPayments");

            migrationBuilder.CreateSequence(
                name: "LoanApplicationNumbers",
                startValue: 1000L);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "LoanApplications",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "EmiSchedules",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "CollectedBy",
                table: "EmiPayments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "EmiPayments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "EmiLateFees",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Customers",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_EmiPayments_EmiScheduleId_IdempotencyKey",
                table: "EmiPayments",
                columns: new[] { "EmiScheduleId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmiPayments_EmiScheduleId_IdempotencyKey",
                table: "EmiPayments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "CollectedBy",
                table: "EmiPayments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "EmiPayments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "EmiLateFees");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Customers");

            migrationBuilder.DropSequence(
                name: "LoanApplicationNumbers");

            migrationBuilder.CreateIndex(
                name: "IX_EmiPayments_EmiScheduleId",
                table: "EmiPayments",
                column: "EmiScheduleId");
        }
    }
}
