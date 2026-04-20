using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Queries.GetProductionShortage;

/// <summary>
/// P8.5 — Material shortage for active production orders.
///
/// Aggregates <c>ProductionOrderMaterial</c> rows across every PO in
/// Draft/Released/InProgress, sums remaining requirement (Required − Issued)
/// per material ItemId, subtracts currently-available inventory (sum of
/// <c>InventoryBalance.Quantity</c> for QualityStatus OK/None and
/// LonProcessState Imported or null — i.e. stock usable for issue), and
/// surfaces only material items where the net result is negative.
/// </summary>
public sealed record GetProductionShortageQuery() : IRequest<Result<ProductionShortageReport>>;

public sealed record ProductionShortagePoRef(
    Guid ProductionOrderId,
    string OrderNumber,
    DateTime PlannedStartDate,
    DateTime PlannedEndDate,
    decimal RemainingRequirement);

public sealed record ProductionShortageRow(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid UoMId,
    string UoMCode,
    decimal TotalRequiredRemaining,
    decimal TotalAvailable,
    decimal Deficit,
    IReadOnlyList<ProductionShortagePoRef> AffectedOrders);

public sealed record ProductionShortageReport(
    IReadOnlyList<ProductionShortageRow> Rows,
    int TotalActiveOrders,
    int MaterialsShort);

public sealed class GetProductionShortageQueryHandler
    : IRequestHandler<GetProductionShortageQuery, Result<ProductionShortageReport>>
{
    private readonly IApplicationDbContext _context;

    public GetProductionShortageQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductionShortageReport>> Handle(
        GetProductionShortageQuery request,
        CancellationToken cancellationToken)
    {
        var activePo = ProductionOrderStatus.InProgress;
        var draft = ProductionOrderStatus.Draft;
        var released = ProductionOrderStatus.Released;

        var activeOrders = await _context.ProductionOrders
            .AsNoTracking()
            .Where(po => po.Status == draft || po.Status == released || po.Status == activePo)
            .Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.PlannedStartDate,
                po.PlannedEndDate,
            })
            .ToListAsync(cancellationToken);

        var activeOrderIds = activeOrders.Select(o => o.Id).ToHashSet();
        if (activeOrderIds.Count == 0)
        {
            return Result<ProductionShortageReport>.Success(new ProductionShortageReport(
                Array.Empty<ProductionShortageRow>(), 0, 0));
        }

        var materials = await _context.ProductionOrderMaterials
            .AsNoTracking()
            .Where(m => activeOrderIds.Contains(m.ProductionOrderId))
            .Select(m => new
            {
                m.ProductionOrderId,
                m.ItemId,
                m.UoMId,
                UoMCode = m.UoM.Code,
                ItemCode = m.Item.Code,
                ItemName = m.Item.Name,
                m.RequiredQuantity,
                m.IssuedQuantity,
            })
            .ToListAsync(cancellationToken);

        if (materials.Count == 0)
        {
            return Result<ProductionShortageReport>.Success(new ProductionShortageReport(
                Array.Empty<ProductionShortageRow>(), activeOrders.Count, 0));
        }

        var itemIds = materials.Select(m => m.ItemId).Distinct().ToList();

        // Available inventory for issue: OK/None quality + Imported or null process-state
        var okQuality = QualityStatus.OK;
        var imported = LonProcessState.Imported;
        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => itemIds.Contains(b.ItemId)
                && b.QualityStatus == okQuality
                && (b.LonProcessState == imported || b.LonProcessState == null))
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, Available = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Available, cancellationToken);

        var orderLookup = activeOrders.ToDictionary(o => o.Id);

        var rows = materials
            .GroupBy(m => new { m.ItemId, m.ItemCode, m.ItemName, m.UoMId, m.UoMCode })
            .Select(g =>
            {
                var remainingRequirement = g.Sum(x => Math.Max(0m, x.RequiredQuantity - x.IssuedQuantity));
                var available = balances.TryGetValue(g.Key.ItemId, out var a) ? a : 0m;
                var deficit = remainingRequirement - available;
                var affected = g
                    .Where(x => x.RequiredQuantity - x.IssuedQuantity > 0m)
                    .Select(x =>
                    {
                        var po = orderLookup[x.ProductionOrderId];
                        return new ProductionShortagePoRef(
                            x.ProductionOrderId,
                            po.OrderNumber,
                            po.PlannedStartDate,
                            po.PlannedEndDate,
                            x.RequiredQuantity - x.IssuedQuantity);
                    })
                    .OrderBy(x => x.PlannedEndDate)
                    .ToList();

                return new ProductionShortageRow(
                    g.Key.ItemId,
                    g.Key.ItemCode,
                    g.Key.ItemName,
                    g.Key.UoMId,
                    g.Key.UoMCode,
                    remainingRequirement,
                    available,
                    deficit,
                    affected);
            })
            .Where(r => r.Deficit > 0m)
            .OrderByDescending(r => r.Deficit)
            .ToList();

        return Result<ProductionShortageReport>.Success(new ProductionShortageReport(
            rows,
            activeOrders.Count,
            rows.Count));
    }
}
