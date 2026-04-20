using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.MassLocationTransfer;

/// <summary>
/// P5.2.7 — filter inventory across any combination of (Item, Batch, MRN,
/// source Warehouse/Location, QualityStatus, LonProcessState) and transfer
/// every match to a single target <see cref="Domain.Entities.MasterData.Location"/>.
///
/// Differs from <see cref="MoveBatchAcrossStages.MoveBatchAcrossStagesCommand"/>
/// which is batch-scoped and target-stage-scoped; this one is an arbitrary
/// predicate + explicit target location — the bulk analogue of individual
/// transfers.
///
/// Contract:
/// - At least one filter field must be supplied; otherwise the handler
///   refuses to move every positive balance in the tenant.
/// - <c>TargetLocationId</c> is mandatory; the caller picks a concrete bin.
/// - Source rows already at the target location are skipped (no self-transfer).
/// - Drained source rows are left at <c>Quantity = 0</c> for audit.
/// - Balance consolidation mirrors MoveBatch: DbSet.Local first (consolidate
///   within the same command), then DB FirstOrDefaultAsync (consolidate with
///   pre-existing untracked rows), else a new row keyed on natural key.
/// </summary>
public record MassLocationTransferCommand(
    Guid TargetLocationId,
    Guid? ItemId = null,
    string? BatchNumber = null,
    string? MRN = null,
    Guid? SourceWarehouseId = null,
    Guid? SourceLocationId = null,
    QualityStatus? QualityStatus = null,
    LonProcessState? LonProcessState = null,
    DateTime? MovementDate = null,
    string? Reason = null) : ICommand<Result<MassTransferResult>>;

public sealed record MassTransferResult(
    int BalancesMoved,
    decimal TotalQuantityMoved,
    Guid TargetLocationId,
    IReadOnlyList<MassTransferMovement> Movements);

public sealed record MassTransferMovement(
    Guid MovementId,
    string MovementNumber,
    Guid ItemId,
    string? BatchNumber,
    string? MRN,
    Guid FromLocationId,
    Guid ToLocationId,
    decimal Quantity);

public class MassLocationTransferCommandHandler
    : ICommandHandler<MassLocationTransferCommand, Result<MassTransferResult>>
{
    private readonly IApplicationDbContext _context;

    public MassLocationTransferCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MassTransferResult>> Handle(
        MassLocationTransferCommand request, CancellationToken cancellationToken)
    {
        if (!HasAnyFilter(request))
            return Result<MassTransferResult>.Failure(
                "At least one filter criterion is required (itemId, batchNumber, mrn, sourceWarehouseId, sourceLocationId, qualityStatus, or lonProcessState).");

        if (request.TargetLocationId == Guid.Empty)
            return Result<MassTransferResult>.Failure("TargetLocationId is required.");

        var target = await _context.Locations
            .FirstOrDefaultAsync(l => l.Id == request.TargetLocationId && l.IsActive, cancellationToken);
        if (target is null)
            return Result<MassTransferResult>.Failure(
                $"Target location '{request.TargetLocationId}' not found or inactive.");

        var query = _context.InventoryBalances
            .Include(b => b.Location)
            .Where(b => b.Quantity > 0m);

        if (request.ItemId.HasValue)
            query = query.Where(b => b.ItemId == request.ItemId.Value);
        if (!string.IsNullOrWhiteSpace(request.BatchNumber))
        {
            var batch = request.BatchNumber.Trim();
            query = query.Where(b => b.BatchNumber == batch);
        }
        if (!string.IsNullOrWhiteSpace(request.MRN))
        {
            var mrn = request.MRN.Trim();
            query = query.Where(b => b.MRN == mrn);
        }
        if (request.SourceWarehouseId.HasValue)
            query = query.Where(b => b.Location.WarehouseId == request.SourceWarehouseId.Value);
        if (request.SourceLocationId.HasValue)
            query = query.Where(b => b.LocationId == request.SourceLocationId.Value);
        if (request.QualityStatus.HasValue)
            query = query.Where(b => b.QualityStatus == request.QualityStatus.Value);
        if (request.LonProcessState.HasValue)
            query = query.Where(b => b.LonProcessState == request.LonProcessState.Value);

        var balances = await query.ToListAsync(cancellationToken);

        // Skip rows already sitting at the target — they don't need moving,
        // and would otherwise trip the idempotency guard below.
        balances = balances.Where(b => b.LocationId != target.Id).ToList();

        if (balances.Count == 0)
            return Result<MassTransferResult>.Failure(
                "No positive-quantity inventory matched the filter (or every match already sits at the target).");

        var whenUtc = request.MovementDate ?? DateTime.UtcNow;

        var movements = new List<MassTransferMovement>();
        var movementEntities = new List<InventoryMovement>();
        decimal totalMoved = 0m;

        foreach (var src in balances)
        {
            // Target balance lookup: context local first (captures balances
            // we already touched in this handler), then DB.
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
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MTR-{whenUtc:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
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
                Notes = request.Reason
            };
            movementEntities.Add(movement);

            src.Quantity = 0m;
            totalMoved += moved;
            movements.Add(new MassTransferMovement(movement.Id, movement.MovementNumber,
                src.ItemId, src.BatchNumber, src.MRN, src.LocationId, target.Id, moved));
        }

        _context.InventoryMovements.AddRange(movementEntities);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<MassTransferResult>.Success(new MassTransferResult(
            movements.Count, totalMoved, target.Id, movements));
    }

    private static bool HasAnyFilter(MassLocationTransferCommand r) =>
        r.ItemId.HasValue ||
        !string.IsNullOrWhiteSpace(r.BatchNumber) ||
        !string.IsNullOrWhiteSpace(r.MRN) ||
        r.SourceWarehouseId.HasValue ||
        r.SourceLocationId.HasValue ||
        r.QualityStatus.HasValue ||
        r.LonProcessState.HasValue;
}
