using LON.Domain.Common;
using LON.Domain.Enums;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// P10.1 — daily attendance record per employee. One row per (EmployeeId,
/// Date). ClockIn is set when the employee clocks in; ClockOut when they
/// clock out. <see cref="Hours"/> is computed server-side on clock-out.
/// </summary>
public class AttendanceRecord : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public DateTime Date { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public decimal? Hours { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// P10.2 — absence record (sick / vacation / personal / other). Spans a date
/// range. <see cref="Approved"/> stays null until a manager decides.
/// </summary>
public class Absence : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public AbsenceType Type { get; set; }
    public string? Reason { get; set; }
    public bool? Approved { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

/// <summary>
/// P10.5 — operator ↔ machine assignment window. Defines who is certified /
/// scheduled to run which machine for a period. NULL ValidTo means open-ended.
/// </summary>
public class OperatorMachineAssignment : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public Guid MachineId { get; set; }
    public virtual Machine Machine { get; set; } = null!;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? Notes { get; set; }
}
