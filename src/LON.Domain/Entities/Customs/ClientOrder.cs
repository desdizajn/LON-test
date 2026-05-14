using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Customs;

/// <summary>
/// Phase 17 §E1 — top-level customer-order aggregate (Zaklucok analog in legacy ELON).
///
/// Per BLUEPRINT §3.1: ClientOrder is the hub that ties together:
/// customer order intent → CustomsDeclaration(s) → ProductionOrder(s) →
/// Shipment(s) → Razdolzuvanje. All linked entities carry a nullable
/// ClientOrderId FK back to here.
///
/// Numbering: SQL SEQUENCE `seq_ClientOrder_&lt;tenantId&gt;` produces the
/// per-tenant sequence; <see cref="LON.Domain.Common.NumberFormatter"/>
/// stamps `CO-{year}-{seq:D6}` as <see cref="OrderNumber"/>.
///
/// Soft-delete: `BaseEntity.IsDeleted` + `DeletedAt` / `DeletedBy` on this entity.
/// Cascading soft-delete behavior (cancel cascades to linked declarations etc.)
/// lands with §E14.
/// </summary>
public class ClientOrder : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    public Guid TenantId { get; set; }

    /// <summary>Per-tenant sequential number, formatted as `CO-{year}-{seq:D6}`.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>FK to Partner with type=Customer. Required.</summary>
    public Guid CustomerPartnerId { get; set; }
    public virtual Partner? CustomerPartner { get; set; }

    /// <summary>FK to LONAuthorization. REQUIRED — inward-processing customer orders are always under a LON.</summary>
    public Guid LONAuthorizationId { get; set; }
    public virtual LONAuthorization? LONAuthorization { get; set; }

    /// <summary>Customer's own internal order reference (their PO number, etc.).</summary>
    public string? CustomerOrderReference { get; set; }

    public DateTime OrderDate { get; set; }

    /// <summary>Desired ship-by date (optional; warns in UI if at risk).</summary>
    public DateTime? RequestedShipDate { get; set; }

    public ClientOrderStatus Status { get; set; } = ClientOrderStatus.Draft;

    /// <summary>Free-text notes from manager / sales.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional reason given when the order was cancelled. Required when Status transitions to Cancelled.</summary>
    public string? CancellationReason { get; set; }

    // Soft-delete extension fields (BaseEntity already has IsDeleted bool).
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Backrefs — populated on demand by handlers; not eagerly loaded.
    public virtual ICollection<ClientOrderFinishedGood> FinishedGoods { get; set; } = new List<ClientOrderFinishedGood>();
    public virtual ICollection<CustomsDeclaration> Declarations { get; set; } = new List<CustomsDeclaration>();
}
