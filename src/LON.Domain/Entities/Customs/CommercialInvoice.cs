using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.WMS;

namespace LON.Domain.Entities.Customs;

/// <summary>
/// Phase 17 §E8.5 (D4 decision 2026-05-12) — customs commercial invoice that
/// accompanies an EX shipment. Replaces legacy `tblIzvozniFakturi` +
/// `tblIzvozniFakturiStavki` (3,239 + 57,857 rows).
///
/// Distinct from sales <c>Invoice</c> (BLUEPRINT §5.14.2 — Teksport billing the
/// customer for processing labor). <see cref="CommercialInvoice"/> is the
/// declared trade value of finished goods at the border, drafted by Teksport
/// on the customer's behalf when goods leave for the consignee abroad.
///
/// Workflow: auto-suggested from a Shipment's lines → user edits consignee /
/// consignor / incoterms → Save Draft → Issue (locks, PDF available) → optional
/// Cancel with reason.
///
/// Finance integration (margin reconciliation per ClientOrder) is deferred to
/// Phase 27. v1 stops at the export-customs document itself.
/// </summary>
public class CommercialInvoice : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    /// <summary>Per-tenant sequential number formatted via
    /// <see cref="LON.Domain.Common.NumberFormatter.CommercialInvoice"/>:
    /// <c>CI-{year:0000}-{seq:D6}</c>. Unique within tenant.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Optional FK to the parent ClientOrder so the hub
    /// "Commercial invoices" tab can filter via a single IN-clause.</summary>
    public Guid? ClientOrderId { get; set; }
    public virtual ClientOrder? ClientOrder { get; set; }

    /// <summary>FK to the physical shipment carrying these goods. Optional —
    /// commercial invoices can be drafted speculatively before a shipment exists,
    /// but in practice the suggest-from-shipment flow always wires this.</summary>
    public Guid? ShipmentId { get; set; }
    public virtual Shipment? Shipment { get; set; }

    /// <summary>FK to the EX <see cref="CustomsDeclaration"/>. Optional —
    /// commercial invoice may be raised pre-declaration.</summary>
    public Guid? CustomsDeclarationId { get; set; }
    public virtual CustomsDeclaration? CustomsDeclaration { get; set; }

    /// <summary>Receiver (downstream brand / retailer abroad). Partner with Type=Customer.</summary>
    public Guid ConsigneePartnerId { get; set; }
    public virtual Partner? ConsigneePartner { get; set; }

    /// <summary>Sender (usually the Teksport customer brand whose goods leave the border).</summary>
    public Guid ConsignorPartnerId { get; set; }
    public virtual Partner? ConsignorPartner { get; set; }

    public DateTime InvoiceDate { get; set; }

    /// <summary>3-char ISO currency. Defaults to EUR.</summary>
    public string Currency { get; set; } = "EUR";

    public decimal Subtotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>2-char ISO country code (destination of FG).</summary>
    public string? CountryOfDestination { get; set; }

    /// <summary>Trade terms: FOB / EXW / CIF / DAP / DAT / DDP / ...</summary>
    public string Incoterms { get; set; } = string.Empty;

    /// <summary>Free-text payment terms.</summary>
    public string? PaymentTerms { get; set; }

    /// <summary>Lifecycle: Draft → Issued → (optional) Cancelled.</summary>
    public CommercialInvoiceStatus Status { get; set; } = CommercialInvoiceStatus.Draft;

    public DateTime? IssuedAt { get; set; }
    public string? IssuedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public string? Notes { get; set; }

    // Soft-delete extension fields (BaseEntity.IsDeleted alone, with optional audit columns
    // matching ClientOrder's convention).
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public virtual ICollection<CommercialInvoiceLine> Lines { get; set; } = new List<CommercialInvoiceLine>();
}

public class CommercialInvoiceLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CommercialInvoiceId { get; set; }
    public virtual CommercialInvoice CommercialInvoice { get; set; } = null!;

    public int LineNumber { get; set; }

    public Guid ItemId { get; set; }
    public virtual Item? Item { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure? UoM { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>2-char ISO country code of origin (usually MK for FG processed in MK).</summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>Optional FK to TariffCode (pulled from Item.DefaultTariffCode on suggest).</summary>
    public Guid? TariffCodeId { get; set; }

    public string? Notes { get; set; }
}

public enum CommercialInvoiceStatus
{
    Draft = 1,
    Issued = 2,
    Cancelled = 3,
}
