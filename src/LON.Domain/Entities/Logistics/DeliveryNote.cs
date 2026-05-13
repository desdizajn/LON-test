using LON.Domain.Common;

namespace LON.Domain.Entities.Logistics;

/// <summary>
/// Phase 17 §E7.6 (D5 decision 2026-05-12) — polymorphic delivery-note entity
/// modelled after legacy ELON `Propratnici` + `PropratniciStavki` (1,658
/// headers + 295,918 lines). Replaces the ad-hoc "Generate Propratnica PDF"
/// rendering mentioned in earlier BLUEPRINT drafts with a first-class entity
/// that survives across the three flows below.
/// </summary>
public enum DeliveryNoteType
{
    /// <summary>Goods leaving HQ → sub-contractor producer. Paired with `MaterialIssue.Id`.</summary>
    ProducerDispatch = 1,

    /// <summary>Finished goods returning from producer → HQ. Paired with `Shipment.Id` (Type=ProducerReturn).</summary>
    ProducerReturn = 2,

    /// <summary>Finished goods leaving HQ → customer. Paired with `Shipment.Id` (Type=Export).</summary>
    CustomerShipment = 3,
}

/// <summary>
/// Lifecycle of a single DeliveryNote.
/// Draft → Sent on user confirmation; Cancelled only reachable from Draft.
/// (Confirmed = Sent + signed handover; modeled as same status today,
/// distinct ledger when E13 audit log lands.)
/// </summary>
public enum DeliveryNoteStatus
{
    Draft = 1,
    Sent = 2,
    Confirmed = 3,
    Cancelled = 4,
}

/// <summary>
/// Physical cover-sheet that accompanies goods between sites (HQ ↔ producer,
/// HQ → customer). Polymorphic via <see cref="DocumentType"/>: a single table
/// keeps the three legacy `Propratnici` variants in one place so reports
/// don't have to UNION across siblings.
///
/// Auto-generated in `Draft` status when the related document
/// (`MaterialIssue` / `Shipment`) commits; user reviews → adds driver / vehicle
/// → confirms → status flips to `Sent` and the cover-sheet PDF generates.
/// </summary>
public class DeliveryNote : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// SEQUENCE-backed identifier, formatted via
    /// <see cref="LON.Domain.Common.NumberFormatter.DeliveryNote(int,long)"/>
    /// → `DN-{year:0000}-{seq:D6}`. Unique within tenant.
    /// </summary>
    public string Number { get; set; } = string.Empty;

    public DeliveryNoteType DocumentType { get; set; }

    /// <summary>
    /// Polymorphic FK to the related entity.
    /// - <see cref="DeliveryNoteType.ProducerDispatch"/> → `MaterialIssue.Id`
    /// - <see cref="DeliveryNoteType.ProducerReturn"/> → `Shipment.Id`
    /// - <see cref="DeliveryNoteType.CustomerShipment"/> → `Shipment.Id`
    /// </summary>
    public Guid RelatedDocumentId { get; set; }

    public DateTime DispatchDate { get; set; }

    /// <summary>Origin location (warehouse → producer transport leaves from here).</summary>
    public Guid FromLocationId { get; set; }

    /// <summary>Destination internal location (when goods move HQ → HQ-sub-warehouse).</summary>
    public Guid? ToLocationId { get; set; }

    /// <summary>Destination external partner (producer or customer).</summary>
    public Guid? ToPartnerId { get; set; }

    public string? DriverName { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? Remarks { get; set; }

    public DeliveryNoteStatus Status { get; set; } = DeliveryNoteStatus.Draft;

    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public virtual ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
}

public class DeliveryNoteLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public virtual DeliveryNote DeliveryNote { get; set; } = null!;

    public Guid ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public string? Notes { get; set; }
}
