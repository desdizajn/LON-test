using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class CodeListItemConfiguration : IEntityTypeConfiguration<CodeListItem>
{
    public void Configure(EntityTypeBuilder<CodeListItem> builder)
    {
        builder.ToTable("CodeListItems");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.ListType)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(x => x.DescriptionMK)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(x => x.DescriptionEN)
            .HasMaxLength(500);
        
        builder.Property(x => x.BoxNumber)
            .HasMaxLength(10);
        
        builder.Property(x => x.Tooltip)
            .HasMaxLength(1000);
        
        builder.Property(x => x.ParentCode)
            .HasMaxLength(50);
        
        builder.Property(x => x.AdditionalData)
            .HasColumnType("nvarchar(max)");
        
        // Индекси
        builder.HasIndex(x => new { x.ListType, x.Code })
            .IsUnique()
            .HasDatabaseName("IX_CodeListItems_ListType_Code");
        
        builder.HasIndex(x => x.BoxNumber)
            .HasDatabaseName("IX_CodeListItems_BoxNumber");
        
        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_CodeListItems_IsActive");
    }
}
