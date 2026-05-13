using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts;

/// <summary>
/// Phase 17 §E10.5 — list AlertEvents, filtered by status / severity /
/// date window. Newest-first; default page size 50, max 200.
/// </summary>
public record GetAlertEventsQuery : ICommand<Result<List<AlertEventDto>>>
{
    public AlertEventStatus? Status { get; init; }
    public LON.Domain.Entities.Management.AlertSeverity? Severity { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class GetAlertEventsQueryHandler
    : ICommandHandler<GetAlertEventsQuery, Result<List<AlertEventDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetAlertEventsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<AlertEventDto>>> Handle(GetAlertEventsQuery request, CancellationToken ct)
    {
        var query = _context.AlertEvents.Include(e => e.AlertRule).AsQueryable();
        if (request.Status.HasValue)
        {
            var statusVal = (int)request.Status.Value;
            query = query.Where(e => (int)e.Status == statusVal);
        }
        if (request.Severity.HasValue)
        {
            var sevVal = (int)request.Severity.Value;
            query = query.Where(e => (int)e.Severity == sevVal);
        }
        if (request.From.HasValue) query = query.Where(e => e.OccurredAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(e => e.OccurredAt <= request.To.Value);

        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var skip = Math.Max(0, (request.Page - 1) * pageSize);
        var rows = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(e => new AlertEventDto
            {
                Id = e.Id,
                AlertRuleId = e.AlertRuleId,
                AlertRuleCode = e.AlertRule != null ? e.AlertRule.Code : string.Empty,
                AlertRuleName = e.AlertRule != null ? e.AlertRule.NameMk : string.Empty,
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
            })
            .ToListAsync(ct);
        return Result<List<AlertEventDto>>.Success(rows);
    }
}

public record AlertEventDto
{
    public Guid Id { get; init; }
    public Guid AlertRuleId { get; init; }
    public string AlertRuleCode { get; init; } = string.Empty;
    public string AlertRuleName { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public int Severity { get; init; }
    public string SeverityName { get; init; } = string.Empty;
    public int Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
}
