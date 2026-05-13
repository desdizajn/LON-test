using LON.Domain.Common;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;

namespace LON.Domain.Entities.Customs;

/// <summary>
/// Phase 17 §E1 — line on a ClientOrder declaring "we owe customer N pieces of FG item X".
///
/// Per BLUEPRINT §3.1. Mapping from legacy GotoviProizvodi (1 row per FG per
/// Zaklucok). BOMId is optional at create-time — gets assigned during §E5
/// (BOM wiring) or via inline BOM picker on hub.
/// </summary>
public class ClientOrderFinishedGood : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ClientOrderId { get; set; }
    public virtual ClientOrder? ClientOrder { get; set; }

    /// <summary>FK to Item with type=Finished.</summary>
    public Guid ItemId { get; set; }
    public virtual Item? Item { get; set; }

    public decimal Quantity { get; set; }

    public Guid UoMId { get; set; }
    public virtual UnitOfMeasure? UoM { get; set; }

    /// <summary>Optional FK to BOM. Assigned during §E5 (BOM wiring).</summary>
    public Guid? BOMId { get; set; }
    public virtual BOM? BOM { get; set; }

    /// <summary>Contract unit price (foreign currency); used for margin reports.</summary>
    public decimal? UnitPriceForeign { get; set; }

    /// <summary>3-char ISO code; defaults to EUR (TEKSPORT-dominant).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Notes / variant description.</summary>
    public string? Notes { get; set; }
}
