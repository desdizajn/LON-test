using LON.Domain.Entities.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class DomainEventLogConfiguration : IEntityTypeConfiguration<DomainEventLog>
{
    public void Configure(EntityTypeBuilder<DomainEventLog> builder)
    {
        builder.ToTable("DomainEventLogs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
        builder.HasIndex(e => new { e.TenantId, e.EventType, e.OccurredAt });
        builder.HasIndex(e => e.EventId).IsUnique();
    }
}
