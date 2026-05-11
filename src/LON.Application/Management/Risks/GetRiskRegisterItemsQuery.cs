using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Management;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Risks;

public sealed record GetRiskRegisterItemsQuery(RiskKind? Kind)
    : IRequest<Result<IReadOnlyList<RiskRegisterItemDto>>>;

public class GetRiskRegisterItemsQueryHandler
    : IRequestHandler<GetRiskRegisterItemsQuery, Result<IReadOnlyList<RiskRegisterItemDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetRiskRegisterItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<RiskRegisterItemDto>>> Handle(
        GetRiskRegisterItemsQuery request,
        CancellationToken ct)
    {
        var query = _context.RiskRegisterItems.AsQueryable();
        if (request.Kind.HasValue)
            query = query.Where(r => r.Kind == request.Kind.Value);

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => RiskRegisterItemDto.From(r))
            .ToListAsync(ct);

        return Result<IReadOnlyList<RiskRegisterItemDto>>.Success(rows);
    }
}

public sealed record GetRiskRegisterItemByIdQuery(Guid Id)
    : IRequest<Result<RiskRegisterItemDto>>;

public class GetRiskRegisterItemByIdQueryHandler
    : IRequestHandler<GetRiskRegisterItemByIdQuery, Result<RiskRegisterItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRiskRegisterItemByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<RiskRegisterItemDto>> Handle(
        GetRiskRegisterItemByIdQuery request,
        CancellationToken ct)
    {
        var entity = await _context.RiskRegisterItems
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (entity is null)
            return Result<RiskRegisterItemDto>.Failure($"RiskRegisterItem '{request.Id}' not found.");
        return Result<RiskRegisterItemDto>.Success(RiskRegisterItemDto.From(entity));
    }
}
