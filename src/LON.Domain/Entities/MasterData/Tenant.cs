using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// A tenant in the multi-tenant SaaS deployment. Corresponds to one
/// manufacturing company / importer (Uvoznik in ELON terminology). All
/// domain data rows are scoped to a TenantId.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>Short code, unique. Used as display key and for legacy Uvoznik mapping (e.g. "TEKSPORT").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Full legal name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional legacy Uvoznik code from ELON for data migration. Usually same as Code.</summary>
    public string? LegacyUvoznik { get; set; }

    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Customs authorization number (LON Одобрение) — identifies the tenant to customs.</summary>
    public string? CustomsAuthorizationNumber { get; set; }

    /// <summary>Default UI language (mk, sr, sq, en). Users may override individually.</summary>
    public string DefaultLanguage { get; set; } = "mk";

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// I1: per-tenant opt-in to legacy ELON's inflate-for-waste bookkeeping.
    /// When true, Receipt handler inflates the booked quantity by
    /// <c>100/(100 - waste%)</c> using LONAuthorizationItem.AllowedWastePercentage.
    /// TEKSPORT enables this; most tenants leave it off. Receipt-side
    /// application lands with P2.3.
    /// </summary>
    public bool InflateImportForWaste { get; set; }

    /// <summary>
    /// P5.2.5: per-tenant opt-out of FEFO auto-pick during MaterialIssue.
    /// When <c>true</c> (default) the issue handler may auto-resolve
    /// Batch/MRN/Location by FEFO (Imported-first, oldest expiry) when the
    /// caller doesn't pin them. When <c>false</c>, auto-pick is disabled and
    /// the caller MUST explicitly supply Batch or MRN or Location, so the
    /// audit trail never picks inventory implicitly. Useful for tenants
    /// with strict quality-chain or customs-paranoid workflows.
    /// </summary>
    public bool AllowFefoAutoPick { get; set; } = true;
}
