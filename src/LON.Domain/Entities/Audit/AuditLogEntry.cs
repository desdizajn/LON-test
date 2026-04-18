using LON.Domain.Common;

namespace LON.Domain.Entities.Audit;

/// <summary>
/// One row per change (Create / Update / Delete) to an <see cref="IAuditable"/>
/// entity. Written by <c>AuditLogInterceptor</c> atomically with the
/// triggering SaveChangesAsync so the log can never drift from DB state.
/// </summary>
public class AuditLogEntry : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Full CLR name of the changed entity, e.g. "CustomsDeclaration".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the changed entity.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Create / Update / Delete (soft).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of changed fields: <c>[{ "field": "MRN", "old": "...", "new": "..." }]</c>.
    /// For Create, the array lists all initial values. For Delete, the array is empty
    /// and the row is tied to the entity's PK only.
    /// </summary>
    public string ChangesJson { get; set; } = "[]";

    /// <summary>User id from ICurrentUserService; null for seeders / background jobs.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Audit-friendly name (Username or "System").</summary>
    public string UserName { get; set; } = "System";

    /// <summary>Exact UTC timestamp of the change.</summary>
    public DateTime OccurredAt { get; set; }
}
