using LON.Domain.Entities.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LON.Infrastructure.Persistence.Configurations;

public class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ToTable("ProductionOrders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.OrderQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ProducedQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ScrapQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.SalesOrderReference).HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(500);
        
        // Avoid cascade delete cycles
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BOM).WithMany().HasForeignKey(e => e.BOMId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Routing).WithMany().HasForeignKey(e => e.RoutingId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => e.OrderNumber).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.PlannedStartDate);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class BOMConfiguration : IEntityTypeConfiguration<BOM>
{
    public void Configure(EntityTypeBuilder<BOM> builder)
    {
        builder.ToTable("BOMs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.BaseQuantity).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.ItemId, e.Version }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class BOMLineConfiguration : IEntityTypeConfiguration<BOMLine>
{
    public void Configure(EntityTypeBuilder<BOMLine> builder)
    {
        builder.ToTable("BOMLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ScrapPercentage).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.BOMId, e.LineNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class MaterialIssueConfiguration : IEntityTypeConfiguration<MaterialIssue>
{
    public void Configure(EntityTypeBuilder<MaterialIssue> builder)
    {
        builder.ToTable("MaterialIssues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.IssueNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.BatchNumber).HasMaxLength(100);
        builder.Property(e => e.MRN).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => e.IssueNumber).IsUnique();
        builder.HasIndex(e => e.ProductionOrderId);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ProductionReceiptConfiguration : IEntityTypeConfiguration<ProductionReceipt>
{
    public void Configure(EntityTypeBuilder<ProductionReceipt> builder)
    {
        builder.ToTable("ProductionReceipts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
        builder.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ScrapQuantity).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => e.ReceiptNumber).IsUnique();
        builder.HasIndex(e => e.ProductionOrderId);
        builder.HasIndex(e => e.BatchNumber);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ProductionOrderMaterialConfiguration : IEntityTypeConfiguration<ProductionOrderMaterial>
{
    public void Configure(EntityTypeBuilder<ProductionOrderMaterial> builder)
    {
        builder.ToTable("ProductionOrderMaterials");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RequiredQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.IssuedQuantity).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ReservedQuantity).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UoM).WithMany().HasForeignKey(e => e.UoMId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.ProductionOrderId, e.LineNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ProductionOrderOperationConfiguration : IEntityTypeConfiguration<ProductionOrderOperation>
{
    public void Configure(EntityTypeBuilder<ProductionOrderOperation> builder)
    {
        builder.ToTable("ProductionOrderOperations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OperationCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StandardTimeMinutes).HasColumnType("decimal(18,4)");
        builder.Property(e => e.ActualTimeMinutes).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.WorkCenter).WithMany().HasForeignKey(e => e.WorkCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Machine).WithMany().HasForeignKey(e => e.MachineId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.ProductionOrderId, e.SequenceNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class RoutingConfiguration : IEntityTypeConfiguration<Routing>
{
    public void Configure(EntityTypeBuilder<Routing> builder)
    {
        builder.ToTable("Routings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        
        builder.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.ItemId, e.Version }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class RoutingOperationConfiguration : IEntityTypeConfiguration<RoutingOperation>
{
    public void Configure(EntityTypeBuilder<RoutingOperation> builder)
    {
        builder.ToTable("RoutingOperations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OperationCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StandardTimeMinutes).HasColumnType("decimal(18,4)");
        builder.Property(e => e.SetupTimeMinutes).HasColumnType("decimal(18,4)");
        
        builder.HasOne(e => e.WorkCenter).WithMany().HasForeignKey(e => e.WorkCenterId).OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(e => new { e.RoutingId, e.SequenceNumber }).IsUnique();
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
