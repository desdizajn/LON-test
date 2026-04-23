using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.ReportSkart;

/// <summary>
/// P15.3 — report defective-on-intake quantity against a ReceiptLine.
/// Transfers <see cref="SkartQuantity"/> from the OK
/// <see cref="InventoryBalance"/> at the receipt location into a Blocked
/// sibling at the SAME location (same item + batch + MRN + UoM). Emits
/// an <see cref="InventoryMovement"/> of type Adjustment referencing the
/// new <see cref="Skart"/> record for full audit trail.
///
/// <para>Semantics (legacy <c>FakturiU5Skart.cmdZapisi_Click</c>):</para>
/// <list type="bullet">
///   <item>NetoKol = ReceiptLine.Quantity - Σ previous-skart-on-this-line.</item>
///   <item>SkartKol must be ≤ NetoKol. 0 is rejected (use Cancel instead).</item>
///   <item>The OK balance keyed by (item, batch, MRN, location, UoM, QualityStatus=OK)
///         is decremented; a sibling at QualityStatus=Blocked is created or
///         incremented. Both balances remain at the original location because
///         the dock quarantine is a status, not a physical move.</item>
///   <item>LonProcessState is preserved on both rows — skart does NOT release
///         the guarantee bond (supplier claim handles that separately under
///         the chosen <see cref="SkartResolution"/>).</item>
/// </list>
/// </summary>
public record ReportSkartCommand : ICommand<Result<Guid>>
{
    public Guid ReceiptLineId { get; init; }
    public decimal SkartQuantity { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class ReportSkartCommandHandler : ICommandHandler<ReportSkartCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public ReportSkartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(ReportSkartCommand request, CancellationToken ct)
    {
        if (request.SkartQuantity <= 0m)
            return Result<Guid>.Failure("SkartQuantity must be positive.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<Guid>.Failure("Reason is required for a Skart report (supplier-claim audit trail).");

        var line = await _context.ReceiptLines
            .Include(l => l.Receipt)
            .FirstOrDefaultAsync(l => l.Id == request.ReceiptLineId, ct);
        if (line is null)
            return Result<Guid>.Failure($"ReceiptLine '{request.ReceiptLineId}' not found.");
        if (line.LocationId is null)
            return Result<Guid>.Failure(
                $"ReceiptLine '{line.Id}' has no location assigned; resolve intake first.");

        // NetoKol guard — cumulative skart on this line cannot exceed the
        // received quantity. Legacy: NetoKol_Exit rejects overdraw.
        var alreadySkarted = await _context.Skarts
            .Where(s => s.ReceiptLineId == line.Id && !s.IsDeleted)
            .SumAsync(s => s.SkartQuantity, ct);
        var remaining = line.Quantity - alreadySkarted;
        if (request.SkartQuantity > remaining)
            return Result<Guid>.Failure(
                $"Skart qty {request.SkartQuantity} exceeds remaining {remaining} " +
                $"on line (received {line.Quantity}, already skart-ed {alreadySkarted}).");

        // Find the OK balance that this receipt populated. Match by the
        // receipt's natural key: item + batch + MRN + location + UoM +
        // QualityStatus=OK. LonProcessState is preserved (null or Imported).
        var okBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(b => b.ItemId == line.ItemId
                                       && b.LocationId == line.LocationId.Value
                                       && b.UoMId == line.UoMId
                                       && b.BatchNumber == line.BatchNumber
                                       && b.MRN == line.MRN
                                       && b.QualityStatus == QualityStatus.OK
                                       && !b.IsDeleted, ct);
        if (okBalance is null)
            return Result<Guid>.Failure(
                "No OK inventory balance matches the receipt line; has the qty already been issued or moved?");
        if (okBalance.Quantity < request.SkartQuantity)
            return Result<Guid>.Failure(
                $"OK balance at location has only {okBalance.Quantity}; cannot skart {request.SkartQuantity}.");

        // Blocked sibling at the SAME location (same LonProcessState; skart is
        // not a re-classification of the bond, only of the quality).
        var blockedBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(b => b.ItemId == line.ItemId
                                       && b.LocationId == line.LocationId.Value
                                       && b.UoMId == line.UoMId
                                       && b.BatchNumber == line.BatchNumber
                                       && b.MRN == line.MRN
                                       && b.QualityStatus == QualityStatus.Blocked
                                       && b.LonProcessState == okBalance.LonProcessState
                                       && !b.IsDeleted, ct);

        if (blockedBalance is null)
        {
            blockedBalance = new InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = line.ItemId,
                LocationId = line.LocationId.Value,
                UoMId = line.UoMId,
                BatchNumber = line.BatchNumber,
                MRN = line.MRN,
                Quantity = 0m,
                QualityStatus = QualityStatus.Blocked,
                ExpiryDate = okBalance.ExpiryDate,
                LonProcessState = okBalance.LonProcessState
            };
            await _context.InventoryBalances.AddAsync(blockedBalance, ct);
        }

        okBalance.Quantity -= request.SkartQuantity;
        blockedBalance.Quantity += request.SkartQuantity;

        var skartNumber = await GenerateSkartNumberAsync(ct);
        var skart = new Skart
        {
            Id = Guid.NewGuid(),
            SkartNumber = skartNumber,
            ReportedAt = DateTime.UtcNow,
            ReceiptLineId = line.Id,
            ItemId = line.ItemId,
            BatchNumber = line.BatchNumber,
            MRN = line.MRN,
            SkartQuantity = request.SkartQuantity,
            UoMId = line.UoMId,
            Reason = request.Reason.Trim(),
            Resolution = SkartResolution.Open
        };
        await _context.Skarts.AddAsync(skart, ct);

        // Audit movement — same location, status flip from OK → Blocked.
        await _context.InventoryMovements.AddAsync(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = skartNumber, // reuse so the audit line is findable by Skart#
            MovementDate = skart.ReportedAt,
            Type = MovementType.Adjustment,
            ItemId = line.ItemId,
            BatchNumber = line.BatchNumber,
            MRN = line.MRN,
            FromLocationId = line.LocationId,
            ToLocationId = line.LocationId,
            Quantity = request.SkartQuantity,
            UoMId = line.UoMId,
            ReferenceNumber = $"Skart:{skartNumber}",
            ReferenceId = skart.Id,
            Notes = $"Skart reported: {request.Reason.Trim()}"
        }, ct);

        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(skart.Id);
    }

    /// <summary>
    /// <c>SKT-yyyyMMdd-NNNN</c> — sequential per tenant per day. Matches the
    /// <c>SKT-</c> naming the receipt/movement audit trail references.
    /// </summary>
    private async Task<string> GenerateSkartNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"SKT-{today:yyyyMMdd}-";
        var todaysCount = await _context.Skarts
            .Where(s => s.ReportedAt >= today && s.ReportedAt < today.AddDays(1))
            .CountAsync(ct);
        return $"{prefix}{(todaysCount + 1):D4}";
    }
}

/// <summary>
/// P15.3 — close out a skart with the settled outcome. Does not change
/// inventory; the Blocked balance stays until an explicit transfer/shipment
/// disposes of it (supplier return = Shipment, destruction = adjustment-out,
/// accepted-at-discount = QC release back to OK).
/// </summary>
public record ResolveSkartCommand : ICommand<Result<bool>>
{
    public Guid SkartId { get; init; }
    public SkartResolution Resolution { get; init; }
    public string? ResolutionNote { get; init; }
}

public class ResolveSkartCommandHandler : ICommandHandler<ResolveSkartCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public ResolveSkartCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(ResolveSkartCommand request, CancellationToken ct)
    {
        if (request.Resolution == SkartResolution.Open)
            return Result<bool>.Failure("Resolution must be a terminal state (ReturnedToSupplier / Destroyed / AcceptedAtDiscount).");

        var skart = await _context.Skarts.FirstOrDefaultAsync(s => s.Id == request.SkartId, ct);
        if (skart is null)
            return Result<bool>.Failure($"Skart '{request.SkartId}' not found.");
        if (skart.Resolution != SkartResolution.Open)
            return Result<bool>.Failure(
                $"Skart '{skart.SkartNumber}' already resolved as {skart.Resolution} on {skart.ResolvedAt:yyyy-MM-dd}.");

        skart.Resolution = request.Resolution;
        skart.ResolvedAt = DateTime.UtcNow;
        skart.ResolutionNote = string.IsNullOrWhiteSpace(request.ResolutionNote)
            ? null
            : request.ResolutionNote.Trim();

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
