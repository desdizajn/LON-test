using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.WMS;

public class Receipt : BaseEntity
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public Guid? PartnerId { get; set; }
    public virtual Partner? Partner { get; set; }
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public string? PurchaseOrderNumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public virtual ICollection<ReceiptLine> Lines { get; set; } = new List<ReceiptLine>();
}

public class ReceiptLine : BaseEntity
{
    public Guid ReceiptId { get; set; }
    public virtual Receipt Receipt { get; set; } = null!;
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public QualityStatus QualityStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
}

public class InventoryBalance : BaseEntity
{
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public Guid LocationId { get; set; }
    public virtual Location Location { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public QualityStatus QualityStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    public void AddQuantity(decimal qty)
    {
        if (qty < 0) throw new InvalidOperationException("Cannot add negative quantity");
        Quantity += qty;
    }
    
    public void SubtractQuantity(decimal qty)
    {
        if (qty < 0) throw new InvalidOperationException("Cannot subtract negative quantity");
        if (Quantity < qty) throw new InvalidOperationException("Insufficient inventory");
        Quantity -= qty;
    }
}

public class InventoryMovement : BaseEntity
{
    public string MovementNumber { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public MovementType Type { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public Guid? FromLocationId { get; set; }
    public virtual Location? FromLocation { get; set; }
    public Guid? ToLocationId { get; set; }
    public virtual Location? ToLocation { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public string? ReferenceNumber { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
}

public class Transfer : BaseEntity
{
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public Guid FromLocationId { get; set; }
    public virtual Location FromLocation { get; set; } = null!;
    public Guid ToLocationId { get; set; }
    public virtual Location ToLocation { get; set; } = null!;
    public string? Notes { get; set; }
    public virtual ICollection<TransferLine> Lines { get; set; } = new List<TransferLine>();
}

public class TransferLine : BaseEntity
{
    public Guid TransferId { get; set; }
    public virtual Transfer Transfer { get; set; } = null!;
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
}

public class CycleCount : BaseEntity
{
    public string CountNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public CycleCountStatus Status { get; set; }
    public string? Notes { get; set; }
    public virtual ICollection<CycleCountLine> Lines { get; set; } = new List<CycleCountLine>();
}

public class CycleCountLine : BaseEntity
{
    public Guid CycleCountId { get; set; }
    public virtual CycleCount CycleCount { get; set; } = null!;
    public Guid LocationId { get; set; }
    public virtual Location Location { get; set; } = null!;
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal? CountedQuantity { get; set; }
    public decimal? Variance => CountedQuantity.HasValue ? CountedQuantity.Value - SystemQuantity : null;
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
}

public class PickingWave : BaseEntity
{
    public string WaveNumber { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ICollection<PickTask> PickTasks { get; set; } = new List<PickTask>();
}

public class PickTask : BaseEntity
{
    public string TaskNumber { get; set; } = string.Empty;
    public Guid? WaveId { get; set; }
    public virtual PickingWave? Wave { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public Guid LocationId { get; set; }
    public virtual Location Location { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal QuantityToPick { get; set; }
    public decimal? QuantityPicked { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public PickTaskStatus Status { get; set; }
    public Guid? AssignedToEmployeeId { get; set; }
    public virtual Employee? AssignedToEmployee { get; set; }
    public DateTime? PickedDate { get; set; }
}

public class Shipment : BaseEntity
{
    public string ShipmentNumber { get; set; } = string.Empty;
    public DateTime ShipmentDate { get; set; }
    public Guid? CustomerId { get; set; }
    public virtual Partner? Customer { get; set; }
    public Guid? CarrierId { get; set; }
    public virtual Partner? Carrier { get; set; }
    public ShipmentStatus Status { get; set; }
    public string? TrackingNumber { get; set; }
    public string? SalesOrderNumber { get; set; }
    public virtual ICollection<ShipmentLine> Lines { get; set; } = new List<ShipmentLine>();
}

public class ShipmentLine : BaseEntity
{
    public Guid ShipmentId { get; set; }
    public virtual Shipment Shipment { get; set; } = null!;
    public int LineNumber { get; set; }
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;
    public Guid? CustomsDeclarationId { get; set; }
}
