namespace LON.Application.Importing.DTOs;

/// <summary>
/// P5.1.3 — header-level defaults that cascade into every imported row.
/// Stored as a free-form dictionary so the wizard stays target-agnostic:
/// P5.1.5 target validation decides which keys are meaningful for each
/// target entity (Receipts want Warehouse/Location/Partner/Date; Items
/// don't want any of those).
///
/// Keys are target-field names (same space as <see cref="ImportMappingColumn.TargetField"/>).
/// Values are string-typed — the transform rules in P5.1.4 promote them
/// to decimal/date/guid at commit time.
/// </summary>
public sealed record ImportDefaults(Dictionary<string, string?> Values)
{
    public ImportDefaults() : this(new Dictionary<string, string?>()) { }
}
