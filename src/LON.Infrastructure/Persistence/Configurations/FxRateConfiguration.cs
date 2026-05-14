using LON.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
{
    public void Configure(EntityTypeBuilder<FxRate> builder)
    {
        builder.ToTable("FxRates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromCurrency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.ToCurrency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.Rate).HasColumnType("decimal(18,8)");
        builder.Property(e => e.Source).HasConversion<int>();
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => new { e.TenantId, e.FromCurrency, e.ToCurrency, e.EffectiveDate })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.FromCurrency, e.ToCurrency, e.EffectiveDate })
            .HasDatabaseName("IX_FxRates_Lookup");
    }
}
