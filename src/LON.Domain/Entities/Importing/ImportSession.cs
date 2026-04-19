using LON.Domain.Common;
using LON.Domain.Enums;

namespace LON.Domain.Entities.Importing;

/// <summary>
/// P5.1 — one uploaded file's parsed grid plus the mapping/transform state
/// that evolves as the user walks through the import wizard.
/// Status transitions: Uploaded -> Mapped -> Committed | Failed.
/// Headers / Rows / Mapping / Defaults / Transforms are stored as JSON blobs;
/// the import wizard is the only consumer and a strongly-typed shape would
/// bloat the schema for tabular data whose columns aren't known upfront.
/// </summary>
public class ImportSession : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public ImportSourceFormat SourceFormat { get; set; }
    public long FileSizeBytes { get; set; }
    public ImportSessionStatus Status { get; set; } = ImportSessionStatus.Uploaded;

    /// <summary>JSON array of detected header strings, e.g. ["Code","Name","Qty"].</summary>
    public string HeadersJson { get; set; } = "[]";

    /// <summary>JSON array-of-arrays — every parsed row as an array of string cells.
    /// All rows are persisted so dry-run (P5.1.6) and commit can replay without re-upload.</summary>
    public string RowsJson { get; set; } = "[]";

    public int RowCount { get; set; }

    /// <summary>Populated in P5.1.2+: source-column → target-field mapping + profile id.</summary>
    public string? MappingJson { get; set; }

    /// <summary>Populated in P5.1.3+: header-level default values that cascade into every row.</summary>
    public string? DefaultsJson { get; set; }

    /// <summary>Populated in P5.1.4+: per-column transform rules (UPPER/TRIM/decimal/date parse/lookup).</summary>
    public string? TransformsJson { get; set; }

    /// <summary>Target entity name from P5.1.5 (Receipts, Items, Partners, BOMs, CustomsDeclarations).</summary>
    public string? TargetEntity { get; set; }

    /// <summary>Optional partner/supplier context used to scope saved mapping profiles (P5.1.2).</summary>
    public Guid? PartnerContextId { get; set; }

    /// <summary>Optional user-facing label.</summary>
    public string? Notes { get; set; }
}
