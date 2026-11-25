using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

public static class LoanProductSeeder
{
    private static readonly string[] ProductNames = ["Personal loan", "Business loan", "Vehicle loan", "Home loan"];

    public static async Task SeedAsync(LoanDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.LoanProducts.AnyAsync(cancellationToken)) return;
        db.LoanProducts.AddRange(ProductNames.Select(name => new LMERP.Domain.Entities.LoanProduct
        {
            Name = name,
            IsActive = true,
            IsLateFeeApplicable = false
        }));
        await db.SaveChangesAsync(cancellationToken);
    }
}
