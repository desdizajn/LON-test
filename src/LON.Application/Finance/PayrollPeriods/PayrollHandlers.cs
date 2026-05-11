using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Finance.PayrollPeriods;

public sealed record PayrollLineDto(
    Guid Id,
    Guid PeriodId,
    Guid EmployeeId,
    string? EmployeeName,
    string? EmployeeNumber,
    decimal RegularHours,
    decimal OvertimeHours,
    decimal AbsenceHours,
    decimal BonusAmount,
    decimal DeductionAmount,
    decimal NetAmount,
    string Currency);

public sealed record PayrollPeriodDto(
    Guid Id,
    Guid TenantId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    PayrollStatus Status,
    DateTime? ExportedAt,
    string? Notes,
    IReadOnlyList<PayrollLineDto> Lines,
    DateTime CreatedAt,
    DateTime? ModifiedAt);

/// <summary>
/// P16.C3.b — create (or fetch) a payroll period for [start,end] and
/// seed one PayrollLine per Employee from existing Attendance + Absence
/// totals. Idempotent on (TenantId, PeriodStart, PeriodEnd).
/// </summary>
public sealed record CreatePayrollPeriodCommand : ICommand<Result<PayrollPeriodDto>>
{
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public decimal StandardHoursPerDay { get; init; } = 8m;
    public string? Notes { get; init; }
}

public class CreatePayrollPeriodCommandHandler
    : ICommandHandler<CreatePayrollPeriodCommand, Result<PayrollPeriodDto>>
{
    private readonly IApplicationDbContext _ctx;
    public CreatePayrollPeriodCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<PayrollPeriodDto>> Handle(CreatePayrollPeriodCommand r, CancellationToken ct)
    {
        if (r.PeriodEnd < r.PeriodStart)
            return Result<PayrollPeriodDto>.Failure("PeriodEnd must be >= PeriodStart.");

        var existing = await _ctx.PayrollPeriods
            .Include(p => p.Lines).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(p => p.PeriodStart == r.PeriodStart && p.PeriodEnd == r.PeriodEnd, ct);
        if (existing is not null)
            return Result<PayrollPeriodDto>.Success(ToDto(existing));

        var period = new PayrollPeriod
        {
            Id = Guid.NewGuid(),
            PeriodStart = r.PeriodStart.Date,
            PeriodEnd = r.PeriodEnd.Date,
            Status = PayrollStatus.Draft,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes,
        };
        _ctx.PayrollPeriods.Add(period);

        // Seed lines from existing attendance + absence aggregates.
        var attendance = await _ctx.AttendanceRecords
            .Where(a => a.Date >= period.PeriodStart && a.Date <= period.PeriodEnd && a.Hours != null)
            .GroupBy(a => a.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, TotalHours = g.Sum(x => x.Hours!.Value) })
            .ToListAsync(ct);

        var absence = await _ctx.Absences
            .Where(a => a.From <= period.PeriodEnd && a.To >= period.PeriodStart && a.Approved == true)
            .ToListAsync(ct);

        var absHoursByEmp = absence
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Sum(a =>
            {
                var s = a.From < period.PeriodStart ? period.PeriodStart : a.From;
                var e = a.To > period.PeriodEnd ? period.PeriodEnd : a.To;
                var days = Math.Max(0, (e.Date - s.Date).Days + 1);
                return days * r.StandardHoursPerDay;
            }));

        var employees = await _ctx.Employees.Where(e => e.IsActive).ToListAsync(ct);
        var monthSpanDays = Math.Max(1, (period.PeriodEnd - period.PeriodStart).Days + 1);
        var standardMonthlyHours = monthSpanDays * r.StandardHoursPerDay;

        foreach (var emp in employees)
        {
            var total = attendance.FirstOrDefault(a => a.EmployeeId == emp.Id)?.TotalHours ?? 0m;
            var regular = Math.Min(total, standardMonthlyHours);
            var overtime = Math.Max(0m, total - standardMonthlyHours);
            var absHours = absHoursByEmp.TryGetValue(emp.Id, out var ah) ? ah : 0m;

            _ctx.PayrollLines.Add(new PayrollLine
            {
                Id = Guid.NewGuid(),
                PeriodId = period.Id,
                EmployeeId = emp.Id,
                RegularHours = regular,
                OvertimeHours = overtime,
                AbsenceHours = absHours,
                BonusAmount = 0m,
                DeductionAmount = 0m,
                NetAmount = 0m,
                Currency = "EUR",
            });
        }

        await _ctx.SaveChangesAsync(ct);

        var saved = await _ctx.PayrollPeriods
            .Include(p => p.Lines).ThenInclude(l => l.Employee)
            .FirstAsync(p => p.Id == period.Id, ct);
        return Result<PayrollPeriodDto>.Success(ToDto(saved));
    }

    internal static PayrollPeriodDto ToDto(PayrollPeriod p) => new(
        p.Id, p.TenantId, p.PeriodStart, p.PeriodEnd, p.Status, p.ExportedAt, p.Notes,
        p.Lines.Select(l => new PayrollLineDto(
            l.Id, l.PeriodId, l.EmployeeId,
            l.Employee is null ? null : $"{l.Employee.FirstName} {l.Employee.LastName}".Trim(),
            l.Employee?.EmployeeNumber,
            l.RegularHours, l.OvertimeHours, l.AbsenceHours,
            l.BonusAmount, l.DeductionAmount, l.NetAmount, l.Currency)).ToList(),
        p.CreatedAt, p.ModifiedAt);
}

public sealed record UpdatePayrollLineCommand : ICommand<Result<PayrollLineDto>>
{
    public Guid Id { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal AbsenceHours { get; init; }
    public decimal BonusAmount { get; init; }
    public decimal DeductionAmount { get; init; }
    public decimal NetAmount { get; init; }
    public string Currency { get; init; } = "EUR";
}

public class UpdatePayrollLineCommandHandler
    : ICommandHandler<UpdatePayrollLineCommand, Result<PayrollLineDto>>
{
    private readonly IApplicationDbContext _ctx;
    public UpdatePayrollLineCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<PayrollLineDto>> Handle(UpdatePayrollLineCommand r, CancellationToken ct)
    {
        var line = await _ctx.PayrollLines
            .Include(l => l.Period).Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == r.Id, ct);
        if (line is null) return Result<PayrollLineDto>.Failure($"PayrollLine '{r.Id}' not found.");
        if (line.Period.Status != PayrollStatus.Draft)
            return Result<PayrollLineDto>.Failure("Cannot edit lines on a finalized or exported period.");

        line.RegularHours = r.RegularHours;
        line.OvertimeHours = r.OvertimeHours;
        line.AbsenceHours = r.AbsenceHours;
        line.BonusAmount = r.BonusAmount;
        line.DeductionAmount = r.DeductionAmount;
        line.NetAmount = r.NetAmount;
        if (!string.IsNullOrWhiteSpace(r.Currency))
            line.Currency = r.Currency.ToUpperInvariant();

        await _ctx.SaveChangesAsync(ct);
        return Result<PayrollLineDto>.Success(new PayrollLineDto(
            line.Id, line.PeriodId, line.EmployeeId,
            line.Employee is null ? null : $"{line.Employee.FirstName} {line.Employee.LastName}".Trim(),
            line.Employee?.EmployeeNumber,
            line.RegularHours, line.OvertimeHours, line.AbsenceHours,
            line.BonusAmount, line.DeductionAmount, line.NetAmount, line.Currency));
    }
}

public sealed record FinalizePayrollPeriodCommand(Guid PeriodId) : ICommand<Result<PayrollPeriodDto>>;

public class FinalizePayrollPeriodCommandHandler
    : ICommandHandler<FinalizePayrollPeriodCommand, Result<PayrollPeriodDto>>
{
    private readonly IApplicationDbContext _ctx;
    public FinalizePayrollPeriodCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<PayrollPeriodDto>> Handle(FinalizePayrollPeriodCommand r, CancellationToken ct)
    {
        var p = await _ctx.PayrollPeriods
            .Include(x => x.Lines).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(x => x.Id == r.PeriodId, ct);
        if (p is null) return Result<PayrollPeriodDto>.Failure($"PayrollPeriod '{r.PeriodId}' not found.");
        if (p.Status != PayrollStatus.Draft)
            return Result<PayrollPeriodDto>.Failure("Period is already finalized or exported.");

        p.Status = PayrollStatus.Finalized;
        await _ctx.SaveChangesAsync(ct);
        return Result<PayrollPeriodDto>.Success(CreatePayrollPeriodCommandHandler.ToDto(p));
    }
}

public sealed record ExportPayrollPeriodCommand(Guid PeriodId) : ICommand<Result<PayrollPeriodDto>>;

public class ExportPayrollPeriodCommandHandler
    : ICommandHandler<ExportPayrollPeriodCommand, Result<PayrollPeriodDto>>
{
    private readonly IApplicationDbContext _ctx;
    public ExportPayrollPeriodCommandHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<PayrollPeriodDto>> Handle(ExportPayrollPeriodCommand r, CancellationToken ct)
    {
        var p = await _ctx.PayrollPeriods
            .Include(x => x.Lines).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(x => x.Id == r.PeriodId, ct);
        if (p is null) return Result<PayrollPeriodDto>.Failure($"PayrollPeriod '{r.PeriodId}' not found.");
        if (p.Status == PayrollStatus.Draft)
            return Result<PayrollPeriodDto>.Failure("Finalize the period before exporting.");

        p.Status = PayrollStatus.Exported;
        p.ExportedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Result<PayrollPeriodDto>.Success(CreatePayrollPeriodCommandHandler.ToDto(p));
    }
}

public sealed record GetPayrollPeriodsQuery : IRequest<Result<IReadOnlyList<PayrollPeriodDto>>>;

public class GetPayrollPeriodsQueryHandler
    : IRequestHandler<GetPayrollPeriodsQuery, Result<IReadOnlyList<PayrollPeriodDto>>>
{
    private readonly IApplicationDbContext _ctx;
    public GetPayrollPeriodsQueryHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<IReadOnlyList<PayrollPeriodDto>>> Handle(GetPayrollPeriodsQuery r, CancellationToken ct)
    {
        var rows = await _ctx.PayrollPeriods
            .Include(p => p.Lines).ThenInclude(l => l.Employee)
            .OrderByDescending(p => p.PeriodStart)
            .ToListAsync(ct);
        return Result<IReadOnlyList<PayrollPeriodDto>>.Success(
            rows.Select(CreatePayrollPeriodCommandHandler.ToDto).ToList());
    }
}

public sealed record GetPayrollPeriodByIdQuery(Guid Id) : IRequest<Result<PayrollPeriodDto>>;

public class GetPayrollPeriodByIdQueryHandler
    : IRequestHandler<GetPayrollPeriodByIdQuery, Result<PayrollPeriodDto>>
{
    private readonly IApplicationDbContext _ctx;
    public GetPayrollPeriodByIdQueryHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<Result<PayrollPeriodDto>> Handle(GetPayrollPeriodByIdQuery r, CancellationToken ct)
    {
        var p = await _ctx.PayrollPeriods
            .Include(x => x.Lines).ThenInclude(l => l.Employee)
            .FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (p is null) return Result<PayrollPeriodDto>.Failure($"PayrollPeriod '{r.Id}' not found.");
        return Result<PayrollPeriodDto>.Success(CreatePayrollPeriodCommandHandler.ToDto(p));
    }
}
