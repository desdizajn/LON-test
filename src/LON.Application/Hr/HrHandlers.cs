using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Hr;

// ─────────────────── DTOs returned to frontend ───────────────────

public sealed record AttendanceTodayRow(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    string? Department,
    Guid? AttendanceId,
    DateTime? ClockIn,
    DateTime? ClockOut,
    decimal? Hours,
    AttendanceStatus? Status);

public sealed record AttendanceRecordDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    DateTime Date,
    DateTime? ClockIn,
    DateTime? ClockOut,
    decimal? Hours,
    AttendanceStatus Status,
    string? Notes);

public sealed record AbsenceDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    DateTime From,
    DateTime To,
    AbsenceType Type,
    string? Reason,
    bool? Approved,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt);

public sealed record AssignmentDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string FullName,
    Guid MachineId,
    string MachineCode,
    string MachineName,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes);

// ─────────────────── P10.1 — attendance ───────────────────

/// <summary>
/// Clock-in / clock-out upsert per (Employee, Date). When ClockIn is called
/// and no row exists for today, a row is inserted. When ClockOut is called
/// and the row for today is open, it's closed and Hours is computed.
/// </summary>
public sealed record ClockInCommand(Guid EmployeeId, DateTime? At) : IRequest<Result<Guid>>;

public sealed class ClockInHandler : IRequestHandler<ClockInCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public ClockInHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(ClockInCommand request, CancellationToken ct)
    {
        if (!await _context.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct))
            return Result<Guid>.Failure("hr.employee_not_found", "Employee not found.");

        var at = request.At ?? DateTime.UtcNow;
        var today = at.Date;

        var existing = await _context.AttendanceRecords
            .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Date == today, ct);

        if (existing is not null)
        {
            if (existing.ClockIn.HasValue)
                return Result<Guid>.Failure("hr.already_clocked_in", "Already clocked in today.");
            existing.ClockIn = at;
            existing.Status = AttendanceStatus.Present;
            await _context.SaveChangesAsync(ct);
            return Result<Guid>.Success(existing.Id);
        }

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            Date = today,
            ClockIn = at,
            Status = AttendanceStatus.Present,
        };
        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(record.Id);
    }
}

public sealed record ClockOutCommand(Guid EmployeeId, DateTime? At) : IRequest<Result<Guid>>;

public sealed class ClockOutHandler : IRequestHandler<ClockOutCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public ClockOutHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(ClockOutCommand request, CancellationToken ct)
    {
        var at = request.At ?? DateTime.UtcNow;
        var today = at.Date;

        var record = await _context.AttendanceRecords
            .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Date == today, ct);

        if (record is null)
            return Result<Guid>.Failure("hr.no_clock_in", "No clock-in recorded for today.");
        if (!record.ClockIn.HasValue)
            return Result<Guid>.Failure("hr.no_clock_in", "Record has no clock-in.");
        if (record.ClockOut.HasValue)
            return Result<Guid>.Failure("hr.already_clocked_out", "Already clocked out today.");
        if (at < record.ClockIn.Value)
            return Result<Guid>.Failure("hr.clockout_before_clockin", "Clock-out cannot be before clock-in.");

        record.ClockOut = at;
        record.Hours = Math.Round((decimal)(at - record.ClockIn.Value).TotalHours, 2);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(record.Id);
    }
}

public sealed record GetAttendanceTodayQuery(DateTime? Day)
    : IRequest<Result<IReadOnlyList<AttendanceTodayRow>>>;

public sealed class GetAttendanceTodayHandler
    : IRequestHandler<GetAttendanceTodayQuery, Result<IReadOnlyList<AttendanceTodayRow>>>
{
    private readonly IApplicationDbContext _context;
    public GetAttendanceTodayHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<AttendanceTodayRow>>> Handle(GetAttendanceTodayQuery request, CancellationToken ct)
    {
        var day = (request.Day ?? DateTime.UtcNow).Date;

        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new
            {
                e.Id,
                e.EmployeeNumber,
                e.FirstName,
                e.LastName,
                e.Department,
            })
            .ToListAsync(ct);

        var employeeIds = employees.Select(e => e.Id).ToList();
        var attendance = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.Date == day && employeeIds.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId, ct);

        var rows = employees.Select(e =>
        {
            attendance.TryGetValue(e.Id, out var rec);
            return new AttendanceTodayRow(
                e.Id,
                e.EmployeeNumber,
                $"{e.FirstName} {e.LastName}".Trim(),
                e.Department,
                rec?.Id,
                rec?.ClockIn,
                rec?.ClockOut,
                rec?.Hours,
                rec?.Status);
        }).ToList();

        return Result<IReadOnlyList<AttendanceTodayRow>>.Success(rows);
    }
}

public sealed record GetAttendanceHistoryQuery(Guid? EmployeeId, DateTime? From, DateTime? To)
    : IRequest<Result<IReadOnlyList<AttendanceRecordDto>>>;

public sealed class GetAttendanceHistoryHandler
    : IRequestHandler<GetAttendanceHistoryQuery, Result<IReadOnlyList<AttendanceRecordDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetAttendanceHistoryHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<AttendanceRecordDto>>> Handle(GetAttendanceHistoryQuery request, CancellationToken ct)
    {
        var q = _context.AttendanceRecords.AsNoTracking();
        if (request.EmployeeId.HasValue) q = q.Where(a => a.EmployeeId == request.EmployeeId.Value);
        if (request.From.HasValue) q = q.Where(a => a.Date >= request.From.Value.Date);
        if (request.To.HasValue) q = q.Where(a => a.Date <= request.To.Value.Date);

        var rows = await q
            .OrderByDescending(a => a.Date)
            .Select(a => new AttendanceRecordDto(
                a.Id, a.EmployeeId, a.Employee.EmployeeNumber,
                (a.Employee.FirstName + " " + a.Employee.LastName).Trim(),
                a.Date, a.ClockIn, a.ClockOut, a.Hours, a.Status, a.Notes))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AttendanceRecordDto>>.Success(rows);
    }
}

// ─────────────────── P10.2 — absences ───────────────────

public sealed record CreateAbsenceCommand(
    Guid EmployeeId,
    DateTime From,
    DateTime To,
    AbsenceType Type,
    string? Reason) : IRequest<Result<Guid>>;

public sealed class CreateAbsenceHandler : IRequestHandler<CreateAbsenceCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public CreateAbsenceHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(CreateAbsenceCommand request, CancellationToken ct)
    {
        if (!await _context.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct))
            return Result<Guid>.Failure("hr.employee_not_found", "Employee not found.");
        if (request.To < request.From)
            return Result<Guid>.Failure("hr.absence_range_invalid", "`To` cannot be before `From`.");

        var absence = new Absence
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            From = request.From.Date,
            To = request.To.Date,
            Type = request.Type,
            Reason = request.Reason?.Trim(),
            Approved = null,
        };
        _context.Absences.Add(absence);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(absence.Id);
    }
}

public sealed record DecideAbsenceCommand(Guid Id, bool Approve) : IRequest<Result>;

public sealed class DecideAbsenceHandler : IRequestHandler<DecideAbsenceCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public DecideAbsenceHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DecideAbsenceCommand request, CancellationToken ct)
    {
        var absence = await _context.Absences.FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (absence is null) return Result.Failure("hr.absence_not_found", "Absence not found.");
        if (absence.Approved.HasValue)
            return Result.Failure("hr.absence_already_decided", "Absence already approved or rejected.");

        absence.Approved = request.Approve;
        absence.ApprovedByUserId = _currentUser.UserId;
        absence.ApprovedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetAbsencesQuery(Guid? EmployeeId, bool? PendingOnly)
    : IRequest<Result<IReadOnlyList<AbsenceDto>>>;

public sealed class GetAbsencesHandler : IRequestHandler<GetAbsencesQuery, Result<IReadOnlyList<AbsenceDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetAbsencesHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<AbsenceDto>>> Handle(GetAbsencesQuery request, CancellationToken ct)
    {
        var q = _context.Absences.AsNoTracking();
        if (request.EmployeeId.HasValue) q = q.Where(a => a.EmployeeId == request.EmployeeId.Value);
        if (request.PendingOnly == true) q = q.Where(a => a.Approved == null);

        var rows = await q
            .OrderByDescending(a => a.From)
            .Select(a => new AbsenceDto(
                a.Id, a.EmployeeId, a.Employee.EmployeeNumber,
                (a.Employee.FirstName + " " + a.Employee.LastName).Trim(),
                a.From, a.To, a.Type, a.Reason, a.Approved, a.ApprovedByUserId, a.ApprovedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AbsenceDto>>.Success(rows);
    }
}

// ─────────────────── P10.5 — operator-machine assignments ───────────────────

public sealed record CreateAssignmentCommand(
    Guid EmployeeId,
    Guid MachineId,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class CreateAssignmentHandler : IRequestHandler<CreateAssignmentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    public CreateAssignmentHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<Guid>> Handle(CreateAssignmentCommand request, CancellationToken ct)
    {
        if (!await _context.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct))
            return Result<Guid>.Failure("hr.employee_not_found", "Employee not found.");
        if (!await _context.Machines.AnyAsync(m => m.Id == request.MachineId, ct))
            return Result<Guid>.Failure("machine.not_found", "Machine not found.");
        if (request.ValidTo is { } vt && vt < request.ValidFrom)
            return Result<Guid>.Failure("hr.assignment_range_invalid", "ValidTo cannot be before ValidFrom.");

        var assignment = new OperatorMachineAssignment
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            MachineId = request.MachineId,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Notes = request.Notes?.Trim(),
        };
        _context.OperatorMachineAssignments.Add(assignment);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(assignment.Id);
    }
}

public sealed record EndAssignmentCommand(Guid Id, DateTime ValidTo) : IRequest<Result>;

public sealed class EndAssignmentHandler : IRequestHandler<EndAssignmentCommand, Result>
{
    private readonly IApplicationDbContext _context;
    public EndAssignmentHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result> Handle(EndAssignmentCommand request, CancellationToken ct)
    {
        var a = await _context.OperatorMachineAssignments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (a is null) return Result.Failure("hr.assignment_not_found", "Assignment not found.");
        if (request.ValidTo < a.ValidFrom)
            return Result.Failure("hr.assignment_range_invalid", "ValidTo cannot be before ValidFrom.");

        a.ValidTo = request.ValidTo;
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record GetAssignmentsQuery(Guid? EmployeeId, Guid? MachineId, bool? ActiveOnly)
    : IRequest<Result<IReadOnlyList<AssignmentDto>>>;

public sealed class GetAssignmentsHandler
    : IRequestHandler<GetAssignmentsQuery, Result<IReadOnlyList<AssignmentDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetAssignmentsHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<AssignmentDto>>> Handle(GetAssignmentsQuery request, CancellationToken ct)
    {
        var q = _context.OperatorMachineAssignments.AsNoTracking();
        if (request.EmployeeId.HasValue) q = q.Where(a => a.EmployeeId == request.EmployeeId.Value);
        if (request.MachineId.HasValue) q = q.Where(a => a.MachineId == request.MachineId.Value);
        if (request.ActiveOnly == true)
        {
            var now = DateTime.UtcNow;
            q = q.Where(a => a.ValidFrom <= now && (a.ValidTo == null || a.ValidTo >= now));
        }

        var rows = await q
            .OrderByDescending(a => a.ValidFrom)
            .Select(a => new AssignmentDto(
                a.Id, a.EmployeeId, a.Employee.EmployeeNumber,
                (a.Employee.FirstName + " " + a.Employee.LastName).Trim(),
                a.MachineId, a.Machine.Code, a.Machine.Name,
                a.ValidFrom, a.ValidTo, a.Notes))
            .ToListAsync(ct);

        return Result<IReadOnlyList<AssignmentDto>>.Success(rows);
    }
}
