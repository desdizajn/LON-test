using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Common.Queries;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E1 — list ClientOrders with optional filters.
/// Returns ClientOrderSummaryDto (header data + counts).
/// </summary>
public record GetClientOrdersQuery : IQuery<Result<IReadOnlyList<ClientOrderSummaryDto>>>
{
    public int? Status { get; init; }
    public Guid? CustomerPartnerId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool IncludeCancelled { get; init; } = false;
}

public class GetClientOrdersQueryHandler
    : IQueryHandler<GetClientOrdersQuery, Result<IReadOnlyList<ClientOrderSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetClientOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<ClientOrderSummaryDto>>> Handle(
        GetClientOrdersQuery request, CancellationToken cancellationToken)
    {
        var q = _context.ClientOrders.AsQueryable();

        if (!request.IncludeCancelled)
            q = q.Where(o => o.Status != ClientOrderStatus.Cancelled);

        if (request.Status.HasValue)
            q = q.Where(o => (int)o.Status == request.Status.Value);
        if (request.CustomerPartnerId.HasValue)
            q = q.Where(o => o.CustomerPartnerId == request.CustomerPartnerId.Value);
        if (request.FromDate.HasValue)
            q = q.Where(o => o.OrderDate >= request.FromDate.Value);
        if (request.ToDate.HasValue)
            q = q.Where(o => o.OrderDate <= request.ToDate.Value);

        // Join Partner + LONAuthorization for friendly display fields.
        var rows = await q
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                Order = o,
                CustomerName = _context.Partners
                    .Where(p => p.Id == o.CustomerPartnerId)
                    .Select(p => p.Name)
                    .FirstOrDefault(),
                LonAuthNo = _context.LONAuthorizations
                    .Where(a => a.Id == o.LONAuthorizationId)
                    .Select(a => a.AuthorizationNumber)
                    .FirstOrDefault(),
                FgCount = _context.ClientOrderFinishedGoods.Count(g => g.ClientOrderId == o.Id),
                DeclCount = _context.CustomsDeclarations.Count(d => d.ClientOrderId == o.Id),
                PoCount = _context.ProductionOrders.Count(p => p.ClientOrderId == o.Id),
                ShipCount = _context.Shipments.Count(s => s.ClientOrderId == o.Id),
            })
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(r => new ClientOrderSummaryDto(
            r.Order.Id,
            r.Order.OrderNumber,
            r.Order.CustomerPartnerId,
            r.CustomerName,
            r.Order.LONAuthorizationId,
            r.LonAuthNo,
            r.Order.CustomerOrderReference,
            r.Order.OrderDate,
            r.Order.RequestedShipDate,
            (int)r.Order.Status,
            r.Order.Status.ToString(),
            r.FgCount,
            r.DeclCount,
            r.PoCount,
            r.ShipCount)).ToList();

        return Result<IReadOnlyList<ClientOrderSummaryDto>>.Success(dtos);
    }
}
