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
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
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
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FromLocation).WithMany().HasForeignKey(e => e.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ToLocation).WithMany().HasForeignKey(e => e.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
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
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Partner).WithMany().HasForeignKey(e => e.PartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        
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
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
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
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Carrier).WithMany().HasForeignKey(e => e.CarrierId).OnDelete(DeleteBehavior.Restrict);
        
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
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => e.TaskNumber).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ShipmentLineConfiguration : IEntityTypeConfiguration<ShipmentLine>
{
    public void Configure(EntityTypeBuilder<ShipmentLine> builder)
    {
        builder.ToTable("ShipmentLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.ShipmentId, e.LineNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TransferNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.FromLocation).WithMany().HasForeignKey(e => e.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ToLocation).WithMany().HasForeignKey(e => e.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => e.TransferNumber).IsUnique();
        builder.HasIndex(e => e.TransferDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class TransferLineConfiguration : IEntityTypeConfiguration<TransferLine>
{
    public void Configure(EntityTypeBuilder<TransferLine> builder)
    {
        builder.ToTable("TransferLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.TransferId, e.LineNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PickingWaveConfiguration : IEntityTypeConfiguration<PickingWave>
{
    public void Configure(EntityTypeBuilder<PickingWave> builder)
    {
        builder.ToTable("PickingWaves");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WaveNumber).IsRequired().HasMaxLength(50);
        
        // Avoid cascade delete cycles on SQL Server
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => e.WaveNumber).IsUnique();
        builder.HasIndex(e => e.CreatedDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CycleCountConfiguration : IEntityTypeConfiguration<CycleCount>
{
    public void Configure(EntityTypeBuilder<CycleCount> builder)
    {
        builder.ToTable("CycleCounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CountNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        // CycleCount belongs to Warehouse - cascade delete is appropriate
        builder.HasOne(e => e.Warehouse).WithMany().HasForeignKey(e => e.WarehouseId).OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(e => e.CountNumber).IsUnique();
        builder.HasIndex(e => e.ScheduledDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CycleCountLineConfiguration : IEntityTypeConfiguration<CycleCountLine>
{
    public void Configure(EntityTypeBuilder<CycleCountLine> builder)
    {
        builder.ToTable("CycleCountLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.SystemQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.CountedQuantity).HasColumnType("decimal(18,4)");
        
        // Cascade from CycleCount parent only
        builder.HasOne(e => e.CycleCount).WithMany(c => c.Lines).HasForeignKey(e => e.CycleCountId).OnDelete(DeleteBehavior.Cascade);
        // Restrict for all master data references to avoid cycles
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
