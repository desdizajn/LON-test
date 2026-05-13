using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phase 17 §E1 — ClientOrder + ClientOrderFinishedGood EF mappings.
/// Tenant filter + soft-delete filter come from
/// <c>ApplicationDbContext.ConfigureTenantScoped&lt;T&gt;</c> reflection (universal
/// across <c>ITenantScoped</c> entities) — this file only owns shape +
/// constraints.
/// </summary>
public class ClientOrderConfiguration : IEntityTypeConfiguration<ClientOrder>
{
    public void Configure(EntityTypeBuilder<ClientOrder> builder)
    {
        builder.ToTable("ClientOrders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(x => x.CustomerOrderReference)
            .HasMaxLength(80);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(500);

        builder.Property(x => x.DeletedBy)
            .HasMaxLength(120);

        builder.Property(x => x.Status)
            .HasConversion<int>();

        // Per-tenant unique OrderNumber. Filtered on IsDeleted so soft-deleted
        // orders don't block reuse (unlikely in practice but cheap to keep).
        builder.HasIndex(x => new { x.TenantId, x.OrderNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => x.LONAuthorizationId);
        builder.HasIndex(x => x.CustomerPartnerId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.CustomerPartner)
            .WithMany()
            .HasForeignKey(x => x.CustomerPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LONAuthorization)
            .WithMany()
            .HasForeignKey(x => x.LONAuthorizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.FinishedGoods)
            .WithOne(g => g.ClientOrder!)
            .HasForeignKey(g => g.ClientOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Declarations)
            .WithOne(d => d.ClientOrder!)
            .HasForeignKey(d => d.ClientOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ClientOrderFinishedGoodConfiguration : IEntityTypeConfiguration<ClientOrderFinishedGood>
{
    public void Configure(EntityTypeBuilder<ClientOrderFinishedGood> builder)
    {
        builder.ToTable("ClientOrderFinishedGoods");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.UnitPriceForeign)
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasIndex(x => x.ClientOrderId);
        builder.HasIndex(x => x.ItemId);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UoM)
            .WithMany()
            .HasForeignKey(x => x.UoMId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BOM)
            .WithMany()
            .HasForeignKey(x => x.BOMId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
