using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.DistributeMaterialAcrossOrders;

/// <summary>
/// P15.16 — legacy <c>frmDodeluvanjeNormativiOdU5M</c> (multi-product
/// distribution). Operator picks ONE imported material (from a specific
/// <see cref="Domain.Entities.Customs.CustomsDeclarationLine"/>) and spreads
/// the imported quantity across multiple open production orders, setting
/// a picked Normativ-per-unit on each.
///
/// <para>Legacy modes (fraIzberi option group):</para>
/// <list type="bullet">
///   <item>NewDistribution (lIzberi=1) — WIPE unselected POs' lines for this
///         material, redistribute FULL KolMatU by PO.OrderQuantity:
///         <c>NormativProsek = Σ KolMat / Σ Kol</c>.</item>
///   <item>FillGaps (lIzberi=2) — only fill POs that currently have no
///         Normativ for this material. Uses KolMatR remainder.</item>
///   <item>DistributeOverAll (lIzberi=3) — add / subtract against existing
///         lines on ALL selected POs.</item>
/// </list>
///
/// <para>Second-pass correction: KolMat rounded to 2 decimals, Normativ
/// re-derived as <c>KolMat / Kol</c>, and the LAST row absorbs cumulative
/// rounding drift so the sum matches the imported KolMatU exactly.</para>
/// </summary>
public record DistributeMaterialAcrossOrdersCommand : ICommand<Result<DistributeResult>>
{
    public Guid CustomsDeclarationLineId { get; init; }
    public DistributionMode Mode { get; init; } = DistributionMode.NewDistribution;
    public List<Guid> ProductionOrderIds { get; init; } = new();
}

public enum DistributionMode
{
    /// <summary>lIzberi=1 — full redistribution.</summary>
    NewDistribution = 1,
    /// <summary>lIzberi=2 — fill gaps only.</summary>
    FillGaps = 2,
    /// <summary>lIzberi=3 — adjust against existing on all selected.</summary>
    DistributeOverAll = 3
}

public record DistributeResult(
    decimal TotalMaterialQuantity,
    decimal TotalFGQuantity,
    decimal WeightedAverageNormativ,
    int LinesAffected);

public class DistributeMaterialAcrossOrdersCommandHandler
    : ICommandHandler<DistributeMaterialAcrossOrdersCommand, Result<DistributeResult>>
{
    private readonly IApplicationDbContext _context;

    public DistributeMaterialAcrossOrdersCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DistributeResult>> Handle(
        DistributeMaterialAcrossOrdersCommand request, CancellationToken ct)
    {
        if (request.ProductionOrderIds.Count == 0)
            return Result<DistributeResult>.Failure("At least one ProductionOrderId is required.");

        var declLine = await _context.CustomsDeclarationLines
            .Include(l => l.CustomsDeclaration)
            .FirstOrDefaultAsync(l => l.Id == request.CustomsDeclarationLineId && !l.IsDeleted, ct);
        if (declLine is null)
            return Result<DistributeResult>.Failure(
                $"CustomsDeclarationLine '{request.CustomsDeclarationLineId}' not found.");

        var materialId = declLine.ItemId;
        var importedQty = declLine.Quantity;

        if (importedQty <= 0m)
            return Result<DistributeResult>.Failure("Imported quantity must be positive.");

        // Fetch selected POs + their existing materials row for this item (if any).
        var pos = await _context.ProductionOrders
            .Include(p => p.Materials.Where(m => m.ItemId == materialId && !m.IsDeleted))
            .Where(p => request.ProductionOrderIds.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync(ct);

        if (pos.Count == 0)
            return Result<DistributeResult>.Failure("No selected production orders found in tenant scope.");

        var distinctUoM = await _context.CustomsDeclarationLines
            .Where(l => l.Id == declLine.Id)
            .Select(l => l.UoMId)
            .FirstAsync(ct);

        // Mode-specific target-PO selection
        List<ProductionOrder> targetPos = request.Mode switch
        {
            DistributionMode.FillGaps => pos.Where(p => !p.Materials.Any(m => m.ItemId == materialId)).ToList(),
            _ => pos
        };

        if (targetPos.Count == 0)
            return Result<DistributeResult>.Failure(
                "No eligible production orders after mode filter (FillGaps mode only targets POs without existing line).");

        // Total FG qty denominator — all selected POs contribute.
        var totalFg = targetPos.Sum(p => p.OrderQuantity);
        if (totalFg <= 0m)
            return Result<DistributeResult>.Failure("Selected production orders have zero total OrderQuantity.");

        // NewDistribution mode wipes existing unselected-for-this-mode lines
        // that reference the same material to match legacy "completely fresh
        // allocation" semantic. FillGaps / DistributeOverAll preserve them.
        if (request.Mode == DistributionMode.NewDistribution)
        {
            var unselectedPoIds = pos.Select(p => p.Id)
                .Except(targetPos.Select(p => p.Id))
                .ToList();
            if (unselectedPoIds.Count > 0)
            {
                var toDelete = await _context.ProductionOrderMaterials
                    .Where(m => unselectedPoIds.Contains(m.ProductionOrderId)
                                 && m.ItemId == materialId
                                 && !m.IsDeleted)
                    .ToListAsync(ct);
                foreach (var m in toDelete) m.IsDeleted = true;
            }
        }

        // Weighted-average normativ = imported / total FG
        var normativProsek = importedQty / totalFg;

        var affected = 0;
        decimal cumulativeMaterial = 0m;
        for (int i = 0; i < targetPos.Count; i++)
        {
            var po = targetPos[i];
            var plannedMaterial = Math.Round(po.OrderQuantity * normativProsek, 4, MidpointRounding.AwayFromZero);

            // Last row absorbs cumulative rounding drift so Σ = imported exactly.
            if (i == targetPos.Count - 1)
            {
                plannedMaterial = Math.Round(importedQty - cumulativeMaterial, 4, MidpointRounding.AwayFromZero);
            }
            cumulativeMaterial += plannedMaterial;

            var existing = po.Materials.FirstOrDefault(m => m.ItemId == materialId);
            if (existing is null)
            {
                var nextLine = (await _context.ProductionOrderMaterials
                    .Where(m => m.ProductionOrderId == po.Id && !m.IsDeleted)
                    .Select(m => (int?)m.LineNumber)
                    .MaxAsync(ct) ?? 0) + 1;
                await _context.ProductionOrderMaterials.AddAsync(new ProductionOrderMaterial
                {
                    Id = Guid.NewGuid(),
                    ProductionOrderId = po.Id,
                    LineNumber = nextLine,
                    ItemId = materialId,
                    RequiredQuantity = plannedMaterial,
                    IssuedQuantity = 0m,
                    ReservedQuantity = plannedMaterial,
                    UoMId = distinctUoM,
                    PlannedQuantityPerUnit = Math.Round(normativProsek, 6),
                }, ct);
            }
            else
            {
                // Legacy DistributeOverAll: add / subtract against existing.
                // NewDistribution & FillGaps overwrite cleanly.
                if (request.Mode == DistributionMode.DistributeOverAll)
                {
                    existing.RequiredQuantity += plannedMaterial;
                    existing.ReservedQuantity += plannedMaterial;
                }
                else
                {
                    existing.RequiredQuantity = plannedMaterial;
                    existing.ReservedQuantity = plannedMaterial;
                    existing.PlannedQuantityPerUnit = Math.Round(normativProsek, 6);
                }
            }
            affected++;
        }

        await _context.SaveChangesAsync(ct);
        return Result<DistributeResult>.Success(new DistributeResult(
            TotalMaterialQuantity: importedQty,
            TotalFGQuantity: totalFg,
            WeightedAverageNormativ: Math.Round(normativProsek, 6),
            LinesAffected: affected));
    }
}
