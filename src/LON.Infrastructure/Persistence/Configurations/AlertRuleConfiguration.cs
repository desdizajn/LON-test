using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phase 17 §E10.5 — EF mappings for AlertRule + AlertEvent.
/// </summary>
public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("AlertRules");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(80);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.NameMk).IsRequired().HasMaxLength(200);
        builder.Property(e => e.DeliveryChannels).IsRequired().HasMaxLength(100);
        builder.Property(e => e.RecipientsJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Threshold).HasColumnType("decimal(18,4)");

        builder.Property(e => e.Severity).HasConversion<int>();
        builder.Property(e => e.TriggerKind).HasConversion<int>();

        builder.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.IsActive, e.TriggerKind });
    }
}

public class AlertEventConfiguration : IEntityTypeConfiguration<AlertEvent>
{
    public void Configure(EntityTypeBuilder<AlertEvent> builder)
    {
        builder.ToTable("AlertEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(80);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Body).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.DedupKey).IsRequired().HasMaxLength(300);

        builder.Property(e => e.AcknowledgedBy).HasMaxLength(120);
        builder.Property(e => e.AcknowledgedReason).HasMaxLength(500);
        builder.Property(e => e.ResolvedBy).HasMaxLength(120);
        builder.Property(e => e.ResolvedReason).HasMaxLength(500);

        builder.Property(e => e.Severity).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();

        builder.HasIndex(e => new { e.TenantId, e.Status, e.OccurredAt });
        builder.HasIndex(e => new { e.TenantId, e.DedupKey, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.AlertRuleId });

        builder.HasOne(e => e.AlertRule)
            .WithMany()
            .HasForeignKey(e => e.AlertRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
