using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.Podelba;

/// <summary>
/// P15.8 — Podelba (distribution to sub-contractor producers). Splits one
/// tenant-held <see cref="InventoryBalance"/> into per-producer siblings,
/// each tagged with <see cref="InventoryBalance.AssignedProducerId"/>.
/// Physical inventory stays at the same location; the split is logical,
/// answering "how much of this batch is earmarked for Producer X".
///
/// <para>
/// Legacy ELON <c>frmPodeliBaranjaBrz</c> produced one <c>LagerMaterijali</c>
/// row per (closure, producer, size). We preserve the same semantic on
/// <see cref="InventoryBalance"/>: source row decrements; one sibling per
/// producer increments (or is created). Source stays at qty 0 so audit can
/// trace "this row was fully distributed on day X".
/// </para>
///
/// <para>Rules:</para>
/// <list type="bullet">
///   <item>Source balance must exist, be non-deleted, qty > 0.</item>
///   <item>Σ allocation qty must equal source qty exactly. Fractional
///         splits (95% + 5%) are allowed; short allocations are not —
///         the operator must either fully distribute or stop.</item>
///   <item>Each producer must be a <see cref="Partner"/> with
///         <see cref="PartnerType.Producer"/>. Supplier / Customer partners
///         are rejected so the flow can't be misused.</item>
///   <item>A sibling balance with identical natural key (item, location,
///         batch, MRN, UoM, QualityStatus, LonProcessState, producer) is
///         incremented instead of duplicated — idempotent re-runs.</item>
///   <item>One <see cref="InventoryMovement"/> of type Transfer per allocation
///         referencing the <c>PDL-</c>-numbered Podelba run.</item>
/// </list>
/// </summary>
public record PodelbaCommand : ICommand<Result<Guid>>
{
    public Guid SourceBalanceId { get; init; }
    public List<PodelbaAllocation> Allocations { get; init; } = new();
}

public record PodelbaAllocation
{
    public Guid ProducerId { get; init; }
    public decimal Quantity { get; init; }
}

public class PodelbaCommandHandler : ICommandHandler<PodelbaCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public PodelbaCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(PodelbaCommand request, CancellationToken ct)
    {
        if (request.Allocations.Count == 0)
            return Result<Guid>.Failure("At least one allocation is required.");
        if (request.Allocations.Any(a => a.Quantity <= 0m))
            return Result<Guid>.Failure("Every allocation quantity must be positive.");

        var source = await _context.InventoryBalances
            .FirstOrDefaultAsync(b => b.Id == request.SourceBalanceId && !b.IsDeleted, ct);
        if (source is null)
            return Result<Guid>.Failure($"Source balance '{request.SourceBalanceId}' not found.");
        if (source.Quantity <= 0m)
            return Result<Guid>.Failure("Source balance has zero quantity — nothing to distribute.");

        var totalAlloc = request.Allocations.Sum(a => a.Quantity);
        if (totalAlloc != source.Quantity)
            return Result<Guid>.Failure(
                $"Allocation total {totalAlloc} must equal source quantity {source.Quantity} exactly (partial podelba not supported).");

        // Validate all producer IDs are distinct Partner rows of Type=Producer.
        var producerIds = request.Allocations.Select(a => a.ProducerId).Distinct().ToList();
        if (producerIds.Count != request.Allocations.Count)
            return Result<Guid>.Failure("Duplicate producer in allocations. Combine lines for the same producer.");
        var producers = await _context.Partners
            .Where(p => producerIds.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync(ct);
        if (producers.Count != producerIds.Count)
            return Result<Guid>.Failure("One or more producers not found or not accessible under current tenant.");
        var notProducers = producers.Where(p => p.Type != PartnerType.Producer).Select(p => p.Code).ToList();
        if (notProducers.Count > 0)
            return Result<Guid>.Failure(
                $"Partners {string.Join(", ", notProducers)} are not of type Producer; refusing podelba.");

        // Atomic split. Source drains to 0; siblings consolidated via DbSet.Local first.
        var podelbaNumber = $"PDL-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var movementDate = DateTime.UtcNow;

        foreach (var alloc in request.Allocations)
        {
            // Look for an existing sibling with same natural key + producer.
            var sibling = _context.InventoryBalances.Local
                .Where(b => !b.IsDeleted
                             && b.ItemId == source.ItemId
                             && b.LocationId == source.LocationId
                             && b.BatchNumber == source.BatchNumber
                             && b.MRN == source.MRN
                             && b.UoMId == source.UoMId
                             && b.QualityStatus == source.QualityStatus
                             && b.LonProcessState == source.LonProcessState
                             && b.AssignedProducerId == alloc.ProducerId)
                .FirstOrDefault();

            if (sibling is null)
            {
                sibling = await _context.InventoryBalances
                    .FirstOrDefaultAsync(b => !b.IsDeleted
                                               && b.ItemId == source.ItemId
                                               && b.LocationId == source.LocationId
                                               && b.BatchNumber == source.BatchNumber
                                               && b.MRN == source.MRN
                                               && b.UoMId == source.UoMId
                                               && b.QualityStatus == source.QualityStatus
                                               && b.LonProcessState == source.LonProcessState
                                               && b.AssignedProducerId == alloc.ProducerId, ct);
            }

            if (sibling is null)
            {
                sibling = new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    ItemId = source.ItemId,
                    LocationId = source.LocationId,
                    UoMId = source.UoMId,
                    BatchNumber = source.BatchNumber,
                    MRN = source.MRN,
                    Quantity = 0m,
                    QualityStatus = source.QualityStatus,
                    ExpiryDate = source.ExpiryDate,
                    LonProcessState = source.LonProcessState,
                    AssignedProducerId = alloc.ProducerId
                };
                await _context.InventoryBalances.AddAsync(sibling, ct);
            }

            sibling.Quantity += alloc.Quantity;

            await _context.InventoryMovements.AddAsync(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = podelbaNumber,
                MovementDate = movementDate,
                Type = MovementType.Transfer,
                ItemId = source.ItemId,
                BatchNumber = source.BatchNumber,
                MRN = source.MRN,
                FromLocationId = source.LocationId,
                ToLocationId = source.LocationId,
                Quantity = alloc.Quantity,
                UoMId = source.UoMId,
                ReferenceNumber = $"Podelba:{alloc.ProducerId}",
                Notes = $"Podelba allocation to producer {alloc.ProducerId}"
            }, ct);
        }

        source.Quantity = 0m;

        await _context.SaveChangesAsync(ct);
        // Return source id so the caller can re-query the sibling tree (they all share natural key).
        return Result<Guid>.Success(source.Id);
    }
}
