namespace LON.Application.Ai;

/// <summary>
/// Phase 17 §E10 — single recommendation surfaced by the AI helper drawer.
/// Returned in lists by <see cref="IAiAssistantService.GetRecommendationsAsync"/>.
/// </summary>
public record Recommendation
{
    /// <summary>Persisted <c>AiSuggestionLog.Id</c>; the UI POSTs this back on action / dismiss.</summary>
    public Guid Id { get; init; }

    /// <summary>Stable, machine-readable code (e.g. "hub.draft.no-fgs"). Use to dedupe / analyse.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Localised title shown as the recommendation header (Macedonian by default).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Localised body / explanation text.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>"info" | "warning" | "success".</summary>
    public string Severity { get; init; } = "info";

    /// <summary>0.0–1.0 confidence score. Currently fixed per engine; ML scoring is post-v1.</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Optional UI action that the recommendation drives. Either a deep link
    /// ("/orders/{id}") or a hub action key ("orders.actions.bom") that the
    /// hub action launcher recognises.
    /// </summary>
    public string? ActionLink { get; init; }

    /// <summary>
    /// Localised label for the action button. Null = render no button (info-only).
    /// </summary>
    public string? ActionLabel { get; init; }

    /// <summary>Bag of numeric / id values rendered in the body (e.g. variance %).</summary>
    public Dictionary<string, object?>? StructuredData { get; init; }
}
