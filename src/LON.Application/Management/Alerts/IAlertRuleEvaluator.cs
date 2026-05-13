using LON.Domain.Entities.Management;

namespace LON.Application.Management.Alerts;

/// <summary>
/// Phase 17 §E10.5 — strategy interface for the 6 predefined v1 alert rules.
/// One concrete evaluator per <see cref="AlertTriggerKind"/>. The worker
/// resolves all registered evaluators, filters AlertRules by <see cref="Kind"/>,
/// and lets each evaluator emit zero or more drafts that the worker turns
/// into <see cref="AlertEvent"/> rows (with dedupe).
/// </summary>
public interface IAlertRuleEvaluator
{
    AlertTriggerKind Kind { get; }

    Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct);
}

/// <summary>
/// Output of an evaluator: a candidate <see cref="AlertEvent"/> the worker
/// will persist iff no Open event with the same <see cref="DedupKey"/>
/// already exists.
/// </summary>
public record AlertEventDraft
{
    /// <summary>Stable key (rule + entity + bucket) used to suppress duplicates.</summary>
    public string DedupKey { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}
