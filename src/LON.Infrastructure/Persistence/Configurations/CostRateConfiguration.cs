using LON.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class CostRateConfiguration : IEntityTypeConfiguration<CostRate>
{
    public void Configure(EntityTypeBuilder<CostRate> builder)
    {
        builder.ToTable("CostRates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Scope).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Notes).HasColumnType("nvarchar(max)");

        builder.Property(x => x.CostPerHour).HasColumnType("decimal(18,4)");
        builder.Property(x => x.CostPerUnit).HasColumnType("decimal(18,4)");

        builder.HasIndex(x => new { x.TenantId, x.Scope, x.ScopeId })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_CostRates_TenantId_Scope_ScopeId");

        builder.HasIndex(x => x.ValidFrom)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_CostRates_ValidFrom");
    }
}
