using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Common.Queries;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E1 — fetch one ClientOrder with its FinishedGoods.
/// Used by /orders/:id hub page.
/// </summary>
public record GetClientOrderByIdQuery(Guid Id) : IQuery<Result<ClientOrderDto>>;

public class GetClientOrderByIdQueryHandler
    : IQueryHandler<GetClientOrderByIdQuery, Result<ClientOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetClientOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClientOrderDto>> Handle(GetClientOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.ClientOrders
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (order is null)
            return Result<ClientOrderDto>.Failure($"ClientOrder '{request.Id}' does not exist.");

        var customerName = await _context.Partners
            .Where(p => p.Id == order.CustomerPartnerId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var lonAuthNo = await _context.LONAuthorizations
            .Where(a => a.Id == order.LONAuthorizationId)
            .Select(a => a.AuthorizationNumber)
            .FirstOrDefaultAsync(cancellationToken);

        // Phase 17 cutover: IgnoreQueryFilters for Item / UoM lookups so
        // FGs whose master Item is Arhivirano=1 (legacy soft-delete) still
        // render with full code/name. Same pattern as the Materials tab fix.
        var fgs = await _context.ClientOrderFinishedGoods
            .Where(g => g.ClientOrderId == order.Id)
            .Select(g => new
            {
                Fg = g,
                ItemCode = _context.Items.IgnoreQueryFilters().Where(i => i.Id == g.ItemId).Select(i => i.Code).FirstOrDefault(),
                ItemName = _context.Items.IgnoreQueryFilters().Where(i => i.Id == g.ItemId).Select(i => i.Name).FirstOrDefault(),
                UoMCode = _context.UnitsOfMeasure.IgnoreQueryFilters().Where(u => u.Id == g.UoMId).Select(u => u.Code).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var fgDtos = fgs.Select(r => new ClientOrderFinishedGoodDto(
            r.Fg.Id, r.Fg.ItemId, r.ItemCode, r.ItemName,
            r.Fg.Quantity, r.Fg.UoMId, r.UoMCode, r.Fg.BOMId,
            r.Fg.UnitPriceForeign, r.Fg.Currency, r.Fg.Notes)).ToList();

        var dto = new ClientOrderDto(
            order.Id,
            order.OrderNumber,
            order.CustomerPartnerId,
            customerName,
            order.LONAuthorizationId,
            lonAuthNo,
            order.CustomerOrderReference,
            order.OrderDate,
            order.RequestedShipDate,
            (int)order.Status,
            order.Status.ToString(),
            order.Notes,
            order.CancellationReason,
            order.CreatedAt,
            order.CreatedBy,
            fgDtos);

        return Result<ClientOrderDto>.Success(dto);
    }
}
