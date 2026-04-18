using System.Security.Claims;
using LON.Application.Common.Interfaces;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Services;

/// <summary>
/// Resolves the current tenant via:
///   1. `tenant_id` JWT claim (populated at login — P1.3)
///   2. Lookup from current user's TenantId (DB round-trip, one-shot per request)
///   3. First active Tenant (single-tenant fallback until Phase 1 is fully live)
/// Caches the resolved value on the scoped service instance so we do at most
/// one DB round-trip per HTTP request.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    private Guid? _cached;
    private bool _resolved;

    public CurrentTenantService(
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Guid?> GetTenantIdAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved) return _cached;

        // 1. JWT claim (P1.3 populates this)
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
        if (Guid.TryParse(claim, out var fromClaim))
        {
            _cached = fromClaim;
            _resolved = true;
            return _cached;
        }

        // 2. User lookup
        var userId = _currentUser.UserId;
        if (userId.HasValue)
        {
            var tenantId = await _context.Users
                .Where(u => u.Id == userId.Value)
                .Select(u => (Guid?)u.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
            if (tenantId.HasValue && tenantId != Guid.Empty)
            {
                _cached = tenantId;
                _resolved = true;
                return _cached;
            }
        }

        // 3. First active Tenant (single-tenant fallback)
        var first = await _context.Tenants
            .Where(t => t.IsActive)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
        _cached = first;
        _resolved = true;
        return _cached;
    }
}
