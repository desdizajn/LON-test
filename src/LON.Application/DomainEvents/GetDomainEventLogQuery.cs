using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.DomainEvents;

/// <summary>
/// Phase 17 §E11 — read API over DomainEventLogs. Filters by event type +
/// time window; supports pagination. Newest-first.
/// </summary>
public record GetDomainEventLogQuery : ICommand<Result<List<DomainEventLogDto>>>
{
    public string? EventType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class GetDomainEventLogQueryHandler
    : ICommandHandler<GetDomainEventLogQuery, Result<List<DomainEventLogDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetDomainEventLogQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<DomainEventLogDto>>> Handle(GetDomainEventLogQuery request, CancellationToken ct)
    {
        var query = _context.DomainEventLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.EventType))
            query = query.Where(e => e.EventType == request.EventType);
        if (request.From.HasValue) query = query.Where(e => e.OccurredAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(e => e.OccurredAt <= request.To.Value);

        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var skip = Math.Max(0, (request.Page - 1) * pageSize);
        var rows = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(e => new DomainEventLogDto
            {
                Id = e.Id,
                EventId = e.EventId,
                EventType = e.EventType,
                OccurredAt = e.OccurredAt,
                PayloadJson = e.PayloadJson,
                Status = e.Status,
            })
            .ToListAsync(ct);
        return Result<List<DomainEventLogDto>>.Success(rows);
    }
}

public record DomainEventLogDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string PayloadJson { get; init; } = "{}";
    public string Status { get; init; } = string.Empty;
}
