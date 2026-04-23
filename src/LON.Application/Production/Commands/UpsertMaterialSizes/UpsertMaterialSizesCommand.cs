using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.UpsertMaterialSizes;

/// <summary>
/// P15.16.1 — upsert the per-size breakdown of a
/// <see cref="ProductionOrderMaterial"/>. Atomically replaces every child
/// <see cref="ProductionOrderMaterialSize"/> row and:
/// <list type="bullet">
///   <item>Enforces Σ Quantity over sizes == PO.OrderQuantity (legacy
///         subVelicini.Kol_Exit "ПРЕГОЛЕМА КОЛИЧИНА!!" guard).</item>
///   <item>Recomputes parent's <c>RequiredQuantity</c> as the weighted
///         sum <c>Σ (Quantity × NormativPerUnit)</c>. Matches legacy
///         <c>NormativPros = SumOfKolMat / SumOfKol</c> + KolMatVK
///         total write-through.</item>
///   <item>Sets <see cref="ProductionOrderMaterial.HasSizeBreakdown"/> to
///         true (legacy <c>VeliciniDaNe</c>) so the material-issue flow
///         knows to read sizes instead of the flat parent Normativ.</item>
/// </list>
/// </summary>
public record UpsertMaterialSizesCommand : ICommand<Result<UpsertSizesResult>>
{
    public Guid ProductionOrderMaterialId { get; init; }
    public List<SizeLine> Sizes { get; init; } = new();
}

public record SizeLine
{
    public int SizeOrdinal { get; init; }
    public string SizeLabel { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal NormativPerUnit { get; init; }
}

public record UpsertSizesResult(
    Guid ProductionOrderMaterialId,
    int SizesCount,
    decimal TotalFGQuantity,
    decimal WeightedAverageNormativ,
    decimal NewRequiredQuantity);

public class UpsertMaterialSizesCommandHandler
    : ICommandHandler<UpsertMaterialSizesCommand, Result<UpsertSizesResult>>
{
    private readonly IApplicationDbContext _context;

    public UpsertMaterialSizesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UpsertSizesResult>> Handle(UpsertMaterialSizesCommand request, CancellationToken ct)
    {
        if (request.Sizes.Count == 0)
            return Result<UpsertSizesResult>.Failure("At least one size row is required.");

        if (request.Sizes.Any(s => string.IsNullOrWhiteSpace(s.SizeLabel)))
            return Result<UpsertSizesResult>.Failure("Every size row must have a SizeLabel.");
        if (request.Sizes.Any(s => s.Quantity <= 0m || s.NormativPerUnit < 0m))
            return Result<UpsertSizesResult>.Failure("Quantity must be positive and NormativPerUnit non-negative.");

        var ordinals = request.Sizes.Select(s => s.SizeOrdinal).Distinct().ToList();
        if (ordinals.Count != request.Sizes.Count)
            return Result<UpsertSizesResult>.Failure("Duplicate SizeOrdinal in payload — each size must be unique.");
        var labels = request.Sizes.Select(s => s.SizeLabel.Trim().ToUpperInvariant()).Distinct().ToList();
        if (labels.Count != request.Sizes.Count)
            return Result<UpsertSizesResult>.Failure("Duplicate SizeLabel in payload — each size must be unique.");

        var material = await _context.ProductionOrderMaterials
            .Include(m => m.ProductionOrder)
            .Include(m => m.Sizes)
            .FirstOrDefaultAsync(m => m.Id == request.ProductionOrderMaterialId && !m.IsDeleted, ct);
        if (material is null)
            return Result<UpsertSizesResult>.Failure(
                $"ProductionOrderMaterial '{request.ProductionOrderMaterialId}' not found.");

        var poQty = material.ProductionOrder.OrderQuantity;
        var sumKol = request.Sizes.Sum(s => s.Quantity);
        // Legacy overdraw guard: Σ Kol must equal PO.OrderQuantity exactly.
        // Allow a small rounding tolerance for decimal-entry drift.
        if (Math.Abs(sumKol - poQty) > 0.01m)
            return Result<UpsertSizesResult>.Failure(
                $"Σ size quantities ({sumKol}) must equal PO.OrderQuantity ({poQty}). ПРЕГОЛЕМА или ПРЕМАЛА КОЛИЧИНА!");

        // Atomic replace: soft-delete existing sizes, insert fresh.
        foreach (var existing in material.Sizes)
            existing.IsDeleted = true;

        decimal sumKolMat = 0m;
        foreach (var s in request.Sizes)
        {
            var kolMat = Math.Round(s.Quantity * s.NormativPerUnit, 4, MidpointRounding.AwayFromZero);
            sumKolMat += kolMat;
            await _context.ProductionOrderMaterialSizes.AddAsync(new ProductionOrderMaterialSize
            {
                Id = Guid.NewGuid(),
                ProductionOrderMaterialId = material.Id,
                SizeOrdinal = s.SizeOrdinal,
                SizeLabel = s.SizeLabel.Trim(),
                Quantity = s.Quantity,
                NormativPerUnit = Math.Round(s.NormativPerUnit, 6, MidpointRounding.AwayFromZero),
                TotalMaterialQuantity = kolMat
            }, ct);
        }

        // Parent roll-up: weighted average back-propagation + total required.
        var weightedAvgNormativ = sumKol > 0m
            ? Math.Round(sumKolMat / sumKol, 6, MidpointRounding.AwayFromZero)
            : 0m;
        material.RequiredQuantity = sumKolMat;
        material.ReservedQuantity = Math.Max(material.ReservedQuantity, sumKolMat - material.IssuedQuantity);
        material.HasSizeBreakdown = true;

        await _context.SaveChangesAsync(ct);

        return Result<UpsertSizesResult>.Success(new UpsertSizesResult(
            material.Id, request.Sizes.Count, sumKol, weightedAvgNormativ, sumKolMat));
    }
}

/// <summary>
/// P15.16.1 — clear the per-size breakdown on a material. Resets
/// <c>HasSizeBreakdown=false</c> and soft-deletes every child size row.
/// The parent <c>RequiredQuantity</c> is left as-is — caller decides
/// whether to re-seed from BOM or leave the last computed value.
/// </summary>
public record ClearMaterialSizesCommand(Guid ProductionOrderMaterialId) : ICommand<Result<int>>;

public class ClearMaterialSizesCommandHandler : ICommandHandler<ClearMaterialSizesCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;
    public ClearMaterialSizesCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<int>> Handle(ClearMaterialSizesCommand request, CancellationToken ct)
    {
        var material = await _context.ProductionOrderMaterials
            .Include(m => m.Sizes)
            .FirstOrDefaultAsync(m => m.Id == request.ProductionOrderMaterialId && !m.IsDeleted, ct);
        if (material is null)
            return Result<int>.Failure($"ProductionOrderMaterial '{request.ProductionOrderMaterialId}' not found.");

        int n = 0;
        foreach (var s in material.Sizes)
        {
            if (!s.IsDeleted) { s.IsDeleted = true; n++; }
        }
        material.HasSizeBreakdown = false;
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(n);
    }
}
