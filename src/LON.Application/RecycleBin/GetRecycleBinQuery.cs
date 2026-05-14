using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.RecycleBin;

/// <summary>
/// Phase 17 §E14 — list soft-deleted ClientOrders (the only ISoftDeletable
/// entity surfaced in v1 recycle bin; other entities follow post-v1).
/// </summary>
public record GetRecycleBinQuery : ICommand<Result<List<RecycleBinRowDto>>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class GetRecycleBinQueryHandler
    : ICommandHandler<GetRecycleBinQuery, Result<List<RecycleBinRowDto>>>
{
    private readonly IApplicationDbContext _context;
    public GetRecycleBinQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<RecycleBinRowDto>>> Handle(GetRecycleBinQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var skip = Math.Max(0, (request.Page - 1) * pageSize);

        var rows = await _context.ClientOrders
            .IgnoreQueryFilters()
            .Where(o => o.IsDeleted)
            .OrderByDescending(o => o.DeletedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(o => new RecycleBinRowDto
            {
                EntityType = "ClientOrder",
                EntityId = o.Id,
                Label = o.OrderNumber,
                DeletedAt = o.DeletedAt,
                DeletedBy = o.DeletedBy,
                AdditionalInfo = o.CancellationReason,
            })
            .ToListAsync(ct);

        return Result<List<RecycleBinRowDto>>.Success(rows);
    }
}

public record RecycleBinRowDto
{
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Label { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
    public string? AdditionalInfo { get; init; }
}
