using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts;

public record AcknowledgeAlertEventCommand(Guid Id, string? Reason = null)
    : ICommand<Result<AlertEventDto>>;

public sealed class AcknowledgeAlertEventCommandHandler
    : ICommandHandler<AcknowledgeAlertEventCommand, Result<AlertEventDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public AcknowledgeAlertEventCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context; _user = user;
    }

    public async Task<Result<AlertEventDto>> Handle(AcknowledgeAlertEventCommand request, CancellationToken ct)
    {
        var ev = await _context.AlertEvents.Include(e => e.AlertRule)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (ev is null) return Result<AlertEventDto>.Failure($"AlertEvent '{request.Id}' not found.");
        if (ev.Status == AlertEventStatus.Resolved)
            return Result<AlertEventDto>.Failure("Resolved alerts cannot be re-acknowledged.");

        ev.Status = AlertEventStatus.Acknowledged;
        ev.AcknowledgedAt = DateTime.UtcNow;
        ev.AcknowledgedBy = _user.AuditName;
        ev.AcknowledgedReason = request.Reason;
        ev.ModifiedAt = DateTime.UtcNow;
        ev.ModifiedBy = _user.AuditName;
        await _context.SaveChangesAsync(ct);

        return Result<AlertEventDto>.Success(Map(ev));
    }

    internal static AlertEventDto Map(AlertEvent e) => new()
    {
        Id = e.Id,
        AlertRuleId = e.AlertRuleId,
        AlertRuleCode = e.AlertRule?.Code ?? string.Empty,
        AlertRuleName = e.AlertRule?.NameMk ?? string.Empty,
        OccurredAt = e.OccurredAt,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        Severity = (int)e.Severity,
        SeverityName = e.Severity.ToString(),
        Status = (int)e.Status,
        StatusName = e.Status.ToString(),
        Title = e.Title,
        Body = e.Body,
        AcknowledgedAt = e.AcknowledgedAt,
        AcknowledgedBy = e.AcknowledgedBy,
        ResolvedAt = e.ResolvedAt,
        ResolvedBy = e.ResolvedBy,
    };
}

public record ResolveAlertEventCommand(Guid Id, string? Reason = null)
    : ICommand<Result<AlertEventDto>>;

public sealed class ResolveAlertEventCommandHandler
    : ICommandHandler<ResolveAlertEventCommand, Result<AlertEventDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public ResolveAlertEventCommandHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context; _user = user;
    }

    public async Task<Result<AlertEventDto>> Handle(ResolveAlertEventCommand request, CancellationToken ct)
    {
        var ev = await _context.AlertEvents.Include(e => e.AlertRule)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (ev is null) return Result<AlertEventDto>.Failure($"AlertEvent '{request.Id}' not found.");
        if (ev.Status == AlertEventStatus.Resolved)
            return Result<AlertEventDto>.Failure("Alert is already resolved.");

        ev.Status = AlertEventStatus.Resolved;
        ev.ResolvedAt = DateTime.UtcNow;
        ev.ResolvedBy = _user.AuditName;
        ev.ResolvedReason = request.Reason;
        ev.ModifiedAt = DateTime.UtcNow;
        ev.ModifiedBy = _user.AuditName;
        await _context.SaveChangesAsync(ct);

        return Result<AlertEventDto>.Success(AcknowledgeAlertEventCommandHandler.Map(ev));
    }
}

public record RunAlertEvaluatorCommand : ICommand<Result<RunAlertEvaluatorResult>>;

public record RunAlertEvaluatorResult(int RulesEvaluated, int EventsCreated);

public sealed class RunAlertEvaluatorCommandHandler
    : ICommandHandler<RunAlertEvaluatorCommand, Result<RunAlertEvaluatorResult>>
{
    private readonly IAlertEvaluatorRunner _runner;
    public RunAlertEvaluatorCommandHandler(IAlertEvaluatorRunner runner) => _runner = runner;

    public async Task<Result<RunAlertEvaluatorResult>> Handle(RunAlertEvaluatorCommand request, CancellationToken ct)
    {
        var (rules, events) = await _runner.RunOnceAsync(ct);
        return Result<RunAlertEvaluatorResult>.Success(new RunAlertEvaluatorResult(rules, events));
    }
}
