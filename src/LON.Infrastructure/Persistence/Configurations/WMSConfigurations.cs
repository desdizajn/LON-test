using LON.Domain.Entities.WMS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ToTable("InventoryBalances");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => new { e.ItemId, e.LocationId, e.BatchNumber, e.MRN, e.QualityStatus });
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MovementNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ReferenceNumber).HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        builder.HasIndex(e => e.MovementNumber);
        builder.HasIndex(e => e.MovementDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("Receipts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.PurchaseOrderNumber).HasMaxLength(50);
        builder.Property(e => e.ReferenceNumber).HasMaxLength(50);
        
        builder.HasIndex(e => e.ReceiptNumber).IsUnique();
        builder.HasIndex(e => e.ReceiptDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ReceiptLineConfiguration : IEntityTypeConfiguration<ReceiptLine>
{
    public void Configure(EntityTypeBuilder<ReceiptLine> builder)
    {
        builder.ToTable("ReceiptLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => new { e.ReceiptId, e.LineNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ShipmentNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.TrackingNumber).HasMaxLength(100);
        builder.Property(e => e.SalesOrderNumber).HasMaxLength(50);
        
        builder.HasIndex(e => e.ShipmentNumber).IsUnique();
        builder.HasIndex(e => e.ShipmentDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PickTaskConfiguration : IEntityTypeConfiguration<PickTask>
{
    public void Configure(EntityTypeBuilder<PickTask> builder)
    {
        builder.ToTable("PickTasks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TaskNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.QuantityToPick).HasColumnType("decimal(18,4)");
        builder.Property(e => e.QuantityPicked).HasColumnType("decimal(18,4)");
        
        builder.HasIndex(e => e.TaskNumber).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
