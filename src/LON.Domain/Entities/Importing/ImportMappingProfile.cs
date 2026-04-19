using LON.Domain.Common;

namespace LON.Domain.Entities.Importing;

/// <summary>
/// P5.1.2 — a reusable named column-mapping for the generic importer, scoped
/// to (tenant, target entity, optional partner context). When the wizard
/// picks up a new file, it filters profiles by target + partner context so
/// the user sees only what's relevant — a TEKSPORT + MAGNA + Receipts
/// import shows only the MAGNA-invoice profiles, not BOM-import ones.
/// </summary>
public class ImportMappingProfile : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>Target entity the mapping applies to, e.g. "Receipts", "Items", "Partners".</summary>
    public string TargetEntity { get; set; } = string.Empty;

    /// <summary>Optional partner context (e.g. MAGNA supplier). Null = any partner.</summary>
    public Guid? PartnerContextId { get; set; }

    /// <summary>JSON payload describing source-column → target-field mapping (plus reserved keys for P5.1.3 defaults + P5.1.4 transforms).</summary>
    public string MappingJson { get; set; } = "{}";

    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
