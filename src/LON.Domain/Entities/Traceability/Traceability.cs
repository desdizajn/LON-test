using LON.Domain.Common;
using LON.Domain.Entities.MasterData;

namespace LON.Domain.Entities.Traceability;

public class TraceLink : BaseEntity
{
    public string SourceType { get; set; } = string.Empty; // "Receipt", "MaterialIssue", "ProductionReceipt", "Shipment"
    public Guid SourceId { get; set; }
    public string? SourceBatchNumber { get; set; }
    public string? SourceMRN { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string? TargetBatchNumber { get; set; }
    public string? TargetMRN { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public decimal Quantity { get; set; }
    public DateTime LinkDate { get; set; }
}

public class BatchGenealogy : BaseEntity
{
    public string BatchNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public string? ParentBatches { get; set; } // JSON array of parent batch numbers
    public string? ParentMRNs { get; set; } // JSON array of parent MRNs
    public string? Notes { get; set; }
}
