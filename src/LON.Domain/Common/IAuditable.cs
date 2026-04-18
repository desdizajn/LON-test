namespace LON.Domain.Common;

/// <summary>
/// Marker interface opting an entity into the audit-log interceptor. Every
/// insert/update/delete of an IAuditable entity produces one
/// <see cref="Entities.Audit.AuditLogEntry"/> row in the same
/// <c>SaveChangesAsync</c> transaction.
///
/// Kept deliberately narrow — not every BaseEntity goes through the audit
/// log (would be noisy). Add <c>: IAuditable</c> only to entities whose
/// change history matters for compliance or regulatory review.
/// </summary>
public interface IAuditable
{
}
