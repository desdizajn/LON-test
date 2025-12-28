using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Production;

public class BOM : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public int Version { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public decimal BaseQuantity { get; set; }
    public virtual ICollection<BOMLine> Lines { get; set; } = new List<BOMLine>();
}

public class BOMLine : BaseEntity
{
    public Guid BOMId { get; set; }
    public virtual BOM BOM { get; set; } = null!;
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public decimal ScrapPercentage { get; set; }
}

public class Routing : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<RoutingOperation> Operations { get; set; } = new List<RoutingOperation>();
}

public class RoutingOperation : BaseEntity
{
    public Guid RoutingId { get; set; }
    public virtual Routing Routing { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public virtual WorkCenter WorkCenter { get; set; } = null!;
    public decimal StandardTimeMinutes { get; set; }
    public decimal SetupTimeMinutes { get; set; }
}

public class ProductionOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public decimal OrderQuantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal ScrapQuantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public ProductionOrderStatus Status { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public Guid? BOMId { get; set; }
    public virtual BOM? BOM { get; set; }
    public Guid? RoutingId { get; set; }
    public virtual Routing? Routing { get; set; }
    public string? SalesOrderReference { get; set; }
    public string? Notes { get; set; }
    public virtual ICollection<ProductionOrderMaterial> Materials { get; set; } = new List<ProductionOrderMaterial>();
    public virtual ICollection<ProductionOrderOperation> Operations { get; set; } = new List<ProductionOrderOperation>();
    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();
    public virtual ICollection<ProductionReceipt> ProductionReceipts { get; set; } = new List<ProductionReceipt>();
}

public class ProductionOrderMaterial : BaseEntity
{
    public Guid ProductionOrderId { get; set; }
    public virtual ProductionOrder ProductionOrder { get; set; } = null!;
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public decimal RequiredQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
}

public class ProductionOrderOperation : BaseEntity
{
    public Guid ProductionOrderId { get; set; }
    public virtual ProductionOrder ProductionOrder { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public virtual WorkCenter WorkCenter { get; set; } = null!;
    public Guid? MachineId { get; set; }
    public virtual Machine? Machine { get; set; }
    public decimal StandardTimeMinutes { get; set; }
    public decimal ActualTimeMinutes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class MaterialIssue : BaseEntity
{
    public string IssueNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public Guid ProductionOrderId { get; set; }
    public virtual ProductionOrder ProductionOrder { get; set; } = null!;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public Guid? IssuedByEmployeeId { get; set; }
    public virtual Employee? IssuedByEmployee { get; set; }
}

public class ProductionReceipt : BaseEntity
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public Guid ProductionOrderId { get; set; }
    public virtual ProductionOrder ProductionOrder { get; set; } = null!;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? ScrapQuantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public Guid LocationId { get; set; }
    public virtual Location Location { get; set; } = null!;
    public QualityStatus QualityStatus { get; set; }
    public Guid? ReceivedByEmployeeId { get; set; }
    public virtual Employee? ReceivedByEmployee { get; set; }
}
