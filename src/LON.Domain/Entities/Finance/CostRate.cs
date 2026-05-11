using LON.Domain.Common;

namespace LON.Domain.Entities.Finance;

/// <summary>
/// P16.C3.a — cost rate per scope (Machine / Operator / Shift / Operation /
/// WorkCenter). Replaces the localStorage-only persistence of
/// <c>pages/Finance/CostAccounting.tsx</c>.
///
/// Either <see cref="CostPerHour"/> or <see cref="CostPerUnit"/> is set,
/// depending on whether the operator costs by time or by piece. Both can
/// be set; downstream margin/P&L code prefers per-hour when a duration
/// signal is available.
/// </summary>
public class CostRate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public CostRateScope Scope { get; set; }

    /// <summary>FK into the scope's table. Nullable for tenant-wide
    /// default rates that apply when no scope match exists.</summary>
    public Guid? ScopeId { get; set; }

    public decimal? CostPerHour { get; set; }
    public decimal? CostPerUnit { get; set; }

    /// <summary>ISO-4217 code, e.g. EUR / USD / MKD.</summary>
    public string Currency { get; set; } = "EUR";

    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    public string? Notes { get; set; }
}

public enum CostRateScope
{
    Machine = 1,
    Operator = 2,
    Shift = 3,
    Operation = 4,
    WorkCenter = 5,
}
