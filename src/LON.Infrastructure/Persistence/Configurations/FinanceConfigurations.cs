using LON.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>P12.3 — ClientContract table.</summary>
public class ClientContractConfiguration : IEntityTypeConfiguration<ClientContract>
{
    public void Configure(EntityTypeBuilder<ClientContract> builder)
    {
        builder.ToTable("ClientContracts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Number).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.HasOne(e => e.Partner)
            .WithMany()
            .HasForeignKey(e => e.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Contract number unique per tenant among non-deleted rows.
        builder.HasIndex(e => new { e.TenantId, e.Number })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.PartnerId });
        builder.HasIndex(e => new { e.TenantId, e.IsActive });
        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.RateCardEntries);
    }
}

/// <summary>P12.3 — RateCardEntry table.</summary>
public class RateCardEntryConfiguration : IEntityTypeConfiguration<RateCardEntry>
{
    public void Configure(EntityTypeBuilder<RateCardEntry> builder)
    {
        builder.ToTable("RateCardEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RateType).IsRequired();
        builder.Property(e => e.RatePerUnit).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.OperationCode).HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasOne(e => e.Contract)
            .WithMany(c => c.RateCardEntries)
            .HasForeignKey(e => e.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Item)
            .WithMany()
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.ContractId });
        builder.HasIndex(e => new { e.TenantId, e.ItemId });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>P12.2 — Invoice table.</summary>
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Number).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.SubTotal).HasColumnType("decimal(18,4)");
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.IssueDate).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.HasOne(e => e.Partner)
            .WithMany()
            .HasForeignKey(e => e.PartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Contract)
            .WithMany()
            .HasForeignKey(e => e.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        // Invoice number unique per tenant among non-deleted rows (drafts
        // carry a provisional number; it's committed on Issue).
        builder.HasIndex(e => new { e.TenantId, e.Number })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.PartnerId });
        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.IssueDate });
        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.Ignore(e => e.Lines);
    }
}

/// <summary>P12.2 — InvoiceLine table.</summary>
public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
        builder.Property(e => e.LineTotal).HasColumnType("decimal(18,4)");

        builder.HasOne(e => e.Invoice)
            .WithMany(i => i.Lines)
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Item)
            .WithMany()
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RelatedProductionOrder)
            .WithMany()
            .HasForeignKey(e => e.RelatedProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RelatedShipment)
            .WithMany()
            .HasForeignKey(e => e.RelatedShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.InvoiceId, e.LineNumber });
        builder.HasIndex(e => new { e.TenantId, e.RelatedProductionOrderId });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
