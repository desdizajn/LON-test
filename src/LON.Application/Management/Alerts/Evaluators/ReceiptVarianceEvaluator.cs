using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts.Evaluators;

/// <summary>
/// Rule (e) — Receipts whose line-level variance vs the parent
/// CustomsDeclarationLine quantity exceeds <c>threshold</c>. We look at the
/// last 24h of receipts to avoid re-firing on old data while still allowing
/// the worker to catch up after a restart. Per-receipt DedupKey keeps it
/// idempotent.
/// </summary>
public sealed class ReceiptVarianceEvaluator : IAlertRuleEvaluator
{
    private readonly IApplicationDbContext _context;

    public ReceiptVarianceEvaluator(IApplicationDbContext context) => _context = context;

    public AlertTriggerKind Kind => AlertTriggerKind.ReceiptVarianceOverThreshold;

    public async Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var threshold = (double)(rule.Threshold ?? 0.05m);
        var lookback = DateTime.UtcNow.AddHours(-24);

        var recentReceipts = await _context.Receipts
            .Where(r => r.TenantId == rule.TenantId
                        && !r.IsDeleted
                        && r.ReceiptDate >= lookback)
            .Include(r => r.Lines)
            .ToListAsync(ct);
        if (recentReceipts.Count == 0) return new List<AlertEventDraft>();

        var drafts = new List<AlertEventDraft>();
        foreach (var receipt in recentReceipts)
        {
            double maxAbs = 0;
            foreach (var line in receipt.Lines)
            {
                if (line.CustomsDeclarationId is null) continue;
                var declared = await _context.CustomsDeclarationLines
                    .Where(cdl => cdl.CustomsDeclarationId == line.CustomsDeclarationId.Value
                                  && cdl.ItemId == line.ItemId
                                  && !cdl.IsDeleted)
                    .Select(cdl => (decimal?)cdl.Quantity)
                    .FirstOrDefaultAsync(ct);
                if (declared is null or 0m) continue;
                var v = Math.Abs((double)((line.Quantity - declared.Value) / declared.Value));
                if (v > maxAbs) maxAbs = v;
            }
            if (maxAbs <= threshold) continue;

            drafts.Add(new AlertEventDraft
            {
                DedupKey = $"RECEIPT_VAR:{receipt.Id}",
                EntityType = "Receipt",
                EntityId = receipt.Id,
                Title = $"Variance на прием {receipt.ReceiptNumber}: {maxAbs:P1}",
                Body = $"Приемот {receipt.ReceiptNumber} ({receipt.ReceiptDate:yyyy-MM-dd}) има variance {maxAbs:P1} наспроти декларираните количини. Прагот е {threshold:P0}.",
            });
        }
        return drafts;
    }
}
