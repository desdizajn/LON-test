using LON.Domain.Common;

namespace LON.Domain.Entities.Finance;

/// <summary>
/// Phase 17 §E16 — manual currency conversion rates (v1). Per BLUEPRINT
/// §5.14.8: future Phase 27.1 will add auto-import from the central bank;
/// v1 keeps it manual so Teksport can run end-to-end without external feeds.
///
/// Convention:
///   FromCurrency 1.00 = Rate × ToCurrency
///   e.g. EUR → MKD = 61.50  ⇒  1 EUR = 61.50 MKD
/// </summary>
public class FxRate : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    /// <summary>3-char ISO 4217 (EUR / MKD / USD ...).</summary>
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    /// <summary>UTC midnight of the day this rate becomes effective.</summary>
    public DateTime EffectiveDate { get; set; }

    public FxRateSource Source { get; set; } = FxRateSource.Manual;

    public string? Notes { get; set; }
}

public enum FxRateSource
{
    Manual = 1,
    NationalBank = 2,
}
