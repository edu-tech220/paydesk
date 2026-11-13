using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Domain.Entities;

namespace PayDesk.Api.Data;

public class PayDeskDbContext : DbContext
{
    public PayDeskDbContext(DbContextOptions<PayDeskDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<TaxBracket> TaxBrackets => Set<TaxBracket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.EmployeeCode).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayPeriod>(entity =>
        {
            entity.Property(p => p.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<TimesheetEntry>(entity =>
        {
            entity.Property(t => t.HoursWorked).HasColumnType("decimal(5,2)");
            entity.Property(t => t.Version).IsConcurrencyToken();
            entity.HasIndex(t => new { t.EmployeeId, t.WorkDate }).IsUnique();

            entity.HasOne(t => t.Employee)
                .WithMany(e => e.TimesheetEntries)
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.PayPeriod)
                .WithMany(p => p.TimesheetEntries)
                .HasForeignKey(t => t.PayPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ApprovedByEmployee)
                .WithMany()
                .HasForeignKey(t => t.ApprovedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayrollRun>(entity =>
        {
            entity.HasIndex(r => r.PayPeriodId).IsUnique();
            entity.Property(r => r.TotalGrossPay).HasColumnType("decimal(12,2)");
            entity.Property(r => r.TotalTaxAmount).HasColumnType("decimal(12,2)");
            entity.Property(r => r.TotalNetPay).HasColumnType("decimal(12,2)");

            entity.HasOne(r => r.PayPeriod)
                .WithOne(p => p.PayrollRun)
                .HasForeignKey<PayrollRun>(r => r.PayPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payslip>(entity =>
        {
            entity.Property(s => s.RegularHours).HasColumnType("decimal(7,2)");
            entity.Property(s => s.OvertimeHours).HasColumnType("decimal(7,2)");
            entity.Property(s => s.RegularPay).HasColumnType("decimal(12,2)");
            entity.Property(s => s.OvertimePay).HasColumnType("decimal(12,2)");
            entity.Property(s => s.GrossPay).HasColumnType("decimal(12,2)");
            entity.Property(s => s.TaxAmount).HasColumnType("decimal(12,2)");
            entity.Property(s => s.NetPay).HasColumnType("decimal(12,2)");
            entity.HasIndex(s => new { s.PayrollRunId, s.EmployeeId }).IsUnique();

            entity.HasOne(s => s.PayrollRun)
                .WithMany(r => r.Payslips)
                .HasForeignKey(s => s.PayrollRunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Employee)
                .WithMany()
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaxBracket>(entity =>
        {
            entity.Property(b => b.MinAmount).HasColumnType("decimal(12,2)");
            entity.Property(b => b.MaxAmount).HasColumnType("decimal(12,2)");
            entity.Property(b => b.Rate).HasColumnType("decimal(5,4)");
        });
    }

    public override int SaveChanges()
    {
        BumpConcurrencyVersions();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        BumpConcurrencyVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void BumpConcurrencyVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyVersioned>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.Version).OriginalValue = entry.Entity.Version;
                entry.Entity.Version++;
            }
        }
    }
}
