using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// P5.3.5 — per-user "recent values" cache. Each (User, FieldKey, Value)
/// triple is upserted on every form submit; the UI queries top N by
/// <see cref="LastUsedAt"/> to populate datalist-style dropdowns so
/// repeat entry becomes zero-keystroke.
///
/// <para>FieldKey is a dotted string (e.g. <c>receipt.supplier</c>,
/// <c>item.tariffCode</c>) that the calling page chooses. No enum — any
/// form can adopt recent-values by picking a unique key.</para>
///
/// <para>Tenant-scoped so a user doesn't see another tenant's history
/// (relevant for admins that are assigned to multiple tenants over time).
/// Soft-deleted by Users cascade; actual retention pruning (keep top 50)
/// is done by upsert-time cleanup in the handler.</para>
/// </summary>
public class UserFieldHistory : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    /// <summary>Caller-chosen key (e.g. "receipt.supplier"). Max 128 chars.</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>Raw value; stringified by the UI (numbers, codes, even GUIDs). Max 512 chars.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Updated on every upsert; drives the "recency" sort.</summary>
    public DateTime LastUsedAt { get; set; }

    /// <summary>Incremented on every upsert. Lets UI optionally promote most-used over most-recent.</summary>
    public int UsageCount { get; set; }
}
