using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

/// <summary>
/// I8: read-only view over the audit log. Administrator-only to avoid
/// leaking cross-entity change histories to regular operators.
/// </summary>
[Authorize(Roles = "Administrator")]
public class AuditController : BaseController
{
    private readonly ApplicationDbContext _context;

    public AuditController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Filter audit entries by entity type and/or entity id and/or
    /// occurred-between-window. Max 500 rows per call — widen the filter
    /// or page via the `from`/`to` window.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAudit(
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int take = 200)
    {
        take = Math.Clamp(take, 1, 500);
        var q = _context.AuditLogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(e => e.EntityType == entityType);
        if (entityId.HasValue)
            q = q.Where(e => e.EntityId == entityId.Value);
        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(e => e.Action == action);
        if (from.HasValue)
            q = q.Where(e => e.OccurredAt >= from.Value);
        if (to.HasValue)
            q = q.Where(e => e.OccurredAt <= to.Value);

        var rows = await q
            .OrderByDescending(e => e.OccurredAt)
            .Take(take)
            .Select(e => new
            {
                e.Id,
                e.EntityType,
                e.EntityId,
                e.Action,
                e.ChangesJson,
                e.UserId,
                e.UserName,
                e.OccurredAt
            })
            .ToListAsync();

        return Ok(rows);
    }
}
