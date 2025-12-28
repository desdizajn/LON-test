using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class CustomsProcedureConfiguration : IEntityTypeConfiguration<CustomsProcedure>
{
    public void Configure(EntityTypeBuilder<CustomsProcedure> builder)
    {
        builder.ToTable("CustomsProcedures");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.GuaranteePercentage).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CustomsDeclarationConfiguration : IEntityTypeConfiguration<CustomsDeclaration>
{
    public void Configure(EntityTypeBuilder<CustomsDeclaration> builder)
    {
        builder.ToTable("CustomsDeclarations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DeclarationNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.MRN).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.TotalCustomsValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TotalDuty).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TotalVAT).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TotalOtherCharges).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.DeclarationNumber).IsUnique();
        builder.HasIndex(e => e.MRN).IsUnique();
        builder.HasIndex(e => e.DeclarationDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CustomsDeclarationLineConfiguration : IEntityTypeConfiguration<CustomsDeclarationLine>
{
    public void Configure(EntityTypeBuilder<CustomsDeclarationLine> builder)
    {
        builder.ToTable("CustomsDeclarationLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.HSCode).HasMaxLength(20);
        builder.Property(e => e.CountryOfOrigin).HasMaxLength(3);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.CustomsValue).HasColumnType("decimal(18,4)");
        builder.Property(e => e.DutyRate).HasColumnType("decimal(18,4)");
        builder.Property(e => e.DutyAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.VATRate).HasColumnType("decimal(18,4)");
        builder.Property(e => e.VATAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.OtherCharges).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => new { e.CustomsDeclarationId, e.LineNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class MRNRegistryConfiguration : IEntityTypeConfiguration<MRNRegistry>
{
    public void Configure(EntityTypeBuilder<MRNRegistry> builder)
    {
        builder.ToTable("MRNRegistries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MRN).IsRequired().HasMaxLength(100);
        builder.Property(e => e.TotalQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.UsedQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.MRN).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
