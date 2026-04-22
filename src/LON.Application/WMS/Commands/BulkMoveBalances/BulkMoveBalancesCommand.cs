using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.BulkMoveBalances;

/// <summary>
/// P14.7 — bulk move N selected InventoryBalance rows to one target location
/// in a single atomic call. Companion to <see cref="MassLocationTransfer.MassLocationTransferCommand"/>:
/// where the latter is predicate-based, this command is selection-based —
/// the UI hands over the exact set of `BalanceIds` the operator picked.
///
/// Same target consolidation rules as MassLocationTransferCommand:
/// - DbSet.Local first, then DB lookup, else create new row keyed on the
///   natural key (Item, Location, Batch, MRN, UoM, QualityStatus).
/// - Source rows already at the target are silently skipped.
/// - Drained sources left at Quantity = 0 for audit parity.
/// </summary>
public sealed record BulkMoveBalancesCommand(
    IReadOnlyList<Guid> BalanceIds,
    Guid TargetLocationId,
    string? Reason = null,
    DateTime? MovementDate = null) : ICommand<Result<BulkMoveBalancesResult>>;

public sealed record BulkMoveBalancesResult(
    int BalancesMoved,
    int BalancesSkipped,
    decimal TotalQuantityMoved,
    Guid TargetLocationId);

public sealed class BulkMoveBalancesCommandHandler
    : ICommandHandler<BulkMoveBalancesCommand, Result<BulkMoveBalancesResult>>
{
    private readonly IApplicationDbContext _context;

    public BulkMoveBalancesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BulkMoveBalancesResult>> Handle(
        BulkMoveBalancesCommand request, CancellationToken cancellationToken)
    {
        if (request.BalanceIds is null || request.BalanceIds.Count == 0)
            return Result<BulkMoveBalancesResult>.Failure(
                ErrorCodes.TransferNoMatch,
                "At least one balance id is required.");

        if (request.TargetLocationId == Guid.Empty)
            return Result<BulkMoveBalancesResult>.Failure(
                ErrorCodes.LocationNotFound,
                "TargetLocationId is required.");

        var target = await _context.Locations
            .FirstOrDefaultAsync(l => l.Id == request.TargetLocationId && l.IsActive, cancellationToken);
        if (target is null)
            return Result<BulkMoveBalancesResult>.Failure(
                ErrorCodes.LocationNotFound,
                $"Target location '{request.TargetLocationId}' not found or inactive.");

        var ids = request.BalanceIds.Distinct().ToList();
        var balances = await _context.InventoryBalances
            .Where(b => ids.Contains(b.Id) && b.Quantity > 0m)
            .ToListAsync(cancellationToken);

        var skipped = ids.Count - balances.Count;

        // Skip rows already at the target.
        var movable = balances.Where(b => b.LocationId != target.Id).ToList();
        skipped += balances.Count - movable.Count;

        if (movable.Count == 0)
            return Result<BulkMoveBalancesResult>.Failure(
                ErrorCodes.TransferNoMatch,
                "No movable balances (zero quantity, missing, or already at target).");

        var whenUtc = request.MovementDate ?? DateTime.UtcNow;
        var movementEntities = new List<InventoryMovement>();
        decimal totalMoved = 0m;

        foreach (var src in movable)
        {
            var tgt = _context.InventoryBalances.Local.FirstOrDefault(b =>
                b.ItemId == src.ItemId &&
                b.LocationId == target.Id &&
                b.BatchNumber == src.BatchNumber &&
                b.MRN == src.MRN &&
                b.UoMId == src.UoMId &&
                b.QualityStatus == src.QualityStatus);

            if (tgt is null)
            {
                tgt = await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                        b.ItemId == src.ItemId &&
                        b.LocationId == target.Id &&
                        b.BatchNumber == src.BatchNumber &&
                        b.MRN == src.MRN &&
                        b.UoMId == src.UoMId &&
                        b.QualityStatus == src.QualityStatus,
                    cancellationToken);
            }

            if (tgt is null)
            {
                tgt = new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    ItemId = src.ItemId,
                    LocationId = target.Id,
                    BatchNumber = src.BatchNumber,
                    MRN = src.MRN,
                    Quantity = src.Quantity,
                    UoMId = src.UoMId,
                    QualityStatus = src.QualityStatus,
                    ExpiryDate = src.ExpiryDate,
                    LonProcessState = src.LonProcessState
                };
                await _context.InventoryBalances.AddAsync(tgt, cancellationToken);
            }
            else
            {
                tgt.AddQuantity(src.Quantity);
                if (!tgt.LonProcessState.HasValue && src.LonProcessState.HasValue)
                    tgt.LonProcessState = src.LonProcessState;
            }

            var moved = src.Quantity;
            movementEntities.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"BMV-{whenUtc:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = whenUtc,
                Type = MovementType.Transfer,
                ItemId = src.ItemId,
                BatchNumber = src.BatchNumber,
                MRN = src.MRN,
                FromLocationId = src.LocationId,
                ToLocationId = target.Id,
                Quantity = moved,
                UoMId = src.UoMId,
                ReferenceNumber = request.Reason,
                Notes = request.Reason,
            });

            src.Quantity = 0m;
            totalMoved += moved;
        }

        _context.InventoryMovements.AddRange(movementEntities);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<BulkMoveBalancesResult>.Success(new BulkMoveBalancesResult(
            movable.Count, skipped, totalMoved, target.Id));
    }
}
