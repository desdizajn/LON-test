using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Finance;

/// <summary>
/// P12.3 — a client contract binds a customer partner to a period and a rate
/// card. Invoices reference the contract to resolve the right rate at the
/// time of billing. Contracts are tenant-scoped and soft-deletable.
/// </summary>
public class ClientContract : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = string.Empty;
    public Guid PartnerId { get; set; }
    public virtual Partner Partner { get; set; } = null!;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public string Currency { get; set; } = "EUR";
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public virtual ICollection<RateCardEntry> RateCardEntries { get; set; } = new List<RateCardEntry>();
}

/// <summary>
/// P12.3 — a single rate inside a contract. Two flavours, controlled by
/// <see cref="RateType"/>:
///   PerPiece  — <see cref="ItemId"/> required; billed per piece of that FG.
///   PerMinute — <see cref="OperationCode"/> required; billed per minute of
///               logged operation time (future — cost-accounting P12.4 wires
///               this to <c>OperationTimeLog</c>).
/// Currency mirrors the parent contract for v1 — mixed-currency rate cards
/// are an explicit non-goal of the MVP.
/// </summary>
public class RateCardEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public virtual ClientContract Contract { get; set; } = null!;
    public RateType RateType { get; set; }
    public Guid? ItemId { get; set; }
    public virtual Item? Item { get; set; }
    public string? OperationCode { get; set; }
    public decimal RatePerUnit { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// P12.2 — an invoice header. `Status` drives the lifecycle:
///   Draft  — editable, lines can be added, nothing billed yet.
///   Issued — number committed, sent to customer, cannot edit lines.
///   Paid   — closed with payment recorded.
///   Cancelled — voided before or after issue; lines preserved for audit.
/// </summary>
public class Invoice : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Number { get; set; } = string.Empty;
    public Guid PartnerId { get; set; }
    public virtual Partner Partner { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public virtual ClientContract? Contract { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal SubTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime? IssuedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }

    public virtual ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}

/// <summary>
/// P12.2 — one line on an invoice. `RelatedProductionOrderId` / `RelatedShipmentId`
/// preserve the source that generated the line (via GenerateFromPO or manual).
/// </summary>
public class InvoiceLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public virtual Invoice Invoice { get; set; } = null!;
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? ItemId { get; set; }
    public virtual Item? Item { get; set; }
    public Guid? RelatedProductionOrderId { get; set; }
    public virtual ProductionOrder? RelatedProductionOrder { get; set; }
    public Guid? RelatedShipmentId { get; set; }
    public virtual Shipment? RelatedShipment { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
