using LON.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Ai.Engines;

/// <summary>
/// Phase 17 §E10 — Receipt variance recommendation. When a Receipt's lines
/// deviate &gt;5% from the parent CustomsDeclarationLine's declared quantity,
/// surface a warning alongside the supplier's recent variance baseline so
/// the user can decide whether packaging is to blame.
///
/// Baseline = the supplier's last 10 receipts; per-line variance is averaged
/// across all lines in those receipts. Falls back to "no baseline" copy when
/// the supplier has fewer than 3 prior receipts.
/// </summary>
public sealed class ReceiptVarianceRecommendationEngine : IRecommendationEngine
{
    private const double VarianceThreshold = 0.05; // 5%

    private readonly IApplicationDbContext _context;

    public ReceiptVarianceRecommendationEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public string EntityType => "Receipt";

    public async Task<List<Recommendation>> ProduceAsync(Guid receiptId, CancellationToken ct)
    {
        var result = new List<Recommendation>();

        var receipt = await _context.Receipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == receiptId && !r.IsDeleted, ct);
        if (receipt is null) return result;

        var lineVariances = new List<double>();
        foreach (var line in receipt.Lines)
        {
            if (line.CustomsDeclarationId is null) continue;
            var declaredQty = await _context.CustomsDeclarationLines
                .Where(cdl => cdl.CustomsDeclarationId == line.CustomsDeclarationId.Value
                              && cdl.ItemId == line.ItemId
                              && !cdl.IsDeleted)
                .Select(cdl => (decimal?)cdl.Quantity)
                .FirstOrDefaultAsync(ct);
            if (declaredQty is null or 0m) continue;

            var variance = (double)((line.Quantity - declaredQty.Value) / declaredQty.Value);
            lineVariances.Add(variance);
        }

        if (lineVariances.Count == 0) return result;

        var maxAbsVariance = lineVariances.Max(Math.Abs);
        if (maxAbsVariance <= VarianceThreshold)
            return result;

        // Supplier baseline — last 10 receipts from same Partner (excluding this one).
        double? baselineVariance = null;
        int baselineSampleSize = 0;
        if (receipt.PartnerId.HasValue)
        {
            var priorReceipts = await _context.Receipts
                .Include(r => r.Lines)
                .Where(r => r.PartnerId == receipt.PartnerId.Value
                            && r.Id != receipt.Id
                            && !r.IsDeleted)
                .OrderByDescending(r => r.ReceiptDate)
                .Take(10)
                .ToListAsync(ct);
            baselineSampleSize = priorReceipts.Count;

            if (priorReceipts.Count >= 3)
            {
                var baselineSamples = new List<double>();
                foreach (var pr in priorReceipts)
                {
                    foreach (var line in pr.Lines)
                    {
                        if (line.CustomsDeclarationId is null) continue;
                        var declaredQty = await _context.CustomsDeclarationLines
                            .Where(cdl => cdl.CustomsDeclarationId == line.CustomsDeclarationId.Value
                                          && cdl.ItemId == line.ItemId
                                          && !cdl.IsDeleted)
                            .Select(cdl => (decimal?)cdl.Quantity)
                            .FirstOrDefaultAsync(ct);
                        if (declaredQty is null or 0m) continue;
                        baselineSamples.Add(Math.Abs((double)((line.Quantity - declaredQty.Value) / declaredQty.Value)));
                    }
                }
                if (baselineSamples.Count > 0)
                    baselineVariance = baselineSamples.Average();
            }
        }

        var currentPct = Math.Round(maxAbsVariance * 100, 2);
        string body;
        if (baselineVariance.HasValue)
        {
            var basePct = Math.Round(baselineVariance.Value * 100, 2);
            body = $"Просечен variance од овој снабдувач (последни {baselineSampleSize} приема): {basePct}%. Вашиот најголем variance: {currentPct}%. Провери packaging или измери повторно.";
        }
        else
        {
            body = $"Variance: {currentPct}% (>5%). Снабдувачот нема доволно претходни приема за baseline. Провери packaging или измери повторно.";
        }

        result.Add(new Recommendation
        {
            Code = "receipt.variance.over-threshold",
            Title = $"Variance {currentPct}% на овој прием",
            Body = body,
            Severity = "warning",
            StructuredData = new Dictionary<string, object?>
            {
                ["currentVariance"] = currentPct,
                ["baselineVariance"] = baselineVariance.HasValue ? Math.Round(baselineVariance.Value * 100, 2) : null,
                ["baselineSampleSize"] = baselineSampleSize,
                ["partnerId"] = receipt.PartnerId,
                ["thresholdPct"] = VarianceThreshold * 100,
            },
        });

        return result;
    }
}
