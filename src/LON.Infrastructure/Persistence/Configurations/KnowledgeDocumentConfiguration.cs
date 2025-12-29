using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("KnowledgeDocuments");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(x => x.DocumentType);
        
        builder.Property(x => x.TitleEN)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(x => x.TitleMK)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(x => x.Reference)
            .HasMaxLength(100);
        
        builder.HasIndex(x => x.Reference);
        
        builder.Property(x => x.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
        
        builder.Property(x => x.Metadata)
            .HasColumnType("nvarchar(max)");
        
        builder.Property(x => x.SourceUrl)
            .HasMaxLength(500);
        
        builder.Property(x => x.Version)
            .HasMaxLength(20);
        
        builder.Property(x => x.Language)
            .IsRequired()
            .HasMaxLength(2);
        
        builder.HasIndex(x => x.Language);
        
        // Navigation
        builder.HasMany(x => x.Chunks)
            .WithOne(x => x.Document)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
