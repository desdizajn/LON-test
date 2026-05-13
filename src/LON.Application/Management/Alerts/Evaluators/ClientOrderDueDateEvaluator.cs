using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts.Evaluators;

/// <summary>
/// Rule (b) — ClientOrder with RequestedShipDate within threshold days AND
/// produced &lt; 50% of order quantity.
/// </summary>
public sealed class ClientOrderDueDateEvaluator : IAlertRuleEvaluator
{
    private readonly IApplicationDbContext _context;

    public ClientOrderDueDateEvaluator(IApplicationDbContext context) => _context = context;

    public AlertTriggerKind Kind => AlertTriggerKind.ClientOrderDueDateAtRisk;

    public async Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var daysBefore = (int)(rule.Threshold ?? 7m);
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysBefore);

        var openOrders = await _context.ClientOrders
            .Where(o => o.TenantId == rule.TenantId
                        && !o.IsDeleted
                        && o.Status != ClientOrderStatus.Closed
                        && o.Status != ClientOrderStatus.Cancelled
                        && o.RequestedShipDate != null
                        && o.RequestedShipDate <= cutoff)
            .Select(o => new { o.Id, o.OrderNumber, o.RequestedShipDate })
            .ToListAsync(ct);

        var drafts = new List<AlertEventDraft>();
        foreach (var co in openOrders)
        {
            var totals = await _context.ProductionOrders
                .Where(po => po.ClientOrderId == co.Id && !po.IsDeleted)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Order = g.Sum(po => po.OrderQuantity),
                    Produced = g.Sum(po => po.ProducedQuantity),
                })
                .FirstOrDefaultAsync(ct);
            if (totals is null || totals.Order <= 0) continue;
            var pct = totals.Produced / totals.Order;
            if (pct >= 0.50m) continue;

            drafts.Add(new AlertEventDraft
            {
                DedupKey = $"ORDER_DUE_AT_RISK:{co.Id}",
                EntityType = "ClientOrder",
                EntityId = co.Id,
                Title = $"Налог {co.OrderNumber} со рок {co.RequestedShipDate:yyyy-MM-dd} и {pct:P0} произведено",
                Body = $"Налог {co.OrderNumber} има <{daysBefore} дена до RequestedShipDate ({co.RequestedShipDate:yyyy-MM-dd}) и моментално е произведено само {pct:P1} ({totals.Produced:N1}/{totals.Order:N1}).",
            });
        }

        return drafts;
    }
}
