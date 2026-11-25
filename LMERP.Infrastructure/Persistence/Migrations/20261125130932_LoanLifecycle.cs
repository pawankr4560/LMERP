using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoanLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "LoanApplications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisbursedAt",
                table: "LoanApplications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "LoanApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Existing schedule dates predate recorded disbursement dates; using the month before the first due date is an approximation.
            migrationBuilder.Sql("""
                UPDATE loans
                SET Status = N'Disbursed', DisbursedAt = DATEADD(month, -1, schedules.FirstDueDate)
                FROM LoanApplications AS loans
                INNER JOIN
                (
                    SELECT LoanApplicationId, MIN(DueDate) AS FirstDueDate
                    FROM EmiSchedules
                    GROUP BY LoanApplicationId
                ) AS schedules ON schedules.LoanApplicationId = loans.Id
                WHERE loans.Status = N'Approved';

                UPDATE loans
                SET Status = N'Closed', ClosedAt = SYSUTCDATETIME()
                FROM LoanApplications AS loans
                WHERE loans.Status = N'Disbursed'
                  AND EXISTS (SELECT 1 FROM EmiSchedules schedules WHERE schedules.LoanApplicationId = loans.Id)
                  AND NOT EXISTS (SELECT 1 FROM EmiSchedules schedules WHERE schedules.LoanApplicationId = loans.Id AND schedules.Status <> N'Paid');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "DisbursedAt",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "LoanApplications");
        }
    }
}
