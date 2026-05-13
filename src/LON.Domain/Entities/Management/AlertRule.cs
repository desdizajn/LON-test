using LON.Domain.Common;

namespace LON.Domain.Entities.Management;

/// <summary>
/// Phase 17 §E10.5 — configurable alert definition. v1 ships 6 predefined
/// rules per tenant (seeded by the migration); a Phase 26 UI editor will let
/// admins create more.
/// </summary>
public class AlertRule : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }

    /// <summary>Stable code, unique per tenant (e.g. "GUARANTEE_UTIL_90").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Macedonian label.</summary>
    public string NameMk { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    public bool IsActive { get; set; } = true;

    public AlertTriggerKind TriggerKind { get; set; }

    /// <summary>
    /// Per-rule numeric threshold (e.g. 0.90 for "90% guarantee utilisation",
    /// 7.0 for "7 days before due-date"). Interpretation depends on TriggerKind.
    /// </summary>
    public decimal? Threshold { get; set; }

    /// <summary>JSON array of role names that should see this alert in the dashboard / receive emails.</summary>
    public string? RecipientsJson { get; set; }

    /// <summary>Comma-separated channel codes: "Dashboard" (v1) / "Email" (Phase 26).</summary>
    public string DeliveryChannels { get; set; } = "Dashboard";
}

public class AlertEvent : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid AlertRuleId { get; set; }
    public virtual AlertRule? AlertRule { get; set; }

    public DateTime OccurredAt { get; set; }

    /// <summary>Subject entity type, e.g. "ClientOrder", "Machine", "Employee".</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public AlertSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public AlertEventStatus Status { get; set; } = AlertEventStatus.Open;

    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedReason { get; set; }

    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedReason { get; set; }

    /// <summary>
    /// Dedup key: a stable string the evaluator computes from (RuleId,
    /// EntityType, EntityId, condition-bucket). Used to suppress re-creating
    /// an Open AlertEvent that already covers the same condition.
    /// </summary>
    public string DedupKey { get; set; } = string.Empty;
}

public enum AlertSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum AlertTriggerKind
{
    GuaranteeUtilizationHigh = 1,
    ClientOrderDueDateAtRisk = 2,
    MachineDownExtended = 3,
    CertificationExpiringSoon = 4,
    ReceiptVarianceOverThreshold = 5,
    SubcontractorLateOnMilestone = 6,
}

public enum AlertEventStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
}
