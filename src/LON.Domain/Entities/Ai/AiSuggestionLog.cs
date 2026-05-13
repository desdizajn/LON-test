using LON.Domain.Common;

namespace LON.Domain.Entities.Ai;

/// <summary>
/// Phase 17 §E10 — one row per recommendation surfaced by
/// <c>IAiAssistantService.GetRecommendationsAsync</c>. Used for product
/// analytics (which recommendations users find useful) and as an audit
/// trail when a recommendation drives a state-change action.
///
/// Lifecycle:
///   1. AiAssistantService inserts a row with <see cref="UserActedOn"/> = null at generation time.
///   2. User clicks the action button → POST /api/Ai/suggestions/{id}/acted →
///      flips <see cref="UserActedOn"/> = true and stamps actor + timestamp.
///   3. User dismisses → POST /api/Ai/suggestions/{id}/dismissed →
///      flips <see cref="UserActedOn"/> = false.
/// </summary>
public class AiSuggestionLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Subject entity type, e.g. "ClientOrder" or "Receipt".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Subject entity id.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Stable, machine-friendly code, e.g. "hub.draft.no-fgs".</summary>
    public string RecommendationCode { get; set; } = string.Empty;

    /// <summary>Localised, human-readable title (Macedonian by default).</summary>
    public string RecommendationTitle { get; set; } = string.Empty;

    /// <summary>"info" | "warning" | "success".</summary>
    public string Severity { get; set; } = "info";

    /// <summary>Optional JSON blob carrying numbers the UI rendered in the recommendation body.</summary>
    public string? StructuredDataJson { get; set; }

    /// <summary>Optional deep-link to the action the recommendation suggests.</summary>
    public string? ActionLink { get; set; }

    /// <summary>UTC timestamp when the engine emitted this row.</summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>null = pending, true = user clicked the action, false = user dismissed.</summary>
    public bool? UserActedOn { get; set; }

    public DateTime? UserActedAt { get; set; }
    public string? UserActedBy { get; set; }
}
