using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Queries.MozniMinusi;

/// <summary>
/// P4.3 — MozniMinusi (negative-stock reconciliation report).
///
/// Legacy ELON exposed this so the expert could quickly spot (Item, MRN, Batch)
/// triples where cumulative issues exceed cumulative receipts — either a data
/// entry mistake or an inventory sync problem. The report groups all
/// InventoryMovements by their logical key and highlights any group with a
/// negative net. It also reports any InventoryBalance.Quantity &lt; 0 directly,
/// which shouldn't happen under the domain rules but is worth surfacing.
/// </summary>
public sealed record MozniMinusiQuery() : IRequest<Result<MozniMinusiReport>>;

public sealed record MozniMinusiRow(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string? BatchNumber,
    string? MRN,
    decimal ReceiptsQty,
    decimal IssuesQty,
    decimal NetQty,
    decimal? CurrentBalance);

public sealed record MozniMinusiReport(
    IReadOnlyList<MozniMinusiRow> NegativeMovements,
    IReadOnlyList<MozniMinusiRow> NegativeBalances,
    int TotalChecked);

public sealed class MozniMinusiQueryHandler : IRequestHandler<MozniMinusiQuery, Result<MozniMinusiReport>>
{
    private readonly IApplicationDbContext _context;

    public MozniMinusiQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MozniMinusiReport>> Handle(MozniMinusiQuery _, CancellationToken ct)
    {
        // Pull movements + balances in one pass; expected volume per tenant is ~10K rows.
        var mvts = await _context.InventoryMovements
            .Where(m => !m.IsDeleted)
            .Select(m => new {
                m.ItemId, m.BatchNumber, m.MRN, m.Quantity, m.Type,
                m.Item.Code, m.Item.Name,
            })
            .ToListAsync(ct);

        var balances = await _context.InventoryBalances
            .Where(b => !b.IsDeleted)
            .Select(b => new {
                b.ItemId, b.BatchNumber, b.MRN, b.Quantity,
                b.Item.Code, b.Item.Name,
            })
            .ToListAsync(ct);

        bool IsReceipt(MovementType t) =>
            t == MovementType.Receipt ||
            t == MovementType.ProductionReceipt ||
            t == MovementType.Return;
        bool IsIssue(MovementType t) =>
            t == MovementType.Issue ||
            t == MovementType.ProductionIssue ||
            t == MovementType.Shipment;

        var groups = mvts
            .GroupBy(m => new { m.ItemId, m.BatchNumber, m.MRN })
            .Select(g => new MozniMinusiRow(
                g.Key.ItemId,
                g.First().Code,
                g.First().Name,
                g.Key.BatchNumber,
                g.Key.MRN,
                g.Where(m => IsReceipt(m.Type)).Sum(m => m.Quantity),
                g.Where(m => IsIssue(m.Type)).Sum(m => m.Quantity),
                g.Where(m => IsReceipt(m.Type)).Sum(m => m.Quantity)
                    - g.Where(m => IsIssue(m.Type)).Sum(m => m.Quantity),
                balances
                    .Where(b => b.ItemId == g.Key.ItemId && b.BatchNumber == g.Key.BatchNumber && b.MRN == g.Key.MRN)
                    .Sum(b => (decimal?)b.Quantity)))
            .Where(r => r.NetQty < 0)
            .OrderBy(r => r.NetQty)
            .ToList();

        var negBalances = balances
            .Where(b => b.Quantity < 0)
            .Select(b => new MozniMinusiRow(
                b.ItemId, b.Code, b.Name, b.BatchNumber, b.MRN,
                0m, 0m, 0m, b.Quantity))
            .OrderBy(r => r.CurrentBalance)
            .ToList();

        return Result<MozniMinusiReport>.Success(new MozniMinusiReport(groups, negBalances, mvts.Count));
    }
}
