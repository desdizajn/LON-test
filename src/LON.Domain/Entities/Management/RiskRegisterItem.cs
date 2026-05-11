using LON.Domain.Common;

namespace LON.Domain.Entities.Management;

/// <summary>
/// P16.C1 — unified register for the management Risks page + Escalations page.
/// Replaces the localStorage-only persistence that lived behind
/// <c>pages/Management/OpenRisks.tsx</c> and <c>pages/Management/Escalations.tsx</c>.
///
/// One entity, two surfaces: <see cref="Kind"/> partitions rows into Risk vs.
/// Escalation. The two pages each query with a Kind filter, so the operator
/// sees them as two separate lists, but the schema, RBAC, and tenant
/// isolation are shared.
/// </summary>
public class RiskRegisterItem : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public RiskKind Kind { get; set; }

    /// <summary>Short one-line description; required.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional free-text category (e.g. "Customs", "Customer", "Legal").</summary>
    public string? Category { get; set; }

    public RiskSeverity Severity { get; set; } = RiskSeverity.Medium;

    public RiskStatus Status { get; set; } = RiskStatus.Open;

    /// <summary>Optional owner name (free text — not a User FK; the legacy
    /// pages already store an owner string).</summary>
    public string? Owner { get; set; }

    /// <summary>Mitigation plan (Risk semantics).</summary>
    public string? Mitigation { get; set; }

    /// <summary>Final resolution note (Escalation semantics, or closed Risk).</summary>
    public string? Resolution { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ReviewDate { get; set; }
}

public enum RiskKind
{
    Risk = 1,
    Escalation = 2,
}

public enum RiskSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum RiskStatus
{
    Open = 1,
    InReview = 2,
    Mitigating = 3,
    Resolved = 4,
    Deferred = 5,
    Closed = 6,
}
