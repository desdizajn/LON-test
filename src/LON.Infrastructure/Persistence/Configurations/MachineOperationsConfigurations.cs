using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>P11.1 — MachineStateEvent table.</summary>
public class MachineStateEventConfiguration : IEntityTypeConfiguration<MachineStateEvent>
{
    public void Configure(EntityTypeBuilder<MachineStateEvent> builder)
    {
        builder.ToTable("MachineStateEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.State).IsRequired();
        builder.Property(e => e.ChangedAt).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasOne(e => e.Machine)
            .WithMany()
            .HasForeignKey(e => e.MachineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ChangedByEmployee)
            .WithMany()
            .HasForeignKey(e => e.ChangedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.MachineId, e.ChangedAt });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>P11.2 — DowntimeEvent table.</summary>
public class DowntimeEventConfiguration : IEntityTypeConfiguration<DowntimeEvent>
{
    public void Configure(EntityTypeBuilder<DowntimeEvent> builder)
    {
        builder.ToTable("DowntimeEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Category).IsRequired();
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Start).IsRequired();
        builder.Property(e => e.DurationMinutes).HasColumnType("decimal(18,2)");
        builder.Property(e => e.CostImpact).HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.Machine)
            .WithMany()
            .HasForeignKey(e => e.MachineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReportedByEmployee)
            .WithMany()
            .HasForeignKey(e => e.ReportedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.MachineId, e.Start });
        builder.HasIndex(e => new { e.TenantId, e.End });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>P11.4 — MaintenanceSchedule table.</summary>
public class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ToTable("MaintenanceSchedules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TaskDescription).IsRequired().HasMaxLength(500);
        builder.Property(e => e.IntervalDays).IsRequired();
        builder.Property(e => e.NextDue).IsRequired();

        builder.HasOne(e => e.Machine)
            .WithMany()
            .HasForeignKey(e => e.MachineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.NextDue });
        builder.HasIndex(e => new { e.TenantId, e.MachineId });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>P11.5 — MaintenanceWorkOrder table.</summary>
public class MaintenanceWorkOrderConfiguration : IEntityTypeConfiguration<MaintenanceWorkOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceWorkOrder> builder)
    {
        builder.ToTable("MaintenanceWorkOrders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ScheduledDate).IsRequired();
        builder.Property(e => e.TaskDescription).HasMaxLength(500);
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.CostImpact).HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.Machine)
            .WithMany()
            .HasForeignKey(e => e.MachineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Schedule)
            .WithMany(s => s.WorkOrders)
            .HasForeignKey(e => e.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.TechnicianEmployee)
            .WithMany()
            .HasForeignKey(e => e.TechnicianEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.MachineId, e.ScheduledDate });
        builder.HasIndex(e => new { e.TenantId, e.CompletedAt });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
