using LMERP.Domain.Entities;
using LMERP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LMERP.Infrastructure.Persistence;

public sealed class LoanDbContext(DbContextOptions<LoanDbContext> options) : IdentityDbContext<LoanUser, IdentityRole<int>, int>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.FullName).HasMaxLength(160).IsRequired();
            entity.Property(customer => customer.Email).HasMaxLength(254);
            entity.Property(customer => customer.Phone).HasMaxLength(24).IsRequired();
            entity.Property(customer => customer.City).HasMaxLength(100);
            entity.Property(customer => customer.Status).HasMaxLength(24).IsRequired();
            entity.HasIndex(customer => customer.Phone).IsUnique();
            entity.HasIndex(customer => customer.Email);
        });
        modelBuilder.Entity<LoanApplication>(entity =>
        {
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.ApplicationNumber).HasMaxLength(32).IsRequired();
            entity.Property(loan => loan.Product).HasMaxLength(80).IsRequired();
            entity.Property(loan => loan.RequestedAmount).HasPrecision(18, 2);
            entity.Property(loan => loan.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(loan => loan.ApplicationNumber).IsUnique();
            entity.HasOne(loan => loan.Customer).WithMany(customer => customer.LoanApplications).HasForeignKey(loan => loan.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public static async Task SeedAsync(LoanDbContext db)
    {
        if (await db.Customers.AnyAsync()) return;
        var rahul = new Customer { FullName = "Rahul Kumar", Phone = "+91 98765 43210", Email = "rahul.kumar@example.com", City = "Mumbai", Status = "Active", CreatedAt = DateTimeOffset.UtcNow.AddDays(-12) };
        var sneha = new Customer { FullName = "Sneha Patel", Phone = "+91 98201 34678", Email = "sneha.patel@example.com", City = "Pune", Status = "Active", CreatedAt = DateTimeOffset.UtcNow.AddDays(-8) };
        var arjun = new Customer { FullName = "Arjun Mehta", Phone = "+91 99876 12345", Email = "arjun.mehta@example.com", City = "Ahmedabad", Status = "Active", CreatedAt = DateTimeOffset.UtcNow.AddDays(-4) };
        db.Customers.AddRange(rahul, sneha, arjun);
        await db.SaveChangesAsync();
        db.LoanApplications.AddRange(
            new LoanApplication { ApplicationNumber = "LN-2025-0841", CustomerId = rahul.Id, Product = "Personal loan", RequestedAmount = 250000m, TenureMonths = 24, Status = "Under review", AppliedAt = DateTimeOffset.UtcNow.AddHours(-2) },
            new LoanApplication { ApplicationNumber = "LN-2025-0840", CustomerId = sneha.Id, Product = "Business loan", RequestedAmount = 800000m, TenureMonths = 36, Status = "Approved", AppliedAt = DateTimeOffset.UtcNow.AddHours(-4) },
            new LoanApplication { ApplicationNumber = "LN-2025-0839", CustomerId = arjun.Id, Product = "Vehicle loan", RequestedAmount = 575000m, TenureMonths = 48, Status = "Documents needed", AppliedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
    }
}
