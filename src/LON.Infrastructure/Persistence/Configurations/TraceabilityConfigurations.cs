using LON.Domain.Entities.Traceability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class TraceLinkConfiguration : IEntityTypeConfiguration<TraceLink>
{
    public void Configure(EntityTypeBuilder<TraceLink> builder)
    {
        builder.ToTable("TraceLinks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SourceType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.SourceBatchNumber).HasMaxLength(100);
        builder.Property(e => e.SourceMRN).HasMaxLength(100);
        builder.Property(e => e.TargetType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.TargetBatchNumber).HasMaxLength(100);
        builder.Property(e => e.TargetMRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => new { e.SourceType, e.SourceId });
        builder.HasIndex(e => new { e.TargetType, e.TargetId });
        builder.HasIndex(e => e.SourceBatchNumber);
        builder.HasIndex(e => e.SourceMRN);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class BatchGenealogyConfiguration : IEntityTypeConfiguration<BatchGenealogy>
{
    public void Configure(EntityTypeBuilder<BatchGenealogy> builder)
    {
        builder.ToTable("BatchGenealogies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ParentBatches).HasMaxLength(2000);
        builder.Property(e => e.ParentMRNs).HasMaxLength(2000);
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.BatchNumber);
        builder.HasIndex(e => e.ItemId);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
