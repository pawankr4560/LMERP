using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations;

public partial class AddLoanSoftDelete : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAtUtc",
            table: "LoanApplications",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "LoanApplications",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DeletedAtUtc", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "IsDeleted", table: "LoanApplications");
    }
}
