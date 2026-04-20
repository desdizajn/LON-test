using LON.Domain.Common;
using LON.Domain.Enums;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// P11.1 — append-only log of machine operating-state transitions. The current
/// state for a machine is the most recent row by <see cref="ChangedAt"/>.
/// Manual input until a telemetry feed lands.
/// </summary>
public class MachineStateEvent : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }
    public virtual Machine Machine { get; set; } = null!;
    public MachineState State { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedByEmployeeId { get; set; }
    public virtual Employee? ChangedByEmployee { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// P11.2 — downtime log. A row is "open" when <see cref="End"/> is null; once
/// closed, <see cref="DurationMinutes"/> is written. Drives the Pareto chart by
/// <see cref="Category"/>.
/// </summary>
public class DowntimeEvent : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }
    public virtual Machine Machine { get; set; } = null!;
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public decimal? DurationMinutes { get; set; }
    public DowntimeCategory Category { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? CostImpact { get; set; }
    public Guid? ReportedByEmployeeId { get; set; }
    public virtual Employee? ReportedByEmployee { get; set; }
}

/// <summary>
/// P11.4 — recurring preventive maintenance plan per machine. IntervalDays drives
/// the NextDue rollforward when an associated work order is completed.
/// </summary>
public class MaintenanceSchedule : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }
    public virtual Machine Machine { get; set; } = null!;
    public string TaskDescription { get; set; } = string.Empty;
    public int IntervalDays { get; set; }
    public DateTime? LastDone { get; set; }
    public DateTime NextDue { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<MaintenanceWorkOrder> WorkOrders { get; set; } = new List<MaintenanceWorkOrder>();
}

/// <summary>
/// P11.5 — a single maintenance intervention, optionally linked to a
/// <see cref="MaintenanceSchedule"/>. Completing a work order rolls the schedule's
/// LastDone forward and recomputes NextDue.
/// </summary>
public class MaintenanceWorkOrder : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid MachineId { get; set; }
    public virtual Machine Machine { get; set; } = null!;
    public Guid? ScheduleId { get; set; }
    public virtual MaintenanceSchedule? Schedule { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? TechnicianEmployeeId { get; set; }
    public virtual Employee? TechnicianEmployee { get; set; }
    public string? TaskDescription { get; set; }
    public string? Notes { get; set; }
    public decimal? CostImpact { get; set; }
}
