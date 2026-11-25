using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmiLateFeesAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GracePeriodDays",
                table: "EmiSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsLateFeeApplicable",
                table: "EmiSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLateFeeExempt",
                table: "EmiSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LateFeeCalculationType",
                table: "EmiSchedules",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeeRate",
                table: "EmiSchedules",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumLateFeeAmount",
                table: "EmiSchedules",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumPenaltyDays",
                table: "EmiSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmiLateFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmiScheduleId = table.Column<int>(type: "int", nullable: false),
                    PenaltyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PenaltyRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WaivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WaiverReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WaivedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmiLateFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmiLateFees_EmiSchedules_EmiScheduleId",
                        column: x => x.EmiScheduleId,
                        principalTable: "EmiSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmiPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmiScheduleId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmiPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmiPayments_EmiSchedules_EmiScheduleId",
                        column: x => x.EmiScheduleId,
                        principalTable: "EmiSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsLateFeeApplicable = table.Column<bool>(type: "bit", nullable: false),
                    LateFeeRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LateFeeCalculationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    MaximumLateFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumPenaltyDays = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmiPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmiPaymentId = table.Column<int>(type: "int", nullable: false),
                    EmiScheduleId = table.Column<int>(type: "int", nullable: false),
                    EmiLateFeeId = table.Column<int>(type: "int", nullable: true),
                    Component = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmiPaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmiPaymentAllocations_EmiLateFees_EmiLateFeeId",
                        column: x => x.EmiLateFeeId,
                        principalTable: "EmiLateFees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmiPaymentAllocations_EmiPayments_EmiPaymentId",
                        column: x => x.EmiPaymentId,
                        principalTable: "EmiPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmiPaymentAllocations_EmiSchedules_EmiScheduleId",
                        column: x => x.EmiScheduleId,
                        principalTable: "EmiSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmiLateFees_EmiScheduleId_PenaltyDate",
                table: "EmiLateFees",
                columns: new[] { "EmiScheduleId", "PenaltyDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmiPaymentAllocations_EmiLateFeeId",
                table: "EmiPaymentAllocations",
                column: "EmiLateFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmiPaymentAllocations_EmiPaymentId",
                table: "EmiPaymentAllocations",
                column: "EmiPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmiPaymentAllocations_EmiScheduleId",
                table: "EmiPaymentAllocations",
                column: "EmiScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmiPayments_EmiScheduleId",
                table: "EmiPayments",
                column: "EmiScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmiPayments_ReceivedAt",
                table: "EmiPayments",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoanProducts_Name",
                table: "LoanProducts",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmiPaymentAllocations");

            migrationBuilder.DropTable(
                name: "LoanProducts");

            migrationBuilder.DropTable(
                name: "EmiLateFees");

            migrationBuilder.DropTable(
                name: "EmiPayments");

            migrationBuilder.DropColumn(
                name: "GracePeriodDays",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "IsLateFeeApplicable",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "IsLateFeeExempt",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "LateFeeCalculationType",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "LateFeeRate",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "MaximumLateFeeAmount",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "MaximumPenaltyDays",
                table: "EmiSchedules");
        }
    }
}
