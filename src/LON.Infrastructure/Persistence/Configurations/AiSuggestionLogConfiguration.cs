using LON.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phase 17 §E10 — EF mapping for the AI assistant recommendation log.
/// One row per recommendation surfaced; userActedOn flipped on user click /
/// dismissal. Tenant filter + audit columns are added automatically via the
/// ITenantScoped hook in <see cref="ApplicationDbContext.OnModelCreating"/>.
/// </summary>
public class AiSuggestionLogConfiguration : IEntityTypeConfiguration<AiSuggestionLog>
{
    public void Configure(EntityTypeBuilder<AiSuggestionLog> builder)
    {
        builder.ToTable("AiSuggestionLogs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(80);
        builder.Property(e => e.RecommendationCode).IsRequired().HasMaxLength(80);
        builder.Property(e => e.RecommendationTitle).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Severity).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ActionLink).HasMaxLength(500);
        builder.Property(e => e.UserActedBy).HasMaxLength(120);
        builder.Property(e => e.StructuredDataJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId });
        builder.HasIndex(e => new { e.TenantId, e.GeneratedAt });
        builder.HasIndex(e => new { e.TenantId, e.RecommendationCode, e.UserActedOn });
    }
}
