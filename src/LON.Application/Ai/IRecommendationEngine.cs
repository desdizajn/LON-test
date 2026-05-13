namespace LON.Application.Ai;

/// <summary>
/// One engine per entity type. AiAssistantService dispatches to all engines
/// whose <see cref="EntityType"/> matches the requested entityType and
/// aggregates their output.
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>The entity type this engine reacts to (case-sensitive).</summary>
    string EntityType { get; }

    Task<List<Recommendation>> ProduceAsync(Guid entityId, CancellationToken ct);
}
