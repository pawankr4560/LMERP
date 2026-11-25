using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations;

public partial class AddLoanInterestAndEmiSchedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "InterestRate", table: "LoanApplications", type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<bool>(name: "IsFlatInterest", table: "LoanApplications", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.CreateTable(
            name: "EmiSchedules",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                LoanApplicationId = table.Column<int>(type: "int", nullable: false),
                InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                InterestAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                EmiAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                RemainingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmiSchedules", x => x.Id);
                table.ForeignKey(name: "FK_EmiSchedules_LoanApplications_LoanApplicationId", column: x => x.LoanApplicationId, principalTable: "LoanApplications", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_EmiSchedules_LoanApplicationId_InstallmentNumber", table: "EmiSchedules", columns: new[] { "LoanApplicationId", "InstallmentNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmiSchedules");
        migrationBuilder.DropColumn(name: "InterestRate", table: "LoanApplications");
        migrationBuilder.DropColumn(name: "IsFlatInterest", table: "LoanApplications");
    }
}
