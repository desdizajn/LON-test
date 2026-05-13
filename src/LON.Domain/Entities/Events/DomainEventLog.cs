using LON.Domain.Common;

namespace LON.Domain.Entities.Events;

/// <summary>
/// Phase 17 §E11 — append-only persistence of every dispatched
/// <see cref="IDomainEvent"/>. Two purposes:
///   1. Audit trail — "what fired and when" decoupled from the entity audit.
///   2. Replay — Phase 22+ can run event handlers against a window of
///      historical events without re-touching the source entities.
///
/// One row per event. Payload is serialised JSON so new event types don't
/// require migrations.
/// </summary>
public class DomainEventLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Event id as stamped by <see cref="DomainEvent.EventId"/>.</summary>
    public Guid EventId { get; set; }

    /// <summary>Short CLR name of the event type (e.g. "CustomsDeclarationCreatedEvent").</summary>
    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }

    /// <summary>JSON serialisation of the event payload.</summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>"published" | "skipped". Reserved for future Phase 22 replay flow.</summary>
    public string Status { get; set; } = "published";
}
