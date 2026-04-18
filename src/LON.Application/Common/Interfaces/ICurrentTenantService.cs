namespace LON.Application.Common.Interfaces;

/// <summary>
/// Resolves the current tenant for the active request/operation.
///
/// Resolution priority (P1.3 will flesh this out):
///   1. `tenant_id` claim on the JWT (set at login after P1.3)
///   2. Lookup from current user's TenantId (DB round-trip)
///   3. First active Tenant (single-tenant fallback for pre-multi-tenant deployments)
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>Returns the resolved TenantId or null if no tenant can be determined (e.g. background service on fresh DB).</summary>
    Task<Guid?> GetTenantIdAsync(CancellationToken cancellationToken = default);
}
