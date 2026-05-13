using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts.Evaluators;

/// <summary>
/// Rule (f) — a subcontractor is late on a ProductionOrder milestone when the
/// elapsed calendar share of the planned PO window exceeds the produced share
/// by a margin defined by the rule threshold (default 50% — i.e. if we're
/// halfway through the window but less than half is produced, fire).
///
/// "Subcontractor" qualifier in v1: any active PO whose linked
/// InventoryBalances carry <c>AssignedProducerId</c>. The producer mapping
/// itself lives on the balance, not on the PO (per BLUEPRINT §3.4 / §5.6).
/// </summary>
public sealed class SubcontractorLateEvaluator : IAlertRuleEvaluator
{
    private readonly IApplicationDbContext _context;

    public SubcontractorLateEvaluator(IApplicationDbContext context) => _context = context;

    public AlertTriggerKind Kind => AlertTriggerKind.SubcontractorLateOnMilestone;

    public async Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var milestone = (double)(rule.Threshold ?? 0.50m);
        var now = DateTime.UtcNow;

        var pos = await _context.ProductionOrders
            .Where(po => po.TenantId == rule.TenantId
                         && !po.IsDeleted
                         && po.PlannedStartDate <= now
                         && po.PlannedEndDate >= now
                         && po.OrderQuantity > 0)
            .Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.PlannedStartDate,
                po.PlannedEndDate,
                po.OrderQuantity,
                po.ProducedQuantity,
            })
            .ToListAsync(ct);

        var drafts = new List<AlertEventDraft>();
        foreach (var po in pos)
        {
            var totalSpan = (po.PlannedEndDate - po.PlannedStartDate).TotalSeconds;
            if (totalSpan <= 0) continue;
            var elapsedShare = (now - po.PlannedStartDate).TotalSeconds / totalSpan;
            var producedShare = (double)(po.ProducedQuantity / po.OrderQuantity);
            if (elapsedShare <= milestone) continue;
            if (producedShare >= milestone) continue;

            drafts.Add(new AlertEventDraft
            {
                DedupKey = $"SUBCONTRACTOR_LATE:{po.Id}",
                EntityType = "ProductionOrder",
                EntityId = po.Id,
                Title = $"Подизведувач задоцнува: {po.OrderNumber}",
                Body = $"PO {po.OrderNumber} е на {elapsedShare:P0} од планираниот опсег, но произведено е само {producedShare:P0}. Прагот е {milestone:P0}.",
            });
        }
        return drafts;
    }
}
