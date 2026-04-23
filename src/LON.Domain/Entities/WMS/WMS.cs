using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.WMS;

public class Receipt : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }
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

public class ReceiptLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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
    public Guid? LocationId { get; set; }
    public virtual Location? Location { get; set; }
    public QualityStatus QualityStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
}

public class InventoryBalance : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

    /// <summary>
    /// LON business state (legacy `LagerMaterijali.Proces`). Nullable because
    /// non-LON inventory (domestic, regular import) doesn't carry a Proces
    /// value. Set to <see cref="Domain.Enums.LonProcessState.Imported"/> on
    /// Receipt, transitions on MaterialIssue / Shipment / WasteDeclaration.
    /// </summary>
    public LonProcessState? LonProcessState { get; set; }

    /// <summary>
    /// P15.8 — producer (sub-contractor) this batch has been distributed to.
    /// Legacy <c>LagerMaterijali.Proizvoditel</c>. Null = no producer assigned
    /// (material still at tenant's receiving dock or already shipped). When
    /// set, must reference a <see cref="Partner"/> row with
    /// <see cref="Domain.Enums.PartnerType.Producer"/>.
    /// </summary>
    public Guid? AssignedProducerId { get; set; }
    public virtual Partner? AssignedProducer { get; set; }

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

public class InventoryMovement : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

public class Transfer : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public Guid FromLocationId { get; set; }
    public virtual Location FromLocation { get; set; } = null!;
    public Guid ToLocationId { get; set; }
    public virtual Location ToLocation { get; set; } = null!;
    public string? Notes { get; set; }
    public virtual ICollection<TransferLine> Lines { get; set; } = new List<TransferLine>();
}

public class TransferLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

public class CycleCount : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public CycleCountStatus Status { get; set; }
    public string? Notes { get; set; }
    public virtual ICollection<CycleCountLine> Lines { get; set; } = new List<CycleCountLine>();
}

public class CycleCountLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

public class PickingWave : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string WaveNumber { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ICollection<PickTask> PickTasks { get; set; } = new List<PickTask>();
}

public class PickTask : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

public class Shipment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public DateTime ShipmentDate { get; set; }
    public Guid? CustomerId { get; set; }
    public virtual Partner? Customer { get; set; }
    public Guid? CarrierId { get; set; }
    public virtual Partner? Carrier { get; set; }
    public ShipmentStatus Status { get; set; }
    public string? TrackingNumber { get; set; }
    public string? SalesOrderNumber { get; set; }

    /// <summary>
    /// P15.9 — legacy <c>Ispratnici.VidUIS</c>. Customs regime marker that
    /// changes how the shipping document prints and which PEE XML envelope
    /// it belongs to. Typical values:
    ///   EXA3 — export of LON-processed goods (procedure 31 51).
    ///   VS7  — return of LON materials (procedure 61 21).
    ///   DOM  — domestic (non-customs) shipment.
    /// Null = regime not yet decided (pre-clearance draft).
    /// </summary>
    public string? ShipmentRegime { get; set; }

    /// <summary>
    /// P15.9 — legacy <c>Ispratnici.VrakanjeDaNe</c>. True when this shipment
    /// is a return of materials (VS7 regime); drives separate document layout
    /// and guarantee-credit path vs ordinary export.
    /// </summary>
    public bool IsReturn { get; set; }

    /// <summary>
    /// P15.9 — customs zaverka (certification) number stamped by the inspector.
    /// Null until the shipment is cleared by customs.
    /// </summary>
    public string? ZaverkaNumber { get; set; }
    public DateTime? ZaverkaDate { get; set; }

    public virtual ICollection<ShipmentLine> Lines { get; set; } = new List<ShipmentLine>();
}

public class ShipmentLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

/// <summary>
/// P15.3 — legacy <c>FakturiU5Skart</c>. Records the portion of a Receipt
/// line that turned out to be defective on intake and cannot enter
/// production. Semantically distinct from Otpad (manufacturing by-product):
/// Skart is an AVAILABILITY REDUCTION on an import invoice — the qty is
/// physically blocked at the receiving dock, never released to factories,
/// and usually leaves as a supplier return, scrappage, or discount claim.
///
/// <para>
/// Reporting a Skart transfers <see cref="SkartQuantity"/> from the OK
/// <see cref="InventoryBalance"/> at the receipt location into a Blocked
/// sibling at the same location, creating an <see cref="InventoryMovement"/>
/// of <see cref="MovementType.Adjustment"/> for audit. The Skart row itself
/// is the business-level record: operator reason, reporting time, and the
/// eventual <see cref="Resolution"/> once the supplier claim is settled.
/// </para>
/// </summary>
public class Skart : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    /// <summary>Auto-numbered SKT-yyyyMMdd-NNNN; unique per tenant.</summary>
    public string SkartNumber { get; set; } = string.Empty;

    /// <summary>Timestamp the operator pressed "Report skart".</summary>
    public DateTime ReportedAt { get; set; }

    public Guid ReceiptLineId { get; set; }
    public virtual ReceiptLine ReceiptLine { get; set; } = null!;

    // Denormalised snapshot for reporting without a join.
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }

    public decimal SkartQuantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure UoM { get; set; } = null!;

    /// <summary>Free-text reason (torn, wet, wrong colour, ...). Required.</summary>
    public string Reason { get; set; } = string.Empty;

    public SkartResolution Resolution { get; set; } = SkartResolution.Open;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
