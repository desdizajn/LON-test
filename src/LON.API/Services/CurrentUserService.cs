using System.Security.Claims;
using LON.Application.Common.Interfaces;

namespace LON.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Username =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User?
                .FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string AuditName => Username ?? "System";
}
