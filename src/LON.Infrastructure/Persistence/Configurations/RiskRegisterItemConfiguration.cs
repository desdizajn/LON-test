using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class RiskRegisterItemConfiguration : IEntityTypeConfiguration<RiskRegisterItem>
{
    public void Configure(EntityTypeBuilder<RiskRegisterItem> builder)
    {
        builder.ToTable("RiskRegisterItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.Severity).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Category).HasMaxLength(60);
        builder.Property(x => x.Owner).HasMaxLength(120);
        builder.Property(x => x.Mitigation).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Resolution).HasColumnType("nvarchar(max)");

        // Tenant FK + TenantId auto-wired by ApplicationDbContext.OnModelCreating
        // via ITenantScoped marker. The global query filter is added there too.

        builder.HasIndex(x => new { x.TenantId, x.Kind, x.Status })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_RiskRegisterItems_TenantId_Kind_Status");

        builder.HasIndex(x => x.DueDate)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_RiskRegisterItems_DueDate");
    }
}
