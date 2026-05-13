using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.PodelbaToProducer;

/// <summary>
/// Phase 17 §E6 — assign N selected <see cref="InventoryBalance"/> rows to a
/// single sub-contractor producer in one atomic call. Dual of
/// <see cref="Podelba.PodelbaCommand"/>: that command splits ONE balance across
/// many producers (full distribution required); this one routes MANY balances
/// to ONE producer with partial quantities allowed.
///
/// Per-line semantics mirror legacy ELON <c>frmPodeliBaranjaBrz</c> (one row per
/// (closure, producer, size)) — except the source row keeps its remainder
/// instead of draining to zero. Physical stock stays at the same location; the
/// split is logical, stamping <see cref="InventoryBalance.AssignedProducerId"/>
/// on the sibling row.
///
/// <para>Rules:</para>
/// <list type="bullet">
///   <item>At least one line; every quantity &gt; 0.</item>
///   <item>Producer must exist as a <see cref="Partner"/> of
///         <see cref="PartnerType.Producer"/>.</item>
///   <item>Each source balance must exist, be non-deleted, qty &gt; 0,
///         line.Quantity ≤ source.Quantity.</item>
///   <item>Idempotent: a sibling balance with identical natural key (item,
///         location, batch, MRN, UoM, QualityStatus, LonProcessState,
///         producer) is incremented instead of duplicated.</item>
///   <item>One <see cref="InventoryMovement"/> of type Transfer per line
///         referencing <c>Podelba:{producerId}</c> and, optionally, the
///         ClientOrderId for audit traceback.</item>
/// </list>
/// </summary>
public sealed record PodelbaToProducerCommand : ICommand<Result<PodelbaToProducerResult>>
{
    public Guid ProducerId { get; init; }
    public Guid? ClientOrderId { get; init; }
    public string? Reason { get; init; }
    public List<PodelbaToProducerLine> Lines { get; init; } = new();
}

public sealed record PodelbaToProducerLine
{
    public Guid SourceBalanceId { get; init; }
    public decimal Quantity { get; init; }
}

public sealed record PodelbaToProducerResult(
    int LinesAssigned,
    decimal TotalQuantityAssigned,
    Guid ProducerId,
    string PodelbaNumber);

public sealed class PodelbaToProducerCommandHandler
    : ICommandHandler<PodelbaToProducerCommand, Result<PodelbaToProducerResult>>
{
    private readonly IApplicationDbContext _context;

    public PodelbaToProducerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PodelbaToProducerResult>> Handle(
        PodelbaToProducerCommand request, CancellationToken cancellationToken)
    {
        if (request.ProducerId == Guid.Empty)
            return Result<PodelbaToProducerResult>.Failure("ProducerId is required.");
        if (request.Lines is null || request.Lines.Count == 0)
            return Result<PodelbaToProducerResult>.Failure("At least one line is required.");
        if (request.Lines.Any(l => l.Quantity <= 0m))
            return Result<PodelbaToProducerResult>.Failure("Every line quantity must be positive.");
        if (request.Lines.Any(l => l.SourceBalanceId == Guid.Empty))
            return Result<PodelbaToProducerResult>.Failure("Every line must reference a source balance id.");

        var producer = await _context.Partners
            .FirstOrDefaultAsync(p => p.Id == request.ProducerId && !p.IsDeleted, cancellationToken);
        if (producer is null)
            return Result<PodelbaToProducerResult>.Failure(
                $"Producer '{request.ProducerId}' not found or not accessible under current tenant.");
        if (producer.Type != PartnerType.Producer)
            return Result<PodelbaToProducerResult>.Failure(
                $"Partner {producer.Code} is type {producer.Type}, not Producer. Refusing podelba.");
        if (!producer.IsActive)
            return Result<PodelbaToProducerResult>.Failure(
                $"Producer {producer.Code} is inactive.");

        var sourceIds = request.Lines.Select(l => l.SourceBalanceId).Distinct().ToList();
        if (sourceIds.Count != request.Lines.Count)
            return Result<PodelbaToProducerResult>.Failure(
                "Duplicate source balance in lines. Combine quantities for the same balance.");

        var sources = await _context.InventoryBalances
            .Where(b => sourceIds.Contains(b.Id) && !b.IsDeleted)
            .ToListAsync(cancellationToken);
        if (sources.Count != sourceIds.Count)
            return Result<PodelbaToProducerResult>.Failure(
                "One or more source balances not found or not accessible under current tenant.");

        foreach (var line in request.Lines)
        {
            var src = sources.First(s => s.Id == line.SourceBalanceId);
            if (src.Quantity < line.Quantity)
                return Result<PodelbaToProducerResult>.Failure(
                    $"Source balance {src.Id} has {src.Quantity} available; cannot allocate {line.Quantity}.");
        }

        var podelbaNumber = $"PDL-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6]}";
        var whenUtc = DateTime.UtcNow;
        decimal totalAssigned = 0m;

        foreach (var line in request.Lines)
        {
            var src = sources.First(s => s.Id == line.SourceBalanceId);

            // Match an existing sibling assigned to the target producer with the same natural key.
            var sibling = _context.InventoryBalances.Local.FirstOrDefault(b =>
                !b.IsDeleted
                && b.ItemId == src.ItemId
                && b.LocationId == src.LocationId
                && b.BatchNumber == src.BatchNumber
                && b.MRN == src.MRN
                && b.UoMId == src.UoMId
                && b.QualityStatus == src.QualityStatus
                && b.LonProcessState == src.LonProcessState
                && b.AssignedProducerId == request.ProducerId);

            sibling ??= await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                    !b.IsDeleted
                    && b.ItemId == src.ItemId
                    && b.LocationId == src.LocationId
                    && b.BatchNumber == src.BatchNumber
                    && b.MRN == src.MRN
                    && b.UoMId == src.UoMId
                    && b.QualityStatus == src.QualityStatus
                    && b.LonProcessState == src.LonProcessState
                    && b.AssignedProducerId == request.ProducerId,
                cancellationToken);

            if (sibling is null)
            {
                sibling = new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    ItemId = src.ItemId,
                    LocationId = src.LocationId,
                    UoMId = src.UoMId,
                    BatchNumber = src.BatchNumber,
                    MRN = src.MRN,
                    Quantity = 0m,
                    QualityStatus = src.QualityStatus,
                    ExpiryDate = src.ExpiryDate,
                    LonProcessState = src.LonProcessState,
                    AssignedProducerId = request.ProducerId,
                };
                await _context.InventoryBalances.AddAsync(sibling, cancellationToken);
            }

            sibling.Quantity += line.Quantity;
            src.Quantity -= line.Quantity;
            totalAssigned += line.Quantity;

            var notes = request.ClientOrderId.HasValue
                ? $"Podelba to producer {request.ProducerId}; ClientOrderId={request.ClientOrderId}"
                : $"Podelba to producer {request.ProducerId}";
            if (!string.IsNullOrWhiteSpace(request.Reason))
                notes = $"{notes}; reason={request.Reason}";

            await _context.InventoryMovements.AddAsync(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = podelbaNumber,
                MovementDate = whenUtc,
                Type = MovementType.Transfer,
                ItemId = src.ItemId,
                BatchNumber = src.BatchNumber,
                MRN = src.MRN,
                FromLocationId = src.LocationId,
                ToLocationId = src.LocationId,
                Quantity = line.Quantity,
                UoMId = src.UoMId,
                ReferenceNumber = $"Podelba:{request.ProducerId}",
                Notes = notes,
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<PodelbaToProducerResult>.Success(new PodelbaToProducerResult(
            LinesAssigned: request.Lines.Count,
            TotalQuantityAssigned: totalAssigned,
            ProducerId: request.ProducerId,
            PodelbaNumber: podelbaNumber));
    }
}
