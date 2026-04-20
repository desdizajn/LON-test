using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.FinishedGoods;

// ─────────────────── DTOs returned to frontend ───────────────────

public sealed record AwaitingPackRow(
    Guid ProductionOrderId,
    string OrderNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal ProducedQuantity,
    decimal ShippedQuantity,
    decimal RemainingToPack,
    Guid UoMId,
    string UoMCode,
    DateTime? ActualEndDate,
    Guid? CustomerPartnerId,
    string? CustomerOrderNumber);

public sealed record PackagingStockRow(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    Guid UoMId,
    string UoMCode,
    decimal TotalOnHand,
    int LocationCount);

// ─────────────────── P9.1 — awaiting pack ───────────────────

/// <summary>
/// P9.1 — PO со Status=Completed каде производеното количество сè уште не е
/// покриено од ShipmentLine со тој ProductionOrder reference. Суб:
///   remaining = ProducedQuantity − SUM(ShipmentLine.Quantity WHERE PO=this).
/// Bидат само редови со remaining &gt; 0.
/// </summary>
public sealed record GetAwaitingPackQuery() : IRequest<Result<IReadOnlyList<AwaitingPackRow>>>;

public sealed class GetAwaitingPackHandler
    : IRequestHandler<GetAwaitingPackQuery, Result<IReadOnlyList<AwaitingPackRow>>>
{
    private readonly IApplicationDbContext _context;
    public GetAwaitingPackHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<AwaitingPackRow>>> Handle(GetAwaitingPackQuery request, CancellationToken ct)
    {
        var completed = ProductionOrderStatus.Completed;

        // The domain has no direct ShipmentLine.ProductionOrderId FK; the
        // natural join is through (ItemId, BatchNumber, MRN) coming from
        // ProductionReceipt. For this page a simpler "by item" approximation
        // works: total shipped of item ItemId via ShipmentLine where the
        // ShipmentLine row was sourced from this PO's ProductionReceipts.
        // We approximate by joining ShipmentLine on BatchNumber against
        // ProductionReceipt.BatchNumber for the same PO. If both sides are
        // non-null and match, the line counts as shipped output of the PO.
        var orders = await _context.ProductionOrders
            .AsNoTracking()
            .Where(po => po.Status == completed)
            .Select(po => new
            {
                po.Id,
                po.OrderNumber,
                po.ItemId,
                ItemCode = po.Item.Code,
                ItemName = po.Item.Name,
                po.ProducedQuantity,
                po.UoMId,
                UoMCode = po.UoM.Code,
                po.ActualEndDate,
                po.CustomerPartnerId,
                po.CustomerOrderNumber,
                Batches = _context.ProductionReceipts
                    .Where(pr => pr.ProductionOrderId == po.Id && pr.BatchNumber != null)
                    .Select(pr => pr.BatchNumber)
                    .Distinct()
                    .ToList(),
            })
            .ToListAsync(ct);

        var itemIds = orders.Select(o => o.ItemId).Distinct().ToList();

        // Pull all shipment lines for affected items once.
        var shipmentLines = await _context.ShipmentLines
            .AsNoTracking()
            .Where(sl => itemIds.Contains(sl.ItemId) && sl.BatchNumber != null)
            .Select(sl => new { sl.ItemId, sl.BatchNumber, sl.Quantity })
            .ToListAsync(ct);

        var rows = orders.Select(o =>
        {
            var batchSet = new HashSet<string>(o.Batches.Where(b => b != null)!, StringComparer.OrdinalIgnoreCase);
            var shipped = shipmentLines
                .Where(sl => sl.ItemId == o.ItemId && sl.BatchNumber != null && batchSet.Contains(sl.BatchNumber))
                .Sum(sl => sl.Quantity);
            var remaining = o.ProducedQuantity - shipped;
            return new AwaitingPackRow(
                o.Id, o.OrderNumber, o.ItemId, o.ItemCode, o.ItemName,
                o.ProducedQuantity, shipped, remaining,
                o.UoMId, o.UoMCode, o.ActualEndDate,
                o.CustomerPartnerId, o.CustomerOrderNumber);
        })
        .Where(r => r.RemainingToPack > 0m)
        .OrderByDescending(r => r.ActualEndDate ?? DateTime.MinValue)
        .ToList();

        return Result<IReadOnlyList<AwaitingPackRow>>.Success(rows);
    }
}

// ─────────────────── P9.6 — packaging stock ───────────────────

/// <summary>
/// P9.6 — роллуп на InventoryBalance за Items каде <see cref="ItemType.Packaging"/>.
/// Сумирано по ItemId (OK quality + InProduction/Imported/null process state
/// не се применуваат на packaging материјал, сумираме сè што не е exported/waste).
/// </summary>
public sealed record GetPackagingStockQuery() : IRequest<Result<IReadOnlyList<PackagingStockRow>>>;

public sealed class GetPackagingStockHandler
    : IRequestHandler<GetPackagingStockQuery, Result<IReadOnlyList<PackagingStockRow>>>
{
    private readonly IApplicationDbContext _context;
    public GetPackagingStockHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<IReadOnlyList<PackagingStockRow>>> Handle(GetPackagingStockQuery request, CancellationToken ct)
    {
        var packaging = ItemType.Packaging;
        var okQuality = QualityStatus.OK;
        var exported = LonProcessState.Exported;
        var waste = LonProcessState.Waste;

        // Pull packaging items (soft-delete already applied via global query
        // filter). Item.Type not IsActive/ItemType. Listed even when inventory
        // is empty so the report still surfaces the catalog.
        var items = await _context.Items
            .AsNoTracking()
            .Where(i => i.Type == packaging)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.Name,
                UoMId = i.BaseUoMId,
                UoMCode = i.BaseUoM.Code,
            })
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            return Result<IReadOnlyList<PackagingStockRow>>.Success(Array.Empty<PackagingStockRow>());
        }

        var itemIds = items.Select(i => i.Id).ToList();

        // Aggregate inventory. Project into anonymous first to keep LINQ-to-SQL
        // simple (see Phase-11 Pareto lesson).
        var raw = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => itemIds.Contains(b.ItemId)
                && b.QualityStatus == okQuality
                && b.LonProcessState != exported
                && b.LonProcessState != waste)
            .GroupBy(b => b.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                Total = g.Sum(x => (decimal?)x.Quantity) ?? 0m,
                Locations = g.Select(x => x.LocationId).Distinct().Count(),
            })
            .ToListAsync(ct);

        var byItem = raw.ToDictionary(r => r.ItemId);

        var rows = items
            .Select(i =>
            {
                byItem.TryGetValue(i.Id, out var a);
                return new PackagingStockRow(
                    i.Id, i.Code, i.Name, i.UoMId, i.UoMCode,
                    a?.Total ?? 0m, a?.Locations ?? 0);
            })
            .OrderByDescending(r => r.TotalOnHand)
            .ThenBy(r => r.ItemCode)
            .ToList();

        return Result<IReadOnlyList<PackagingStockRow>>.Success(rows);
    }
}
