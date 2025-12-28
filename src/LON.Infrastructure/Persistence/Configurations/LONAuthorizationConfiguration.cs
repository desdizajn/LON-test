using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class LONAuthorizationConfiguration : IEntityTypeConfiguration<LONAuthorization>
{
    public void Configure(EntityTypeBuilder<LONAuthorization> builder)
    {
        builder.ToTable("LONAuthorizations");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.AuthorizationNumber)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(x => x.SystemType)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(x => x.OperationType)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(x => x.EconomicConditionCode)
            .HasMaxLength(10);
        
        builder.Property(x => x.GuaranteeAmount)
            .HasColumnType("decimal(18,2)");
        
        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);
        
        // FK to Partner
        builder.HasOne(x => x.Partner)
            .WithMany()
            .HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Child entities
        builder.HasMany(x => x.ApprovedItems)
            .WithOne(x => x.LONAuthorization)
            .HasForeignKey(x => x.LONAuthorizationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Индекси
        builder.HasIndex(x => x.AuthorizationNumber)
            .IsUnique()
            .HasDatabaseName("IX_LONAuthorizations_AuthorizationNumber");
        
        builder.HasIndex(x => x.PartnerId)
            .HasDatabaseName("IX_LONAuthorizations_PartnerId");
        
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_LONAuthorizations_Status");
    }
}

public class LONAuthorizationItemConfiguration : IEntityTypeConfiguration<LONAuthorizationItem>
{
    public void Configure(EntityTypeBuilder<LONAuthorizationItem> builder)
    {
        builder.ToTable("LONAuthorizationItems");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.ImportTariffCode)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(x => x.CompensatingTariffCode)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(x => x.YieldRate)
            .HasColumnType("decimal(18,4)");
        
        builder.Property(x => x.AllowedWastePercentage)
            .HasColumnType("decimal(5,2)");
        
        // FK to Import Item
        builder.HasOne(x => x.ImportItem)
            .WithMany()
            .HasForeignKey(x => x.ImportItemId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // FK to Compensating Product
        builder.HasOne(x => x.CompensatingProduct)
            .WithMany()
            .HasForeignKey(x => x.CompensatingProductId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Индекси
        builder.HasIndex(x => x.ImportItemId)
            .HasDatabaseName("IX_LONAuthorizationItems_ImportItemId");
        
        builder.HasIndex(x => x.CompensatingProductId)
            .HasDatabaseName("IX_LONAuthorizationItems_CompensatingProductId");
    }
}
