using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.MoveBatchAcrossStages;

/// <summary>
/// P5.2.2 — One-click bulk move of every <see cref="InventoryBalance"/> row
/// that carries <paramref name="BatchNumber"/> to a target <see cref="LocationType"/>
/// (Production / Shipping / Quarantine / Storage …). Useful for textile-style
/// workflows where a whole receipt batch moves from storage into production
/// or from production into shipping at a single point in time.
///
/// - Multi-warehouse: balances in different warehouses each get their own
///   target location of the requested type, resolved inside that warehouse.
/// - Multi-location source: no requirement that source balances share a
///   location; each source row produces its own <see cref="MovementType.Transfer"/>
///   movement into the resolved target.
/// - LonProcessState preserved (transfer does NOT change LON business state;
///   that's what <see cref="CreateMaterialIssue"/> / waste / export do).
/// - Balance rows with <c>Quantity == 0</c> are skipped so repeated calls
///   are idempotent.
/// </summary>
public record MoveBatchAcrossStagesCommand(
    string BatchNumber,
    LocationType TargetStage,
    Guid? WarehouseId = null,
    Guid? TargetLocationId = null,
    DateTime? MovementDate = null,
    string? Reason = null) : ICommand<Result<MoveBatchResult>>;

public sealed record MoveBatchResult(
    string BatchNumber,
    int BalancesMoved,
    decimal TotalQuantityMoved,
    IReadOnlyList<MoveBatchMovement> Movements);

public sealed record MoveBatchMovement(
    Guid MovementId,
    string MovementNumber,
    Guid ItemId,
    Guid FromLocationId,
    Guid ToLocationId,
    decimal Quantity);

public class MoveBatchAcrossStagesCommandHandler
    : ICommandHandler<MoveBatchAcrossStagesCommand, Result<MoveBatchResult>>
{
    private readonly IApplicationDbContext _context;

    public MoveBatchAcrossStagesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MoveBatchResult>> Handle(
        MoveBatchAcrossStagesCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BatchNumber))
            return Result<MoveBatchResult>.Failure(ErrorCodes.BatchNotFound, "BatchNumber is required.");

        var whenUtc = request.MovementDate ?? DateTime.UtcNow;
        var batch = request.BatchNumber.Trim();

        // Pull all positive-quantity balances that carry the batch. Include
        // Location so we can group by WarehouseId for per-warehouse target
        // resolution, and for the movement audit record.
        var balances = await _context.InventoryBalances
            .Include(b => b.Location)
            .Where(b => b.BatchNumber == batch && b.Quantity > 0m)
            .ToListAsync(cancellationToken);

        if (request.WarehouseId.HasValue)
            balances = balances
                .Where(b => b.Location.WarehouseId == request.WarehouseId.Value)
                .ToList();

        if (balances.Count == 0)
            return Result<MoveBatchResult>.Failure(
                ErrorCodes.BatchNotFound,
                $"No positive-quantity inventory found for batch '{batch}'" +
                (request.WarehouseId.HasValue ? " in the supplied warehouse." : "."));

        // Resolve target locations per warehouse. One ExplicitId short-circuits
        // everything (caller picked a specific bin); otherwise pick the first
        // active location of the requested type per warehouse.
        var warehouseIds = balances.Select(b => b.Location.WarehouseId).Distinct().ToList();
        var targetByWarehouse = new Dictionary<Guid, Domain.Entities.MasterData.Location>();

        if (request.TargetLocationId.HasValue)
        {
            var loc = await _context.Locations
                .FirstOrDefaultAsync(l => l.Id == request.TargetLocationId.Value && l.IsActive, cancellationToken);
            if (loc is null)
                return Result<MoveBatchResult>.Failure(ErrorCodes.LocationNotFound, $"Target location '{request.TargetLocationId}' not found or inactive.");
            foreach (var wid in warehouseIds)
                targetByWarehouse[wid] = loc;
        }
        else
        {
            foreach (var wid in warehouseIds)
            {
                var loc = await _context.Locations
                    .FirstOrDefaultAsync(
                        l => l.WarehouseId == wid && l.Type == request.TargetStage && l.IsActive,
                        cancellationToken);
                if (loc is null)
                    return Result<MoveBatchResult>.Failure(
                        $"No active {request.TargetStage} location in warehouse '{wid}'. " +
                        "Seed one or pass an explicit TargetLocationId.");
                targetByWarehouse[wid] = loc;
            }
        }

        // Per-source, per-target: transfer the quantity and emit one movement.
        // Target balance is consolidated by natural key (Item, Location, Batch,
        // MRN, UoM, QualityStatus) — either tracked in context (just added) or
        // loaded from DB. DbSet.Local lookup first so two source rows going to
        // the same target roll up into one row within this transaction.
        var movements = new List<MoveBatchMovement>();
        var movementEntities = new List<InventoryMovement>();
        decimal totalMoved = 0m;
        int balancesMoved = 0;

        foreach (var src in balances)
        {
            var target = targetByWarehouse[src.Location.WarehouseId];
            if (src.LocationId == target.Id)
                continue;

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
                // Preserve source LON state if target has none yet.
                if (!tgt.LonProcessState.HasValue && src.LonProcessState.HasValue)
                    tgt.LonProcessState = src.LonProcessState;
            }

            var moved = src.Quantity;
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"TRF-{whenUtc:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
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

            src.Quantity = 0m;      // drained; zero-row left so audit trail shows the source existed
            totalMoved += moved;
            balancesMoved++;
            movements.Add(new MoveBatchMovement(movement.Id, movement.MovementNumber,
                src.ItemId, src.LocationId, target.Id, moved));
        }

        if (balancesMoved == 0)
            return Result<MoveBatchResult>.Failure(
                ErrorCodes.BatchNoMovementNeeded,
                "No balances needed moving — every row already sits at the target location.");

        _context.InventoryMovements.AddRange(movementEntities);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<MoveBatchResult>.Success(new MoveBatchResult(
            batch, balancesMoved, totalMoved, movements));
    }
}
