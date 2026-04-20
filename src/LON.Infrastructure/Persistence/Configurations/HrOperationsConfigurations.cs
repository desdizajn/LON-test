using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>P10.1 — AttendanceRecord table.</summary>
public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Hours).HasColumnType("decimal(6,2)");
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (Tenant, Employee, Date) — filtered unique.
        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Date })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.Date });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>P10.2 — Absence table.</summary>
public class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> builder)
    {
        builder.ToTable("Absences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.From).IsRequired();
        builder.Property(e => e.To).IsRequired();
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.From });
        builder.HasIndex(e => new { e.TenantId, e.Approved });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>P10.5 — OperatorMachineAssignment table.</summary>
public class OperatorMachineAssignmentConfiguration : IEntityTypeConfiguration<OperatorMachineAssignment>
{
    public void Configure(EntityTypeBuilder<OperatorMachineAssignment> builder)
    {
        builder.ToTable("OperatorMachineAssignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Machine)
            .WithMany()
            .HasForeignKey(e => e.MachineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.ValidFrom });
        builder.HasIndex(e => new { e.TenantId, e.MachineId, e.ValidFrom });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
