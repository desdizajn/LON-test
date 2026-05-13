using LON.Domain.Entities.Logistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

/// <summary>
/// Phase 17 §E7.6 (D5) — EF mapping for DeliveryNote + DeliveryNoteLine.
/// TenantId + tenant query filter come automatically via the ITenantScoped
/// hook in <see cref="ApplicationDbContext.OnModelCreating"/>; this file
/// configures the non-tenant bits (column types, indexes, FKs, soft-delete).
/// </summary>
public class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable("DeliveryNotes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Number).IsRequired().HasMaxLength(40);
        builder.Property(e => e.DriverName).HasMaxLength(120);
        builder.Property(e => e.VehicleRegistration).HasMaxLength(40);
        builder.Property(e => e.Remarks).HasMaxLength(1000);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Property(e => e.DocumentType).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();

        // Number must be unique within a tenant — same SEQUENCE keeps the
        // counter monotonic; the constraint catches any future Sql-side bug.
        builder.HasIndex(e => new { e.TenantId, e.Number })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Querying „all dispatches in past 30 days" hits this index.
        builder.HasIndex(e => new { e.TenantId, e.DocumentType, e.DispatchDate });

        // Polymorphic lookup: „give me the DN for MaterialIssue X" / „for
        // Shipment Y". No DB-level FK because the target table varies; we
        // just index for speed.
        builder.HasIndex(e => e.RelatedDocumentId);

        builder.HasMany(e => e.Lines)
            .WithOne(l => l.DeliveryNote)
            .HasForeignKey(l => l.DeliveryNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class DeliveryNoteLineConfiguration : IEntityTypeConfiguration<DeliveryNoteLine>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteLine> builder)
    {
        builder.ToTable("DeliveryNoteLines");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.BatchNumber).HasMaxLength(60);
        builder.Property(e => e.MRN).HasMaxLength(40);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => e.DeliveryNoteId);
        builder.HasIndex(e => e.ItemId);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
