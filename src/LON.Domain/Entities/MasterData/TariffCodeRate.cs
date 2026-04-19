using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// Year-indexed customs/VAT rate for a tariff code.
///
/// Customs tariff rates can change between MK budget years; a single rate column
/// on <see cref="TariffCode"/> would lose the historical value needed to re-duty
/// a declaration that was filed under the old schedule. This table records one
/// row per (tariff, period) and is consulted by <c>DutyRateLookupWarningRule</c>
/// (warning-only) and will be consulted by any future recalculation flow.
///
/// The row active on a given declaration date D is the one where
/// ValidFrom ≤ D &lt; (ValidTo ?? +∞).
/// </summary>
public class TariffCodeRate : BaseEntity
{
    public Guid TariffCodeId { get; set; }
    public virtual TariffCode TariffCode { get; set; } = null!;

    /// <summary>Inclusive lower bound of the validity window.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Exclusive upper bound. Null = still the current rate.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Customs duty percentage (e.g. 7.5 for 7.5%).</summary>
    public decimal CustomsRate { get; set; }

    /// <summary>VAT percentage for this line (e.g. 18 or 5 for MK ЗДДВ bands).</summary>
    public decimal VATRate { get; set; }

    /// <summary>Optional note: source, gazette reference, amendment number, etc.</summary>
    public string? Source { get; set; }
}
