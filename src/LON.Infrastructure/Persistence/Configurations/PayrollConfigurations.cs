using LON.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("PayrollPeriods");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Notes).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.PeriodStart, x.PeriodEnd })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_PayrollPeriods_Tenant_Range");

        builder.HasMany(x => x.Lines)
            .WithOne(l => l.Period)
            .HasForeignKey(l => l.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayrollLineConfiguration : IEntityTypeConfiguration<PayrollLine>
{
    public void Configure(EntityTypeBuilder<PayrollLine> builder)
    {
        builder.ToTable("PayrollLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RegularHours).HasColumnType("decimal(18,4)");
        builder.Property(x => x.OvertimeHours).HasColumnType("decimal(18,4)");
        builder.Property(x => x.AbsenceHours).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BonusAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.DeductionAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.NetAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PeriodId, x.EmployeeId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_PayrollLines_Period_Employee");
    }
}
