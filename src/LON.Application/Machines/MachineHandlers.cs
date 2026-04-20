using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Machines;

// ─────────────────── DTOs returned to frontend ───────────────────

public sealed record MachineCurrentStateDto(
    Guid MachineId,
    string MachineCode,
    string MachineName,
    Guid WorkCenterId,
    string WorkCenterCode,
    MachineState? CurrentState,
    DateTime? Since,
    string? Notes);

public sealed record MachineStateEventDto(
    Guid Id,
    Guid MachineId,
    string MachineCode,
    MachineState State,
    DateTime ChangedAt,
    Guid? ChangedByEmployeeId,
    string? Notes);

public sealed record DowntimeEventDto(
    Guid Id,
    Guid MachineId,
    string MachineCode,
    string MachineName,
    DateTime Start,
    DateTime? End,
    decimal? DurationMinutes,
    DowntimeCategory Category,
    string Reason,
    decimal? CostImpact,
    Guid? ReportedByEmployeeId);

public sealed record DowntimeParetoBucket(
    DowntimeCategory Category,
    int Count,
    decimal TotalMinutes);

public sealed record MaintenanceScheduleDto(
    Guid Id,
    Guid MachineId,
    string MachineCode,
    string MachineName,
    string TaskDescription,
    int IntervalDays,
    DateTime? LastDone,
    DateTime NextDue,
    int DaysUntilDue,
    bool IsActive);

public sealed record MaintenanceWorkOrderDto(
    Guid Id,
    Guid MachineId,
    string MachineCode,
    string MachineName,
    Guid? ScheduleId,
    DateTime ScheduledDate,
    DateTime? CompletedAt,
    Guid? TechnicianEmployeeId,
    string? TaskDescription,
    string? Notes,
    decimal? CostImpact);

// ─────────────────── P11.1 — state events ───────────────────

public sealed record LogMachineStateCommand(
    Guid MachineId,
    MachineState State,
    DateTime? ChangedAt,
    Guid? ChangedByEmployeeId,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class LogMachineStateHandler : IRequestHandler<LogMachineStateCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public LogMachineStateHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(LogMachineStateCommand request, CancellationToken ct)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId, ct);
        if (machine is null)
            return Result<Guid>.Failure("machine.not_found", "Machine not found.");

        var evt = new MachineStateEvent
        {
            Id = Guid.NewGuid(),
            MachineId = request.MachineId,
            State = request.State,
            ChangedAt = request.ChangedAt ?? DateTime.UtcNow,
            ChangedByEmployeeId = request.ChangedByEmployeeId,
            Notes = request.Notes,
        };
        _context.MachineStateEvents.Add(evt);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(evt.Id);
    }
}

public sealed record GetCurrentMachineStatesQuery() : IRequest<Result<IReadOnlyList<MachineCurrentStateDto>>>;

public sealed class GetCurrentMachineStatesHandler
    : IRequestHandler<GetCurrentMachineStatesQuery, Result<IReadOnlyList<MachineCurrentStateDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetCurrentMachineStatesHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<MachineCurrentStateDto>>> Handle(GetCurrentMachineStatesQuery request, CancellationToken ct)
    {
        var machines = await _context.Machines
            .AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new { m.Id, m.Code, m.Name, m.WorkCenterId, WorkCenterCode = m.WorkCenter.Code })
            .ToListAsync(ct);

        var machineIds = machines.Select(m => m.Id).ToList();

        var latestPerMachine = await _context.MachineStateEvents
            .AsNoTracking()
            .Where(e => machineIds.Contains(e.MachineId))
            .GroupBy(e => e.MachineId)
            .Select(g => g.OrderByDescending(e => e.ChangedAt).First())
            .ToDictionaryAsync(e => e.MachineId, ct);

        var rows = machines.Select(m =>
        {
            latestPerMachine.TryGetValue(m.Id, out var evt);
            return new MachineCurrentStateDto(
                m.Id, m.Code, m.Name, m.WorkCenterId, m.WorkCenterCode,
                evt?.State, evt?.ChangedAt, evt?.Notes);
        }).ToList();

        return Result<IReadOnlyList<MachineCurrentStateDto>>.Success(rows);
    }
}

// ─────────────────── P11.2 — downtime events ───────────────────

public sealed record LogDowntimeCommand(
    Guid MachineId,
    DateTime Start,
    DateTime? End,
    DowntimeCategory Category,
    string Reason,
    decimal? CostImpact,
    Guid? ReportedByEmployeeId) : IRequest<Result<Guid>>;

public sealed class LogDowntimeHandler : IRequestHandler<LogDowntimeCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public LogDowntimeHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(LogDowntimeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<Guid>.Failure("downtime.reason_required", "Reason is required.");

        if (!await _context.Machines.AnyAsync(m => m.Id == request.MachineId, ct))
            return Result<Guid>.Failure("machine.not_found", "Machine not found.");

        if (request.End is { } end && end < request.Start)
            return Result<Guid>.Failure("downtime.end_before_start", "End cannot be before Start.");

        var evt = new DowntimeEvent
        {
            Id = Guid.NewGuid(),
            MachineId = request.MachineId,
            Start = request.Start,
            End = request.End,
            Category = request.Category,
            Reason = request.Reason.Trim(),
            CostImpact = request.CostImpact,
            ReportedByEmployeeId = request.ReportedByEmployeeId,
            DurationMinutes = request.End is { } e ? (decimal)(e - request.Start).TotalMinutes : null,
        };
        _context.DowntimeEvents.Add(evt);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(evt.Id);
    }
}

public sealed record CloseDowntimeCommand(Guid Id, DateTime End) : IRequest<Result>;

public sealed class CloseDowntimeHandler : IRequestHandler<CloseDowntimeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public CloseDowntimeHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(CloseDowntimeCommand request, CancellationToken ct)
    {
        var evt = await _context.DowntimeEvents.FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (evt is null) return Result.Failure("downtime.not_found", "Downtime event not found.");
        if (evt.End.HasValue) return Result.Failure("downtime.already_closed", "Event is already closed.");
        if (request.End < evt.Start) return Result.Failure("downtime.end_before_start", "End cannot be before Start.");

        evt.End = request.End;
        evt.DurationMinutes = (decimal)(request.End - evt.Start).TotalMinutes;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetDowntimeEventsQuery(Guid? MachineId, DateTime? From, DateTime? To)
    : IRequest<Result<IReadOnlyList<DowntimeEventDto>>>;

public sealed class GetDowntimeEventsHandler
    : IRequestHandler<GetDowntimeEventsQuery, Result<IReadOnlyList<DowntimeEventDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetDowntimeEventsHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<DowntimeEventDto>>> Handle(GetDowntimeEventsQuery request, CancellationToken ct)
    {
        var q = _context.DowntimeEvents.AsNoTracking();
        if (request.MachineId.HasValue) q = q.Where(e => e.MachineId == request.MachineId.Value);
        if (request.From.HasValue) q = q.Where(e => e.Start >= request.From.Value);
        if (request.To.HasValue) q = q.Where(e => e.Start <= request.To.Value);

        var rows = await q
            .OrderByDescending(e => e.Start)
            .Select(e => new DowntimeEventDto(
                e.Id, e.MachineId, e.Machine.Code, e.Machine.Name,
                e.Start, e.End, e.DurationMinutes, e.Category, e.Reason,
                e.CostImpact, e.ReportedByEmployeeId))
            .ToListAsync(ct);

        return Result<IReadOnlyList<DowntimeEventDto>>.Success(rows);
    }
}

public sealed record GetDowntimeParetoQuery(DateTime? From, DateTime? To)
    : IRequest<Result<IReadOnlyList<DowntimeParetoBucket>>>;

public sealed class GetDowntimeParetoHandler
    : IRequestHandler<GetDowntimeParetoQuery, Result<IReadOnlyList<DowntimeParetoBucket>>>
{
    private readonly IApplicationDbContext _context;
    public GetDowntimeParetoHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<DowntimeParetoBucket>>> Handle(GetDowntimeParetoQuery request, CancellationToken ct)
    {
        var q = _context.DowntimeEvents.AsNoTracking();
        if (request.From.HasValue) q = q.Where(e => e.Start >= request.From.Value);
        if (request.To.HasValue) q = q.Where(e => e.Start <= request.To.Value);

        var buckets = await q
            .GroupBy(e => e.Category)
            .Select(g => new DowntimeParetoBucket(
                g.Key,
                g.Count(),
                g.Sum(e => e.DurationMinutes ?? 0m)))
            .OrderByDescending(b => b.TotalMinutes)
            .ToListAsync(ct);

        return Result<IReadOnlyList<DowntimeParetoBucket>>.Success(buckets);
    }
}

// ─────────────────── P11.4 — maintenance schedules ───────────────────

public sealed record CreateMaintenanceScheduleCommand(
    Guid MachineId,
    string TaskDescription,
    int IntervalDays,
    DateTime? LastDone,
    DateTime? NextDue) : IRequest<Result<Guid>>;

public sealed class CreateMaintenanceScheduleHandler : IRequestHandler<CreateMaintenanceScheduleCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public CreateMaintenanceScheduleHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(CreateMaintenanceScheduleCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TaskDescription))
            return Result<Guid>.Failure("maintenance.task_required", "Task description is required.");
        if (request.IntervalDays <= 0)
            return Result<Guid>.Failure("maintenance.interval_invalid", "Interval must be > 0 days.");
        if (!await _context.Machines.AnyAsync(m => m.Id == request.MachineId, ct))
            return Result<Guid>.Failure("machine.not_found", "Machine not found.");

        var nextDue = request.NextDue
            ?? (request.LastDone?.AddDays(request.IntervalDays))
            ?? DateTime.UtcNow.Date.AddDays(request.IntervalDays);

        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            MachineId = request.MachineId,
            TaskDescription = request.TaskDescription.Trim(),
            IntervalDays = request.IntervalDays,
            LastDone = request.LastDone,
            NextDue = nextDue,
            IsActive = true,
        };
        _context.MaintenanceSchedules.Add(schedule);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(schedule.Id);
    }
}

public sealed record UpdateMaintenanceScheduleCommand(
    Guid Id,
    string TaskDescription,
    int IntervalDays,
    DateTime? LastDone,
    DateTime NextDue,
    bool IsActive) : IRequest<Result>;

public sealed class UpdateMaintenanceScheduleHandler : IRequestHandler<UpdateMaintenanceScheduleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public UpdateMaintenanceScheduleHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(UpdateMaintenanceScheduleCommand request, CancellationToken ct)
    {
        var schedule = await _context.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (schedule is null) return Result.Failure("maintenance.not_found", "Schedule not found.");
        if (request.IntervalDays <= 0) return Result.Failure("maintenance.interval_invalid", "Interval must be > 0 days.");

        schedule.TaskDescription = request.TaskDescription.Trim();
        schedule.IntervalDays = request.IntervalDays;
        schedule.LastDone = request.LastDone;
        schedule.NextDue = request.NextDue;
        schedule.IsActive = request.IsActive;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetMaintenanceSchedulesQuery(bool? ActiveOnly)
    : IRequest<Result<IReadOnlyList<MaintenanceScheduleDto>>>;

public sealed class GetMaintenanceSchedulesHandler
    : IRequestHandler<GetMaintenanceSchedulesQuery, Result<IReadOnlyList<MaintenanceScheduleDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetMaintenanceSchedulesHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<MaintenanceScheduleDto>>> Handle(GetMaintenanceSchedulesQuery request, CancellationToken ct)
    {
        var q = _context.MaintenanceSchedules.AsNoTracking();
        if (request.ActiveOnly == true) q = q.Where(s => s.IsActive);

        var today = DateTime.UtcNow.Date;
        var rows = await q
            .OrderBy(s => s.NextDue)
            .Select(s => new MaintenanceScheduleDto(
                s.Id, s.MachineId, s.Machine.Code, s.Machine.Name,
                s.TaskDescription, s.IntervalDays, s.LastDone, s.NextDue,
                (int)(s.NextDue.Date - today).TotalDays,
                s.IsActive))
            .ToListAsync(ct);

        return Result<IReadOnlyList<MaintenanceScheduleDto>>.Success(rows);
    }
}

// ─────────────────── P11.5 — work orders ───────────────────

public sealed record CreateMaintenanceWorkOrderCommand(
    Guid MachineId,
    Guid? ScheduleId,
    DateTime ScheduledDate,
    Guid? TechnicianEmployeeId,
    string? TaskDescription,
    string? Notes,
    decimal? CostImpact) : IRequest<Result<Guid>>;

public sealed class CreateMaintenanceWorkOrderHandler
    : IRequestHandler<CreateMaintenanceWorkOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public CreateMaintenanceWorkOrderHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(CreateMaintenanceWorkOrderCommand request, CancellationToken ct)
    {
        if (!await _context.Machines.AnyAsync(m => m.Id == request.MachineId, ct))
            return Result<Guid>.Failure("machine.not_found", "Machine not found.");

        if (request.ScheduleId is { } sid
            && !await _context.MaintenanceSchedules.AnyAsync(s => s.Id == sid, ct))
            return Result<Guid>.Failure("maintenance.not_found", "Schedule not found.");

        var wo = new MaintenanceWorkOrder
        {
            Id = Guid.NewGuid(),
            MachineId = request.MachineId,
            ScheduleId = request.ScheduleId,
            ScheduledDate = request.ScheduledDate,
            TechnicianEmployeeId = request.TechnicianEmployeeId,
            TaskDescription = request.TaskDescription?.Trim(),
            Notes = request.Notes?.Trim(),
            CostImpact = request.CostImpact,
        };
        _context.MaintenanceWorkOrders.Add(wo);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(wo.Id);
    }
}

/// <summary>
/// Completing a work order rolls its parent schedule's LastDone forward and
/// recomputes NextDue from the completion date + IntervalDays. This is the
/// primary way schedules advance (no cron).
/// </summary>
public sealed record CompleteMaintenanceWorkOrderCommand(
    Guid Id,
    DateTime? CompletedAt,
    string? Notes,
    decimal? CostImpact) : IRequest<Result>;

public sealed class CompleteMaintenanceWorkOrderHandler : IRequestHandler<CompleteMaintenanceWorkOrderCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public CompleteMaintenanceWorkOrderHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(CompleteMaintenanceWorkOrderCommand request, CancellationToken ct)
    {
        var wo = await _context.MaintenanceWorkOrders
            .Include(w => w.Schedule)
            .FirstOrDefaultAsync(w => w.Id == request.Id, ct);
        if (wo is null) return Result.Failure("maintenance.wo_not_found", "Work order not found.");
        if (wo.CompletedAt.HasValue) return Result.Failure("maintenance.wo_already_completed", "Work order is already completed.");

        var completedAt = request.CompletedAt ?? DateTime.UtcNow;
        wo.CompletedAt = completedAt;
        if (request.Notes is not null) wo.Notes = request.Notes.Trim();
        if (request.CostImpact.HasValue) wo.CostImpact = request.CostImpact;

        if (wo.Schedule is { } schedule && schedule.IsActive)
        {
            schedule.LastDone = completedAt;
            schedule.NextDue = completedAt.Date.AddDays(schedule.IntervalDays);
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetMaintenanceWorkOrdersQuery(Guid? MachineId, bool? OpenOnly)
    : IRequest<Result<IReadOnlyList<MaintenanceWorkOrderDto>>>;

public sealed class GetMaintenanceWorkOrdersHandler
    : IRequestHandler<GetMaintenanceWorkOrdersQuery, Result<IReadOnlyList<MaintenanceWorkOrderDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetMaintenanceWorkOrdersHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<MaintenanceWorkOrderDto>>> Handle(GetMaintenanceWorkOrdersQuery request, CancellationToken ct)
    {
        var q = _context.MaintenanceWorkOrders.AsNoTracking();
        if (request.MachineId.HasValue) q = q.Where(w => w.MachineId == request.MachineId.Value);
        if (request.OpenOnly == true) q = q.Where(w => w.CompletedAt == null);

        var rows = await q
            .OrderByDescending(w => w.ScheduledDate)
            .Select(w => new MaintenanceWorkOrderDto(
                w.Id, w.MachineId, w.Machine.Code, w.Machine.Name,
                w.ScheduleId, w.ScheduledDate, w.CompletedAt,
                w.TechnicianEmployeeId, w.TaskDescription, w.Notes, w.CostImpact))
            .ToListAsync(ct);

        return Result<IReadOnlyList<MaintenanceWorkOrderDto>>.Success(rows);
    }
}
