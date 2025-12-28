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
            .HasMaxLength(1000);
        
        builder.Property(x => x.DescriptionEN)
            .HasMaxLength(1000);
        
        builder.Property(x => x.LegalBasis)
            .HasMaxLength(500);
        
        builder.HasOne(x => x.TariffCode)
            .WithMany()
            .HasForeignKey(x => x.TariffCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
