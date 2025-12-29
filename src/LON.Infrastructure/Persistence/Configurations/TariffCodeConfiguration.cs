using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class TariffCodeConfiguration : IEntityTypeConfiguration<TariffCode>
{
    public void Configure(EntityTypeBuilder<TariffCode> builder)
    {
        builder.ToTable("TariffCodes");
        
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.TariffNumber)
            .IsRequired()
            .HasMaxLength(10);
        
        builder.HasIndex(x => x.TariffNumber)
            .IsUnique();
        
        builder.Property(x => x.TARBR)
            .HasMaxLength(4);  // 4 цифри (прва група)
        
        builder.Property(x => x.TAROZ1)
            .HasMaxLength(2);  // 2 цифри (втора група)
        
        builder.Property(x => x.TAROZ2)
            .HasMaxLength(2);  // 2 цифри (трета група)
        
        builder.Property(x => x.TAROZ3)
            .HasMaxLength(2);  // 2 цифри (четврта група)
        
        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);
        
        builder.Property(x => x.CustomsRate)
            .HasPrecision(5, 2);
        
        builder.Property(x => x.VATRate)
            .HasPrecision(5, 2);
        
        builder.Property(x => x.UnitMeasure)
            .HasMaxLength(20);
        
        builder.Property(x => x.FI)
            .HasMaxLength(10);
        
        builder.Property(x => x.FU)
            .HasMaxLength(10);
        
        builder.Property(x => x.PV)
            .HasMaxLength(1000);
    }
}
