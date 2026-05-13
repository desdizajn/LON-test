using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LON.Application.Management.Alerts;

/// <summary>
/// Phase 17 §E10.5 — concrete runner used by both the LON.Worker hosted
/// service and the management API's on-demand endpoint. Scope-friendly:
/// expects to be resolved per-pass (one DbContext, one set of evaluators).
/// </summary>
public sealed class AlertEvaluatorRunner : IAlertEvaluatorRunner
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IAlertRuleEvaluator> _evaluators;
    private readonly ILogger<AlertEvaluatorRunner>? _logger;

    public AlertEvaluatorRunner(
        IApplicationDbContext context,
        IEnumerable<IAlertRuleEvaluator> evaluators,
        ILogger<AlertEvaluatorRunner>? logger = null)
    {
        _context = context;
        _evaluators = evaluators;
        _logger = logger;
    }

    public async Task<(int RulesEvaluated, int EventsCreated)> RunOnceAsync(CancellationToken ct = default)
    {
        var byKind = _evaluators.ToDictionary(e => e.Kind);
        if (byKind.Count == 0) return (0, 0);

        var rules = await _context.AlertRules
            .Where(r => r.IsActive && !r.IsDeleted)
            .ToListAsync(ct);

        var totalCreated = 0;
        foreach (var rule in rules)
        {
            if (!byKind.TryGetValue(rule.TriggerKind, out var evaluator)) continue;

            List<AlertEventDraft> drafts;
            try
            {
                drafts = await evaluator.EvaluateAsync(rule, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Evaluator {Kind} failed for rule {RuleId}", rule.TriggerKind, rule.Id);
                continue;
            }
            if (drafts.Count == 0) continue;

            var dedupKeys = drafts.Select(d => d.DedupKey).ToList();
            var existing = await _context.AlertEvents
                .Where(ev => ev.TenantId == rule.TenantId
                             && ev.AlertRuleId == rule.Id
                             && ev.Status == AlertEventStatus.Open
                             && dedupKeys.Contains(ev.DedupKey))
                .Select(ev => ev.DedupKey)
                .ToListAsync(ct);
            var seen = new HashSet<string>(existing);

            var now = DateTime.UtcNow;
            var batchCreated = 0;
            foreach (var draft in drafts)
            {
                if (seen.Contains(draft.DedupKey)) continue;
                seen.Add(draft.DedupKey);

                _context.AlertEvents.Add(new AlertEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = rule.TenantId,
                    AlertRuleId = rule.Id,
                    OccurredAt = now,
                    EntityType = draft.EntityType,
                    EntityId = draft.EntityId,
                    Severity = rule.Severity,
                    Title = draft.Title,
                    Body = draft.Body,
                    Status = AlertEventStatus.Open,
                    DedupKey = draft.DedupKey,
                    CreatedAt = now,
                    CreatedBy = "AlertEvaluator",
                });
                batchCreated++;
            }
            if (batchCreated > 0)
                await _context.SaveChangesAsync(ct);
            totalCreated += batchCreated;
        }

        return (rules.Count, totalCreated);
    }
}
