using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phase 17 §E8.5 (D4) — EF mapping for CommercialInvoice + CommercialInvoiceLine.
/// TenantId + tenant query filter come automatically via the ITenantScoped
/// hook in <see cref="ApplicationDbContext.OnModelCreating"/>; this file
/// configures the non-tenant bits (column types, indexes, FKs, soft-delete).
/// </summary>
public class CommercialInvoiceConfiguration : IEntityTypeConfiguration<CommercialInvoice>
{
    public void Configure(EntityTypeBuilder<CommercialInvoice> builder)
    {
        builder.ToTable("CommercialInvoices");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Number).IsRequired().HasMaxLength(40);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.Incoterms).IsRequired().HasMaxLength(10);
        builder.Property(e => e.PaymentTerms).HasMaxLength(200);
        builder.Property(e => e.CountryOfDestination).HasMaxLength(2);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.CancellationReason).HasMaxLength(500);
        builder.Property(e => e.IssuedBy).HasMaxLength(120);
        builder.Property(e => e.CancelledBy).HasMaxLength(120);
        builder.Property(e => e.DeletedBy).HasMaxLength(120);

        builder.Property(e => e.Subtotal).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TaxAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");

        builder.Property(e => e.Status).HasConversion<int>();

        // Number must be unique within tenant (SEQUENCE keeps it monotonic, the
        // constraint catches any future SQL-side bug).
        builder.HasIndex(e => new { e.TenantId, e.Number })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Hub-tab queries: „all CIs for this ClientOrder", „by date range".
        builder.HasIndex(e => new { e.TenantId, e.ClientOrderId });
        builder.HasIndex(e => new { e.TenantId, e.InvoiceDate });
        builder.HasIndex(e => e.ShipmentId);
        builder.HasIndex(e => e.CustomsDeclarationId);

        builder.HasOne(e => e.ClientOrder)
            .WithMany()
            .HasForeignKey(e => e.ClientOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Shipment)
            .WithMany()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CustomsDeclaration)
            .WithMany()
            .HasForeignKey(e => e.CustomsDeclarationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ConsigneePartner)
            .WithMany()
            .HasForeignKey(e => e.ConsigneePartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ConsignorPartner)
            .WithMany()
            .HasForeignKey(e => e.ConsignorPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Lines)
            .WithOne(l => l.CommercialInvoice)
            .HasForeignKey(l => l.CommercialInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CommercialInvoiceLineConfiguration : IEntityTypeConfiguration<CommercialInvoiceLine>
{
    public void Configure(EntityTypeBuilder<CommercialInvoiceLine> builder)
    {
        builder.ToTable("CommercialInvoiceLines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
        builder.Property(e => e.LineTotal).HasColumnType("decimal(18,4)");
        builder.Property(e => e.CountryOfOrigin).HasMaxLength(2);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => e.CommercialInvoiceId);
        builder.HasIndex(e => e.ItemId);

        builder.HasOne(e => e.Item)
            .WithMany()
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UoM)
            .WithMany()
            .HasForeignKey(e => e.UoMId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
