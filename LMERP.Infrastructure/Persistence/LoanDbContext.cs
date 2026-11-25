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
    public DbSet<EmiSchedule> EmiSchedules => Set<EmiSchedule>();
    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<EmiLateFee> EmiLateFees => Set<EmiLateFee>();
    public DbSet<EmiPayment> EmiPayments => Set<EmiPayment>();
    public DbSet<EmiPaymentAllocation> EmiPaymentAllocations => Set<EmiPaymentAllocation>();

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
            entity.Property(loan => loan.InterestRate).HasPrecision(5, 2).HasDefaultValue(0m);
            entity.Property(loan => loan.IsFlatInterest).HasDefaultValue(true);
            entity.Property(loan => loan.Status).HasMaxLength(32).IsRequired();
            entity.Property(loan => loan.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(loan => loan.ApplicationNumber).IsUnique();
            entity.HasOne(loan => loan.Customer).WithMany(customer => customer.LoanApplications).HasForeignKey(loan => loan.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(loan => !loan.IsDeleted);
        });
        modelBuilder.Entity<EmiSchedule>(entity =>
        {
            entity.HasKey(schedule => schedule.Id);
            entity.Property(schedule => schedule.PrincipalAmount).HasPrecision(18, 2);
            entity.Property(schedule => schedule.InterestAmount).HasPrecision(18, 2);
            entity.Property(schedule => schedule.EmiAmount).HasPrecision(18, 2);
            entity.Property(schedule => schedule.RemainingBalance).HasPrecision(18, 2);
            entity.Property(schedule => schedule.Status).HasMaxLength(24).IsRequired();
            entity.Property(schedule => schedule.LateFeeRate).HasPrecision(5, 2);
            entity.Property(schedule => schedule.MaximumLateFeeAmount).HasPrecision(18, 2);
            entity.Property(schedule => schedule.LateFeeCalculationType).HasMaxLength(40).IsRequired();
            entity.Property(schedule => schedule.LateFeeExemptedBy).HasMaxLength(256);
            entity.Property(schedule => schedule.LateFeeExemptionReason).HasMaxLength(500);
            entity.HasIndex(schedule => new { schedule.LoanApplicationId, schedule.InstallmentNumber }).IsUnique();
            entity.HasOne(schedule => schedule.LoanApplication)
                .WithMany(loan => loan.EmiSchedules)
                .HasForeignKey(schedule => schedule.LoanApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(schedule => schedule.LateFees).WithOne(fee => fee.EmiSchedule).HasForeignKey(fee => fee.EmiScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(schedule => schedule.Payments).WithOne(payment => payment.EmiSchedule).HasForeignKey(payment => payment.EmiScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(schedule => !schedule.LoanApplication!.IsDeleted);
        });
        modelBuilder.Entity<LoanProduct>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(product => product.Name).IsUnique();
            entity.Property(product => product.LateFeeRate).HasPrecision(5, 2);
            entity.Property(product => product.LateFeeCalculationType).HasMaxLength(40).IsRequired();
            entity.Property(product => product.MaximumLateFeeAmount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<EmiLateFee>(entity =>
        {
            entity.HasKey(fee => fee.Id);
            entity.Property(fee => fee.BaseAmount).HasPrecision(18, 2);
            entity.Property(fee => fee.PenaltyRate).HasPrecision(5, 2);
            entity.Property(fee => fee.PenaltyAmount).HasPrecision(18, 2);
            entity.Property(fee => fee.WaivedAmount).HasPrecision(18, 2);
            entity.Property(fee => fee.Status).HasMaxLength(16).IsRequired();
            entity.Property(fee => fee.WaiverReason).HasMaxLength(500);
            entity.Property(fee => fee.WaivedBy).HasMaxLength(256);
            entity.HasIndex(fee => new { fee.EmiScheduleId, fee.PenaltyDate }).IsUnique();
            entity.HasQueryFilter(fee => !fee.EmiSchedule!.LoanApplication!.IsDeleted);
        });
        modelBuilder.Entity<EmiPayment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Method).HasMaxLength(32).IsRequired();
            entity.Property(payment => payment.Reference).HasMaxLength(128);
            entity.HasIndex(payment => payment.ReceivedAt);
            entity.HasQueryFilter(payment => !payment.EmiSchedule!.LoanApplication!.IsDeleted);
        });
        modelBuilder.Entity<EmiPaymentAllocation>(entity =>
        {
            entity.HasKey(allocation => allocation.Id);
            entity.Property(allocation => allocation.Amount).HasPrecision(18, 2);
            entity.Property(allocation => allocation.Component).HasMaxLength(16).IsRequired();
            entity.HasOne(allocation => allocation.EmiSchedule).WithMany().HasForeignKey(allocation => allocation.EmiScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(allocation => allocation.EmiLateFee).WithMany(fee => fee.PaymentAllocations).HasForeignKey(allocation => allocation.EmiLateFeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(allocation => !allocation.EmiSchedule!.LoanApplication!.IsDeleted);
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
