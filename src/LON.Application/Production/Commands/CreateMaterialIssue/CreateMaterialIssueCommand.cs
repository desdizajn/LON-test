using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.CreateMaterialIssue;

public record CreateMaterialIssueCommand : ICommand<Result<Guid>>
{
    public Guid ProductionOrderId { get; init; }
    public DateTime IssueDate { get; init; }
    public Guid? IssuedByEmployeeId { get; init; }
    public List<MaterialIssueLineDto> Lines { get; init; } = new();
}

public record MaterialIssueLineDto
{
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    /// <summary>Optional for non-LON material. For LON material, handler requires that
    /// batch+MRN either be provided OR resolvable via FEFO auto-pick against a single
    /// Imported balance row.</summary>
    public string? BatchNumber { get; init; }
    public string? MRN { get; init; }
    /// <summary>Optional. Narrows balance lookup. If null, handler searches any location.</summary>
    public Guid? LocationId { get; init; }
}

public class CreateMaterialIssueCommandHandler : ICommandHandler<CreateMaterialIssueCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateMaterialIssueCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateMaterialIssueCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return Result<Guid>.Failure(ErrorCodes.MaterialIssueEmptyLines, "Material issue must contain at least one line.");

        var order = await _context.ProductionOrders
            .FirstOrDefaultAsync(po => po.Id == request.ProductionOrderId, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure(ErrorCodes.MaterialIssuePoNotFound, $"ProductionOrder '{request.ProductionOrderId}' not found.");
        if (order.Status == ProductionOrderStatus.Cancelled
            || order.Status == ProductionOrderStatus.Completed
            || order.Status == ProductionOrderStatus.Closed)
            return Result<Guid>.Failure(
                ErrorCodes.PoInvalidStatus,
                $"Cannot issue material to ProductionOrder in status {order.Status}.");

        var issueNumber = $"ISS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";
        var created = new List<MaterialIssue>(request.Lines.Count);
        int lineIdx = 0;

        foreach (var line in request.Lines)
        {
            lineIdx++;
            if (line.Quantity <= 0m)
                return Result<Guid>.Failure(ErrorCodes.MaterialIssueQuantityInvalid, $"Line {lineIdx}: quantity must be positive.");

            var resolved = await ResolveBalanceAsync(line, cancellationToken);
            if (!resolved.IsSuccess)
                return resolved.ErrorCode is { } code
                    ? Result<Guid>.Failure(code, $"Line {lineIdx}: {resolved.ErrorMessage}")
                    : Result<Guid>.Failure($"Line {lineIdx}: {resolved.ErrorMessage}");

            var balance = resolved.Balance!;

            if (balance.Quantity < line.Quantity)
                return Result<Guid>.Failure(
                    ErrorCodes.MaterialIssueInsufficientInventory,
                    $"Line {lineIdx}: insufficient inventory. Demand {line.Quantity}, " +
                    $"available {balance.Quantity} on batch '{balance.BatchNumber ?? "(none)"}' MRN '{balance.MRN ?? "(none)"}'.");

            // LON-mandatory check: material under LON suspension must carry batch+MRN
            // on the IssueLine so PEE060-style reports can trace which parcel left the
            // "Imported" bucket. Enforced post-resolve so FEFO auto-pick still works —
            // the resolved balance's batch+MRN become the persisted values.
            var resolvedBatch = balance.BatchNumber;
            var resolvedMrn = balance.MRN;
            if (balance.LonProcessState == LonProcessState.Imported
                && (string.IsNullOrWhiteSpace(resolvedBatch) || string.IsNullOrWhiteSpace(resolvedMrn)))
            {
                return Result<Guid>.Failure(
                    ErrorCodes.MaterialIssueLonMissingBatchMrn,
                    $"Line {lineIdx}: LON material requires both BatchNumber and MRN on the issue line.");
            }

            // Decrement source balance. SubtractQuantity throws on over-draw (we pre-
            // checked above, so this is belt-and-suspenders).
            balance.SubtractQuantity(line.Quantity);

            // LON state split: the issued portion conceptually transitions to
            // `InProduction`. We materialise this as a separate balance row so the
            // stock ledger stays truthful — the Imported bucket shrinks, the
            // InProduction bucket grows. Mirrors legacy `LagerMaterijali` split-by-Proces.
            if (balance.LonProcessState == LonProcessState.Imported)
            {
                await UpsertInProductionBalanceAsync(balance, line.Quantity, cancellationToken);
            }

            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = request.IssueDate,
                Type = MovementType.ProductionIssue,
                ItemId = line.ItemId,
                BatchNumber = resolvedBatch,
                MRN = resolvedMrn,
                FromLocationId = balance.LocationId,
                ToLocationId = null,
                Quantity = line.Quantity,
                UoMId = line.UoMId,
                ReferenceNumber = issueNumber,
                ReferenceId = order.Id
            };
            _context.InventoryMovements.Add(movement);

            var issue = new MaterialIssue
            {
                Id = Guid.NewGuid(),
                IssueNumber = issueNumber,
                IssueDate = request.IssueDate,
                ProductionOrderId = order.Id,
                ItemId = line.ItemId,
                BatchNumber = resolvedBatch,
                MRN = resolvedMrn,
                Quantity = line.Quantity,
                UoMId = line.UoMId,
                IssuedByEmployeeId = request.IssuedByEmployeeId
            };
            _context.MaterialIssues.Add(issue);
            created.Add(issue);

            // Roll IssuedQuantity forward on the ProductionOrderMaterial that matches
            // this item (if any) so progress reports stay accurate. Missing material row
            // is tolerated — ad-hoc issues outside the BOM are legal in legacy.
            var material = await _context.ProductionOrderMaterials
                .FirstOrDefaultAsync(
                    m => m.ProductionOrderId == order.Id && m.ItemId == line.ItemId,
                    cancellationToken);
            if (material is not null)
                material.IssuedQuantity += line.Quantity;

            order.AddDomainEvent(new MaterialIssuedEvent
            {
                ProductionOrderId = order.Id,
                ItemId = line.ItemId,
                BatchNumber = resolvedBatch,
                MRN = resolvedMrn,
                Quantity = line.Quantity
            });
        }

        // Draft → InProgress on first issue so the lifecycle is visible in reports.
        if (order.Status == ProductionOrderStatus.Draft || order.Status == ProductionOrderStatus.Released)
        {
            order.Status = ProductionOrderStatus.InProgress;
            order.ActualStartDate ??= request.IssueDate;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(created[0].Id);
    }

    private sealed class BalanceResolution
    {
        public bool IsSuccess => ErrorMessage is null && Balance is not null;
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
        public InventoryBalance? Balance { get; init; }
        public static BalanceResolution Ok(InventoryBalance b) => new() { Balance = b };
        public static BalanceResolution Fail(string msg) => new() { ErrorMessage = msg };
        public static BalanceResolution Fail(string code, string msg) => new() { ErrorCode = code, ErrorMessage = msg };
    }

    private async Task<BalanceResolution> ResolveBalanceAsync(
        MaterialIssueLineDto line,
        CancellationToken ct)
    {
        var batchSpecified = !string.IsNullOrWhiteSpace(line.BatchNumber);
        var mrnSpecified = !string.IsNullOrWhiteSpace(line.MRN);

        // Exact-match path: user nailed down batch/MRN/location.
        if (batchSpecified || mrnSpecified || line.LocationId.HasValue)
        {
            // P6.21 — accept both OK and the legacy default (None = 0). Legacy
            // receipts and imports that never explicitly set qualityStatus end up
            // with 0 on disk; the stricter `== OK` filter used to hide them and
            // the resolver would report "no inventory matches …" even though the
            // balance is visible via GET /api/wms/inventory.
            var query = _context.InventoryBalances
                .Where(b => b.ItemId == line.ItemId
                            && b.UoMId == line.UoMId
                            && (b.QualityStatus == QualityStatus.OK
                                || b.QualityStatus == QualityStatus.None)
                            && b.Quantity > 0m);

            if (batchSpecified)
                query = query.Where(b => b.BatchNumber == line.BatchNumber);
            if (mrnSpecified)
                query = query.Where(b => b.MRN == line.MRN);
            if (line.LocationId.HasValue)
                query = query.Where(b => b.LocationId == line.LocationId.Value);

            var candidates = await query
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return BalanceResolution.Fail(
                    "no inventory matches the requested Item/Batch/MRN/Location/UoM combination.");

            // Prefer Imported if present (so LON material gets consumed first even on
            // exact-match queries); otherwise take the oldest.
            var imported = candidates.FirstOrDefault(b => b.LonProcessState == LonProcessState.Imported);
            return BalanceResolution.Ok(imported ?? candidates[0]);
        }

        // Auto-pick path: nothing specified. FEFO, LON-first.
        // P5.2.5 — tenants with strict audit requirements can opt out of
        // implicit material selection. When AllowFefoAutoPick is false, the
        // caller must pin at least one of Batch / MRN / Location.
        if (_context.CurrentTenantId.HasValue)
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == _context.CurrentTenantId.Value, ct);
            if (tenant is not null && !tenant.AllowFefoAutoPick)
            {
                return BalanceResolution.Fail(
                    ErrorCodes.FefoDisabled,
                    "FEFO auto-pick is disabled for this tenant. " +
                    "Supply BatchNumber, MRN, or LocationId on the issue line.");
            }
        }

        var pool = await _context.InventoryBalances
            .Where(b => b.ItemId == line.ItemId
                        && b.UoMId == line.UoMId
                        && (b.QualityStatus == QualityStatus.OK
                            || b.QualityStatus == QualityStatus.None)
                        && b.Quantity > 0m)
            .OrderByDescending(b => b.LonProcessState == LonProcessState.Imported)
            .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

        if (pool.Count == 0)
            return BalanceResolution.Fail($"no inventory available for Item '{line.ItemId}'.");

        return BalanceResolution.Ok(pool[0]);
    }

    private async Task UpsertInProductionBalanceAsync(
        InventoryBalance source,
        decimal qty,
        CancellationToken ct)
    {
        var target = await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                b.ItemId == source.ItemId
                && b.LocationId == source.LocationId
                && b.BatchNumber == source.BatchNumber
                && b.MRN == source.MRN
                && b.UoMId == source.UoMId
                && b.QualityStatus == source.QualityStatus
                && b.LonProcessState == LonProcessState.InProduction,
            ct);

        if (target is null)
        {
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
                LonProcessState = LonProcessState.InProduction
            });
        }
        else
        {
            target.AddQuantity(qty);
        }
    }
}
