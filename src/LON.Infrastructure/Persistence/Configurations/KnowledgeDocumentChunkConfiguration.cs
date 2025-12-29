using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentChunkConfiguration : IEntityTypeConfiguration<KnowledgeDocumentChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocumentChunk> builder)
    {
        builder.ToTable("KnowledgeDocumentChunks");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.DocumentId)
            .IsRequired();
        
        builder.HasIndex(x => x.DocumentId);
        
        builder.Property(x => x.ChunkIndex)
            .IsRequired();
        
        builder.Property(x => x.Content)
            .IsRequired()
            .HasColumnType("nvarchar(max)");
        
        builder.Property(x => x.ChunkTitle)
            .HasMaxLength(200);
        
        builder.Property(x => x.Embedding)
            .HasColumnType("nvarchar(max)");  // JSON array [0.123, -0.456, ...]
        
        builder.Property(x => x.TokenCount)
            .IsRequired();
        
        builder.Property(x => x.Metadata)
            .HasColumnType("nvarchar(max)");
        
        // Composite index за брза навигација низ chunks
        builder.HasIndex(x => new { x.DocumentId, x.ChunkIndex });
    }
}
