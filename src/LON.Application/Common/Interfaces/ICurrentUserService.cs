namespace LON.Application.Common.Interfaces;

public interface ICurrentUserService
{
    /// <summary>
    /// Username of the authenticated user, or null if no HTTP context
    /// (background jobs, migrations, seeders).
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// User id (Guid) from JWT nameidentifier claim, or null.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Tenant id from JWT `tenant_id` claim (P1.3). Null for unauthenticated
    /// requests, background jobs, seeders — callers that need a tenant during
    /// those flows should fall back to ICurrentTenantService (which hits the
    /// DB to pick the first active tenant).
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Audit label used by ApplicationDbContext for CreatedBy/ModifiedBy.
    /// Returns Username when authenticated, else "System".
    /// </summary>
    string AuditName { get; }
}
