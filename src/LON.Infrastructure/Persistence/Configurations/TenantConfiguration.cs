using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.LegacyUvoznik).HasMaxLength(50);
        builder.Property(e => e.TaxNumber).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.Country).HasMaxLength(3);
        builder.Property(e => e.ContactName).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.CustomsAuthorizationNumber).HasMaxLength(100);
        builder.Property(e => e.DefaultLanguage).IsRequired().HasMaxLength(5).HasDefaultValue("mk");

        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.LegacyUvoznik).IsUnique().HasFilter("[LegacyUvoznik] IS NOT NULL");
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
