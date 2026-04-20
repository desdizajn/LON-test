using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Common.Queries;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.MasterData.Queries.GetItemImportAttributes;

/// <summary>
/// P6.31 — per-material import attributes report. For a single Item code, emits
/// the distinct (TariffCode, CountryOfOrigin, IsPreferentialOrigin, SupplierCode,
/// SupplierName) tuples observed across `CustomsDeclarationLine` rows whose
/// parent declaration registered an MRN still active in `MRNRegistry`
/// (IsActive=true). Alongside each tuple the report sums
/// `InventoryBalance.Quantity` across the MRN batches that carry that tuple —
/// the answer to "how much of this material is in stock, under which combo of
/// (tariff, country, preferential flag, supplier)?".
/// </summary>
public sealed record GetItemImportAttributesQuery(Guid ItemId)
    : IQuery<Result<ItemImportAttributesReport>>;

public sealed record ItemImportAttributesReport(
    Guid ItemId,
    string ItemCode,
    List<ItemImportAttributeRow> Rows);

public sealed record ItemImportAttributeRow(
    string? TariffCode,
    string? CountryOfOrigin,
    bool? IsPreferentialOrigin,
    Guid? SupplierId,
    string? SupplierCode,
    string? SupplierName,
    decimal? DutyRate,
    decimal? VatRate,
    int BatchCount,
    decimal AvailableQuantity);

public sealed class GetItemImportAttributesQueryHandler
    : IQueryHandler<GetItemImportAttributesQuery, Result<ItemImportAttributesReport>>
{
    private readonly IApplicationDbContext _context;

    public GetItemImportAttributesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ItemImportAttributesReport>> Handle(
        GetItemImportAttributesQuery request, CancellationToken ct)
    {
        var item = await _context.Items
            .Where(i => i.Id == request.ItemId)
            .Select(i => new { i.Id, i.Code })
            .FirstOrDefaultAsync(ct);
        if (item is null)
            return Result<ItemImportAttributesReport>.Failure("Item not found.");

        // Pull every declaration line for this item that maps onto an MRN still
        // carrying stock. Line → Declaration.MRN → MRNRegistry (must be Active)
        // → sum over InventoryBalance.Quantity where MRN column equals. Done
        // in two scoped queries so the per-MRN sum is computed exactly once.
        var lines = await _context.CustomsDeclarationLines
            .Where(l => l.ItemId == request.ItemId)
            .Select(l => new
            {
                l.TariffCode,
                l.CountryOfOrigin,
                l.IsPreferentialOrigin,
                l.DutyRate,
                l.VATRate,
                l.CustomsDeclaration.PartnerId,
                PartnerCode = l.CustomsDeclaration.Partner != null ? l.CustomsDeclaration.Partner.Code : null,
                PartnerName = l.CustomsDeclaration.Partner != null ? l.CustomsDeclaration.Partner.Name : null,
                Mrn = l.CustomsDeclaration.MRN
            })
            .ToListAsync(ct);

        if (lines.Count == 0)
        {
            return Result<ItemImportAttributesReport>.Success(
                new ItemImportAttributesReport(item.Id, item.Code, new List<ItemImportAttributeRow>()));
        }

        var mrns = lines.Select(x => x.Mrn).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();

        var activeMrns = await _context.MRNRegistries
            .Where(r => r.IsActive && mrns.Contains(r.MRN))
            .Select(r => r.MRN)
            .ToListAsync(ct);
        var activeMrnSet = activeMrns.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var balanceByMrn = await _context.InventoryBalances
            .Where(b => b.ItemId == request.ItemId && b.MRN != null && activeMrns.Contains(b.MRN!) && b.Quantity > 0m)
            .GroupBy(b => b.MRN!)
            .Select(g => new { MRN = g.Key, Qty = g.Sum(b => b.Quantity), BatchCount = g.Select(x => x.BatchNumber).Distinct().Count() })
            .ToListAsync(ct);
        var qtyByMrn = balanceByMrn.ToDictionary(x => x.MRN, x => (x.Qty, x.BatchCount), StringComparer.OrdinalIgnoreCase);

        var rows = lines
            .Where(x => !string.IsNullOrEmpty(x.Mrn) && activeMrnSet.Contains(x.Mrn))
            .GroupBy(x => new
            {
                x.TariffCode,
                x.CountryOfOrigin,
                x.IsPreferentialOrigin,
                x.PartnerId,
                x.PartnerCode,
                x.PartnerName,
                x.DutyRate,
                x.VATRate
            })
            .Select(g =>
            {
                var combined = g
                    .Select(x => qtyByMrn.TryGetValue(x.Mrn, out var v) ? v : (Qty: 0m, BatchCount: 0))
                    .Aggregate((a, b) => (a.Qty + b.Qty, a.BatchCount + b.BatchCount));
                return new ItemImportAttributeRow(
                    g.Key.TariffCode,
                    g.Key.CountryOfOrigin,
                    g.Key.IsPreferentialOrigin,
                    g.Key.PartnerId,
                    g.Key.PartnerCode,
                    g.Key.PartnerName,
                    g.Key.DutyRate,
                    g.Key.VATRate,
                    combined.BatchCount,
                    combined.Qty);
            })
            .OrderByDescending(r => r.AvailableQuantity)
            .ToList();

        return Result<ItemImportAttributesReport>.Success(
            new ItemImportAttributesReport(item.Id, item.Code, rows));
    }
}
