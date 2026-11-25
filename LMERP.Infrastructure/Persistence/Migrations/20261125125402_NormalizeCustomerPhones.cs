using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCustomerPhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @duplicates nvarchar(max);
                WITH SourcePhones AS
                (
                    SELECT Id,
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Phone, N' ', N''), N'-', N''), N'.', N''), N'(', N''), N')', N'') AS Digits
                    FROM Customers
                ), NormalizedPhones AS
                (
                    SELECT Id,
                        CASE
                            WHEN LEFT(Digits, 2) = N'00' THEN N'+' + SUBSTRING(Digits, 3, 32)
                            WHEN LEN(Digits) = 10 AND Digits NOT LIKE N'%[^0-9]%' THEN N'+91' + Digits
                            WHEN LEN(Digits) = 11 AND LEFT(Digits, 1) = N'0' AND Digits NOT LIKE N'%[^0-9]%' THEN N'+91' + SUBSTRING(Digits, 2, 32)
                            ELSE Digits
                        END AS NormalizedPhone
                    FROM SourcePhones
                )
                SELECT @duplicates = STRING_AGG(CONVERT(nvarchar(max), DuplicatePhone), N', ')
                FROM (SELECT NormalizedPhone AS DuplicatePhone FROM NormalizedPhones GROUP BY NormalizedPhone HAVING COUNT(*) > 1) AS duplicates;

                IF @duplicates IS NOT NULL
                BEGIN
                    DECLARE @message nvarchar(2048) = CONCAT(N'Phone normalization would create duplicate customer phone numbers: ', @duplicates);
                    THROW 51000, @message, 1;
                END;

                WITH SourcePhones AS
                (
                    SELECT Id,
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Phone, N' ', N''), N'-', N''), N'.', N''), N'(', N''), N')', N'') AS Digits
                    FROM Customers
                ), NormalizedPhones AS
                (
                    SELECT Id,
                        CASE
                            WHEN LEFT(Digits, 2) = N'00' THEN N'+' + SUBSTRING(Digits, 3, 32)
                            WHEN LEN(Digits) = 10 AND Digits NOT LIKE N'%[^0-9]%' THEN N'+91' + Digits
                            WHEN LEN(Digits) = 11 AND LEFT(Digits, 1) = N'0' AND Digits NOT LIKE N'%[^0-9]%' THEN N'+91' + SUBSTRING(Digits, 2, 32)
                            ELSE Digits
                        END AS NormalizedPhone
                    FROM SourcePhones
                )
                UPDATE customer SET Phone = normalized.NormalizedPhone
                FROM Customers AS customer INNER JOIN NormalizedPhones AS normalized ON normalized.Id = customer.Id
                WHERE customer.Phone <> normalized.NormalizedPhone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
