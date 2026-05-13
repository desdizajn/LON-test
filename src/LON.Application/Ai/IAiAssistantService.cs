namespace LON.Application.Ai;

/// <summary>
/// Phase 17 §E10 — orchestration façade for the AI helper drawer. Dispatches
/// the right <see cref="IRecommendationEngine"/> set per entity type, persists
/// each surfaced recommendation in <c>AiSuggestionLogs</c>, and exposes the
/// "user acted on it / dismissed it" feedback path used by analytics.
/// </summary>
public interface IAiAssistantService
{
    /// <summary>
    /// Returns 0..N recommendations applicable to <paramref name="entityId"/>
    /// of <paramref name="entityType"/>. Each row is persisted to
    /// <c>AiSuggestionLogs</c> on the way out so the UI can POST back.
    /// </summary>
    Task<List<Recommendation>> GetRecommendationsAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default);

    Task<bool> MarkActedAsync(Guid suggestionId, CancellationToken ct = default);
    Task<bool> MarkDismissedAsync(Guid suggestionId, CancellationToken ct = default);
}
