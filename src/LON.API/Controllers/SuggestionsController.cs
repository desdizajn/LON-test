using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E6 — stub for the AI helper's "smart suggestion" surface area.
/// Returns simple, deterministic heuristics that the hub dialogs render as
/// "💡 препорачано". Will be replaced by <c>AiAssistantService</c> in §E10.
///
/// Endpoints purposely return the same JSON shape the full implementation will
/// emit, so the UI doesn't need to change when §E10 swaps in real ML scoring.
/// </summary>
public class SuggestionsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public SuggestionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Suggest the producer most-frequently used by this tenant for Podelba in
    /// the past 90 days. Falls back to the first active Producer partner when
    /// no history exists; returns 204 when the tenant has no Producer partners
    /// at all.
    /// </summary>
    [HttpGet("producer")]
    public async Task<IActionResult> SuggestProducer([FromQuery] Guid? clientOrderId = null)
    {
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);

        var top = await _context.InventoryBalances
            .Where(b => b.AssignedProducerId != null && !b.IsDeleted && b.CreatedAt >= ninetyDaysAgo)
            .GroupBy(b => b.AssignedProducerId!.Value)
            .Select(g => new
            {
                ProducerId = g.Key,
                Count = g.Count(),
                TotalQty = g.Sum(b => b.Quantity),
            })
            .OrderByDescending(g => g.Count)
            .FirstOrDefaultAsync();

        if (top is not null)
        {
            var producer = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == top.ProducerId
                                          && !p.IsDeleted
                                          && p.IsActive
                                          && p.Type == PartnerType.Producer);
            if (producer is not null)
            {
                return Ok(new
                {
                    producerId = producer.Id,
                    code = producer.Code,
                    name = producer.Name,
                    score = top.Count,
                    reason = "history.last90Days",
                    recentAssignmentCount = top.Count,
                    recentTotalQuantity = top.TotalQty,
                    clientOrderId,
                });
            }
        }

        var fallback = await _context.Partners
            .Where(p => !p.IsDeleted && p.IsActive && p.Type == PartnerType.Producer)
            .OrderBy(p => p.Code)
            .FirstOrDefaultAsync();
        if (fallback is null) return NoContent();

        return Ok(new
        {
            producerId = fallback.Id,
            code = fallback.Code,
            name = fallback.Name,
            score = 0,
            reason = "fallback.firstActive",
            recentAssignmentCount = 0,
            recentTotalQuantity = 0m,
            clientOrderId,
        });
    }
}
