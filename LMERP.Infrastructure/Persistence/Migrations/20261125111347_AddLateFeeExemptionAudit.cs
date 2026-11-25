using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLateFeeExemptionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LateFeeExemptedAt",
                table: "EmiSchedules",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LateFeeExemptedBy",
                table: "EmiSchedules",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LateFeeExemptionReason",
                table: "EmiSchedules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LateFeeExemptedAt",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "LateFeeExemptedBy",
                table: "EmiSchedules");

            migrationBuilder.DropColumn(
                name: "LateFeeExemptionReason",
                table: "EmiSchedules");
        }
    }
}
