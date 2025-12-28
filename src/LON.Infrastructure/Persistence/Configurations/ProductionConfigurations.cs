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
        
        builder.HasIndex(e => e.ReceiptNumber).IsUnique();
        builder.HasIndex(e => e.ProductionOrderId);
        builder.HasIndex(e => e.BatchNumber);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
