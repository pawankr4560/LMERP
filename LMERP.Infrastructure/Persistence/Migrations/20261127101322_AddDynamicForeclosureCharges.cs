using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicForeclosureCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanForeclosureChargeRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingsId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsTax = table.Column<bool>(type: "bit", nullable: false),
                    IsServiceFee = table.Column<bool>(type: "bit", nullable: false),
                    CalculationType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CalculationBasis = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanForeclosureChargeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanForeclosureChargeRules_LoanForeclosureSettings_SettingsId",
                        column: x => x.SettingsId,
                        principalTable: "LoanForeclosureSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoanForeclosureCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanForeclosureId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsTax = table.Column<bool>(type: "bit", nullable: false),
                    IsServiceFee = table.Column<bool>(type: "bit", nullable: false),
                    CalculationType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CalculationBasis = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanForeclosureCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanForeclosureCharges_LoanForeclosures_LoanForeclosureId",
                        column: x => x.LoanForeclosureId,
                        principalTable: "LoanForeclosures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosureChargeRules_SettingsId_Name",
                table: "LoanForeclosureChargeRules",
                columns: new[] { "SettingsId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanForeclosureCharges_LoanForeclosureId",
                table: "LoanForeclosureCharges",
                column: "LoanForeclosureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanForeclosureChargeRules");

            migrationBuilder.DropTable(
                name: "LoanForeclosureCharges");
        }
    }
}
