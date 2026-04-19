using LON.Domain.Entities.Importing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class ImportSessionConfiguration : IEntityTypeConfiguration<ImportSession>
{
    public void Configure(EntityTypeBuilder<ImportSession> builder)
    {
        builder.ToTable("ImportSessions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.TargetEntity).HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(1000);

        // JSON payloads (headers, rows grid, mapping, defaults, transforms).
        // SQL Server maps nvarchar(max) from default string config, but being
        // explicit keeps the intent obvious in the generated migration.
        builder.Property(e => e.HeadersJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(e => e.RowsJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(e => e.MappingJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.DefaultsJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.TransformsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
