using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class CustomsRegulationConfiguration : IEntityTypeConfiguration<CustomsRegulation>
{
    public void Configure(EntityTypeBuilder<CustomsRegulation> builder)
    {
        builder.ToTable("CustomsRegulations");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.CelexNumber)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.HasIndex(x => x.CelexNumber);
        
        builder.Property(x => x.TariffNumber)
            .HasMaxLength(10);
        
        builder.Property(x => x.DescriptionMK)
            .IsRequired()
            .HasColumnType("nvarchar(max)");  // Македонски опис може да биде многу долг (2200+ chars)
        
        builder.Property(x => x.DescriptionEN)
            .HasColumnType("nvarchar(max)");  // Англиски опис може да биде многу долг
        
        builder.Property(x => x.LegalBasis)
            .HasColumnType("nvarchar(max)");  // Правна основа може да биде многу долга (3400+ chars)
        
        builder.HasOne(x => x.TariffCode)
            .WithMany()
            .HasForeignKey(x => x.TariffCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
