using LON.Domain.Common;
using LON.Domain.Entities.MasterData;

namespace LON.Domain.Entities.Finance;

/// <summary>
/// P16.C3.b — per-employee line on a <see cref="PayrollPeriod"/>.
///
/// Hours columns are seeded from existing Attendance / Absence tables;
/// operator may override before finalize. NetAmount is operator-entered
/// (rate × hours plus/minus bonuses) — the legacy localStorage rate
/// table is gone. The Currency column is denormalised so the period
/// can mix employees paid in different currencies if needed.
/// </summary>
public class PayrollLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid PeriodId { get; set; }
    public virtual PayrollPeriod Period { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;

    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal AbsenceHours { get; set; }

    public decimal BonusAmount { get; set; }
    public decimal DeductionAmount { get; set; }

    /// <summary>Operator-entered net payroll for this line.</summary>
    public decimal NetAmount { get; set; }

    public string Currency { get; set; } = "EUR";
}
