namespace LON.Domain.Common;

/// <summary>
/// Phase 17 §E14 — marker interface for entities that participate in the
/// soft-delete workflow with full audit (DeletedAt + DeletedBy stamps) and
/// can be restored from the admin recycle bin.
///
/// <see cref="BaseEntity.IsDeleted"/> is the actual flag (already enforced
/// by the global query filter in <c>ApplicationDbContext</c>). This
/// interface signals which entities expose the surrounding policy:
///   • Restore action available in `/admin/recycle-bin`.
///   • Retention job (90 days) will hard-delete after expiry.
///   • Block-delete policy applies when children exist (per-entity logic).
///
/// Entities must also expose <c>DateTime? DeletedAt</c> and
/// <c>string? DeletedBy</c> properties as a convention — the recycle-bin
/// query reflects on them for the "deleted by"/"deleted at" columns.
/// </summary>
public interface ISoftDeletable
{
}
