using LON.Domain.Common;

namespace LON.Domain.Entities.Finance;

/// <summary>
/// P16.C3.b — payroll period (monthly bucket).
///
/// Period header. <see cref="Status"/> moves Draft -> Finalized -> Exported.
/// Finalize freezes the lines; Export emits the payroll file and stamps
/// <see cref="ExportedAt"/>. Lines are created on first read of the period
/// from the seeded Attendance + Absence totals; operator then edits
/// rates/bonuses/deductions/NetAmount per spec.
/// </summary>
public class PayrollPeriod : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public DateTime? ExportedAt { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<PayrollLine> Lines { get; set; } = new List<PayrollLine>();
}

public enum PayrollStatus
{
    Draft = 1,
    Finalized = 2,
    Exported = 3,
}
