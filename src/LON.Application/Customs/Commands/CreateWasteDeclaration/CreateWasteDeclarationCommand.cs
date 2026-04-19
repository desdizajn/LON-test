using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Commands.CreateWasteDeclaration;

/// <summary>
/// P2.6c — Books physical residual LON-state inventory off as waste. Transitions
/// Imported/InProduction balances to <see cref="LonProcessState.Waste"/>. No
/// guarantee-ledger impact in v1: bond tracking is against declared quantity,
/// while inflate-for-waste residual (e.g. 5% TEKSPORT uplift) is physical-only.
/// Over-inflate waste (compliance edge case per MK Правилник 377) is deferred
/// and requires a separate FinalImport re-classification.
/// </summary>
public record CreateWasteDeclarationCommand : ICommand<Result<Guid>>
{
    public DateTime WasteDate { get; init; }
    /// <summary>Source IM MRN whose physical residual is being written off.</summary>
    public string MRN { get; init; } = string.Empty;
    /// <summary>Physical units to transition to Waste. Must &gt; 0.</summary>
    public decimal Quantity { get; init; }
    /// <summary>Free-text explanation (required; populates InventoryMovement.Notes + audit log).</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Optional Item filter when the MRN spans multiple items.</summary>
    public Guid? ItemId { get; init; }
    /// <summary>Optional Batch filter.</summary>
    public string? BatchNumber { get; init; }
    /// <summary>Optional Location filter.</summary>
    public Guid? LocationId { get; init; }

    /// <summary>
    /// P4.6 — Decomposition of the total waste into up to 4 named slots + zaguba.
    /// Legacy ELON stored up to 4 waste percentages (<c>ArtOtpadProc</c>,
    /// <c>ArtOtpadProc1</c>, <c>ArtOtpadProc2</c>) and one <c>ArtOtpadZaguba</c>
    /// zero-recovery slot on <c>tblArtikli</c>. Here we let the caller itemise
    /// the physical write-off so the audit trail and PEE report can cite each
    /// category separately.
    ///
    /// Rules when Slots is non-empty:
    ///   - Sum of slot quantities must equal <see cref="Quantity"/>.
    ///   - SlotIndex: 1..4 normal waste; 0 reserved for Zaguba (unrecoverable).
    ///   - Each slot produces a separate InventoryMovement carrying the slot
    ///     label in its <c>Notes</c> so reports can group.
    /// When Slots is empty, behaviour is unchanged (single-slot waste).
    /// </summary>
    public List<WasteSlot>? Slots { get; init; }
}

public sealed record WasteSlot
{
    /// <summary>0 = Zaguba (loss); 1..4 = normal waste buckets.</summary>
    public int SlotIndex { get; init; }
    public decimal Quantity { get; init; }
    /// <summary>Optional short label shown alongside the movement.</summary>
    public string? Category { get; init; }
}

public class CreateWasteDeclarationCommandHandler
    : ICommandHandler<CreateWasteDeclarationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateWasteDeclarationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        CreateWasteDeclarationCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0m)
            return Result<Guid>.Failure("Quantity must be positive.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<Guid>.Failure("Reason is required for a waste declaration (audit trail).");
        if (string.IsNullOrWhiteSpace(request.MRN))
            return Result<Guid>.Failure("MRN is required.");

        if (request.Slots is { Count: > 0 })
        {
            if (request.Slots.Count > 5)
                return Result<Guid>.Failure("Максимум 5 waste слотови (4 normalни + Zaguba).");
            if (request.Slots.Any(s => s.SlotIndex is < 0 or > 4))
                return Result<Guid>.Failure("SlotIndex мора да биде во 0..4 (0 = Zaguba).");
            if (request.Slots.Any(s => s.Quantity <= 0m))
                return Result<Guid>.Failure("Секој waste слот мора да има позитивно количество.");
            if (request.Slots.GroupBy(s => s.SlotIndex).Any(g => g.Count() > 1))
                return Result<Guid>.Failure("Не може ист SlotIndex да се појавува два пати.");
            var slotSum = request.Slots.Sum(s => s.Quantity);
            if (Math.Abs(slotSum - request.Quantity) > 0.0001m)
                return Result<Guid>.Failure(
                    $"Сума на слотови ({slotSum}) мора да е еднаква со вкупното количество ({request.Quantity}).");
        }

        var mrn = request.MRN.Trim().ToUpperInvariant();

        var registry = await _context.MRNRegistries.FirstOrDefaultAsync(
            r => r.MRN == mrn && !r.IsDeleted, cancellationToken);
        if (registry is null)
            return Result<Guid>.Failure($"MRN '{mrn}' is not registered for this tenant.");

        var query = _context.InventoryBalances
            .Where(b => b.MRN == mrn
                        && b.Quantity > 0m
                        && (b.LonProcessState == LonProcessState.Imported
                            || b.LonProcessState == LonProcessState.InProduction));
        if (request.ItemId.HasValue)
            query = query.Where(b => b.ItemId == request.ItemId.Value);
        if (!string.IsNullOrWhiteSpace(request.BatchNumber))
            query = query.Where(b => b.BatchNumber == request.BatchNumber);
        if (request.LocationId.HasValue)
            query = query.Where(b => b.LocationId == request.LocationId.Value);

        // Imported-first (residual typically sits there after production drains
        // WIP); InProduction picked up only if caller specifically targets
        // still-unprocessed stock.
        var pool = await query
            .OrderBy(b => b.LonProcessState == LonProcessState.Imported ? 0 : 1)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var poolTotal = pool.Sum(b => b.Quantity);
        if (poolTotal < request.Quantity)
        {
            return Result<Guid>.Failure(
                $"Insufficient LON inventory for MRN '{mrn}' under the applied filters. " +
                $"Demand {request.Quantity}, available {poolTotal}.");
        }

        var movementNumber = $"WST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";
        var movements = new List<InventoryMovement>();

        // One stream per requested waste slot. When Slots is null we treat the
        // total as a single-slot with SlotIndex=1 (legacy behaviour).
        var slotList = request.Slots is { Count: > 0 }
            ? request.Slots
            : new List<WasteSlot> { new() { SlotIndex = 1, Quantity = request.Quantity } };

        foreach (var slot in slotList)
        {
            decimal remaining = slot.Quantity;
            string slotLabel = slot.SlotIndex == 0 ? "Zaguba" : $"Otpad{slot.SlotIndex}";
            if (!string.IsNullOrWhiteSpace(slot.Category)) slotLabel = $"{slotLabel} ({slot.Category})";

            foreach (var src in pool)
            {
                if (remaining <= 0m) break;
                if (src.Quantity <= 0m) continue;
                var take = Math.Min(src.Quantity, remaining);
                src.SubtractQuantity(take);

                UpsertWasteBalance(src, take);

                movements.Add(new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    MovementNumber = slot.SlotIndex == 0
                        ? $"{movementNumber}/Z"
                        : $"{movementNumber}/W{slot.SlotIndex}",
                    MovementDate = request.WasteDate,
                    Type = MovementType.Adjustment,
                    ItemId = src.ItemId,
                    BatchNumber = src.BatchNumber,
                    MRN = src.MRN,
                    FromLocationId = src.LocationId,
                    ToLocationId = null,
                    Quantity = take,
                    UoMId = src.UoMId,
                    ReferenceNumber = movementNumber,
                    ReferenceId = null,
                    Notes = $"{slotLabel}: {request.Reason.Trim()}"
                });
                remaining -= take;
            }

            if (remaining > 0m)
            {
                // Should not happen after poolTotal check, but guard defensively.
                return Result<Guid>.Failure(
                    $"Internal: failed to drain {remaining} units for slot {slot.SlotIndex}.");
            }
        }
        foreach (var mv in movements) _context.InventoryMovements.Add(mv);

        // Domain event (reuses the existing InventoryMovedEvent so downstream
        // handlers see waste transitions just like any other movement).
        pool.First().AddDomainEvent(new InventoryMovedEvent
        {
            ItemId = pool.First().ItemId,
            BatchNumber = pool.First().BatchNumber,
            MRN = pool.First().MRN,
            FromLocationId = pool.First().LocationId,
            ToLocationId = null,
            Quantity = request.Quantity,
            MovementType = "Waste"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(movements.First().Id);
    }

    private void UpsertWasteBalance(InventoryBalance source, decimal qty)
    {
        // Same DbSet.Local-first probe used in the EX handler: a single waste
        // command that drains multiple source rows must merge into one Waste
        // sibling, not accumulate duplicates within one SaveChanges cycle.
        var tracked = _context.InventoryBalances.Local.FirstOrDefault(b =>
            b.ItemId == source.ItemId
            && b.LocationId == source.LocationId
            && b.BatchNumber == source.BatchNumber
            && b.MRN == source.MRN
            && b.UoMId == source.UoMId
            && b.QualityStatus == source.QualityStatus
            && b.LonProcessState == LonProcessState.Waste);
        if (tracked is not null)
        {
            tracked.AddQuantity(qty);
            return;
        }

        _context.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(),
            ItemId = source.ItemId,
            LocationId = source.LocationId,
            BatchNumber = source.BatchNumber,
            MRN = source.MRN,
            Quantity = qty,
            UoMId = source.UoMId,
            QualityStatus = source.QualityStatus,
            ExpiryDate = source.ExpiryDate,
            LonProcessState = LonProcessState.Waste
        });
    }
}
