using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Customs;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.CreateReceipt;

public record CreateReceiptCommand : ICommand<Result<Guid>>
{
    public DateTime ReceiptDate { get; init; }
    public Guid? PartnerId { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid? LocationId { get; init; }
    public string? PurchaseOrderNumber { get; init; }
    public string? ReferenceNumber { get; init; }
    public List<ReceiptLineDto> Lines { get; init; } = new();
}

public record ReceiptLineDto
{
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public string? BatchNumber { get; init; }
    public string? MRN { get; init; }
    /// <summary>Per-line location. Falls back to CreateReceiptCommand.LocationId, then auto-resolve.</summary>
    public Guid? LocationId { get; init; }
    public QualityStatus QualityStatus { get; init; } = QualityStatus.OK;
    public DateTime? ExpiryDate { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
}

public class CreateReceiptCommandHandler : ICommandHandler<CreateReceiptCommand, Result<Guid>>
{
    /// <summary>SAD Box 37 procedure codes whose material enters the LON state chain.</summary>
    private static readonly HashSet<string> LonProcedureCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "4200", "5100"
    };

    private readonly IApplicationDbContext _context;

    public CreateReceiptCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateReceiptCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return Result<Guid>.Failure("Receipt must contain at least one line.");

        var fallbackLocationId = await ResolveLandingLocationAsync(request, cancellationToken);

        // P2.3 — pre-resolve everything we need for per-line decisions in ONE
        // batch so we don't N+1 the DB while looping. Fetching up-front also
        // lets us fail-fast BEFORE any entity is added to the context.
        var mrnContext = await LoadMrnContextAsync(request, cancellationToken);
        if (!mrnContext.IsSuccess)
            return Result<Guid>.Failure(mrnContext.ErrorMessage!);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            ReceiptDate = request.ReceiptDate,
            PartnerId = request.PartnerId,
            WarehouseId = request.WarehouseId,
            PurchaseOrderNumber = request.PurchaseOrderNumber,
            ReferenceNumber = request.ReferenceNumber
        };

        int lineNumber = 1;
        foreach (var lineDto in request.Lines)
        {
            var lineLocationId = lineDto.LocationId ?? fallbackLocationId;
            if (lineLocationId is null)
                return Result<Guid>.Failure(
                    $"Line {lineNumber}: no location resolved. " +
                    "Specify LocationId on the line, on the receipt, or configure a Receiving location in the warehouse.");

            // P6.21 — coerce unset (enum default 0 = None) to OK so filters that
            // match `== QualityStatus.OK` find the resulting balance. Without this,
            // callers that omit qualityStatus in the JSON payload end up storing 0
            // and MaterialIssue / Export / Waste queries skip the row.
            var lineQuality = lineDto.QualityStatus == QualityStatus.None
                ? QualityStatus.OK
                : lineDto.QualityStatus;

            // I1 — inflate-for-waste. Applied per line, only for TEKSPORT-style
            // tenants AND only when the line references an MRN whose
            // LONAuthorizationItem carries a waste percentage > 0. The
            // ReceiptLine.Quantity stays at the DECLARED value (what customs
            // ticked) so bond-tracking math lines up; InventoryBalance /
            // InventoryMovement record the INFLATED physical count (legacy
            // `LagerMaterijali.Kol = KolMat × 100/(100-otpad%)`).
            var bookedQuantity = mrnContext.ComputeInflatedQty(lineDto);

            // LON state transitions: only parcels that truly belong to a LON
            // suspension procedure (4200 / 5100) start as `Imported`. Non-LON
            // MRNs (e.g. FINAL procedure) leave LonProcessState null so the
            // state machine doesn't advertise a stage that doesn't apply.
            LonProcessState? lineLonState = null;
            if (!string.IsNullOrWhiteSpace(lineDto.MRN)
                && mrnContext.IsLonMrn(lineDto.MRN!))
            {
                lineLonState = LonProcessState.Imported;
            }

            var line = new ReceiptLine
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                Quantity = lineDto.Quantity, // declared, unchanged
                UoMId = lineDto.UoMId,
                BatchNumber = lineDto.BatchNumber,
                MRN = lineDto.MRN,
                LocationId = lineLocationId,
                QualityStatus = lineQuality,
                ExpiryDate = lineDto.ExpiryDate,
                CustomsDeclarationId = lineDto.CustomsDeclarationId
            };
            receipt.Lines.Add(line);

            _context.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = request.ReceiptDate,
                Type = MovementType.Receipt,
                ItemId = lineDto.ItemId,
                BatchNumber = lineDto.BatchNumber,
                MRN = lineDto.MRN,
                FromLocationId = null,
                ToLocationId = lineLocationId.Value,
                Quantity = bookedQuantity, // inflated when the tenant opts in
                UoMId = lineDto.UoMId,
                ReferenceNumber = receipt.ReceiptNumber,
                ReferenceId = receipt.Id
            });

            var balance = await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                    b.ItemId == lineDto.ItemId &&
                    b.LocationId == lineLocationId.Value &&
                    b.BatchNumber == lineDto.BatchNumber &&
                    b.MRN == lineDto.MRN &&
                    b.UoMId == lineDto.UoMId &&
                    b.QualityStatus == lineQuality,
                cancellationToken);

            if (balance is null)
            {
                _context.InventoryBalances.Add(new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    ItemId = lineDto.ItemId,
                    LocationId = lineLocationId.Value,
                    BatchNumber = lineDto.BatchNumber,
                    MRN = lineDto.MRN,
                    Quantity = bookedQuantity,
                    UoMId = lineDto.UoMId,
                    QualityStatus = lineQuality,
                    ExpiryDate = lineDto.ExpiryDate,
                    LonProcessState = lineLonState
                });
            }
            else
            {
                balance.AddQuantity(bookedQuantity);
                if (!balance.LonProcessState.HasValue && lineLonState.HasValue)
                    balance.LonProcessState = lineLonState;
            }

            // Atomic MRN consumption. Declared qty (not inflated) is what
            // counts against customs bond. Flip IsActive=false when fully
            // consumed so subsequent receipts stop at the pre-validate step
            // (RemainingQuantity check).
            if (!string.IsNullOrWhiteSpace(lineDto.MRN))
                mrnContext.ConsumeFromRegistry(lineDto.MRN!, lineDto.Quantity);
        }

        receipt.AddDomainEvent(new ReceiptCreatedEvent
        {
            ReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate
        });

        await _context.Receipts.AddAsync(receipt, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(receipt.Id);
    }

    private async Task<Guid?> ResolveLandingLocationAsync(CreateReceiptCommand request, CancellationToken ct)
    {
        if (request.LocationId.HasValue)
        {
            var explicitLoc = await _context.Locations.FirstOrDefaultAsync(
                l => l.Id == request.LocationId.Value && l.WarehouseId == request.WarehouseId, ct);
            return explicitLoc?.Id;
        }

        var byType = await _context.Locations.FirstOrDefaultAsync(
            l => l.WarehouseId == request.WarehouseId && l.Type == LocationType.Receiving && l.IsActive, ct);
        if (byType != null) return byType.Id;

        var byCode = await _context.Locations.FirstOrDefaultAsync(
            l => l.WarehouseId == request.WarehouseId && l.Code.StartsWith("RCV") && l.IsActive, ct);
        if (byCode != null) return byCode.Id;

        var firstActive = await _context.Locations.FirstOrDefaultAsync(
            l => l.WarehouseId == request.WarehouseId && l.IsActive, ct);
        return firstActive?.Id;
    }

    // ================================================================
    // P2.3 — MRN context: registry lookup, aggregate validation, waste%
    // resolution, and mutator that flips UsedQuantity atomically in the
    // same SaveChanges as the receipt itself.
    // ================================================================
    private sealed class MrnContext
    {
        public bool IsSuccess => ErrorMessage is null;
        public string? ErrorMessage { get; private init; }

        /// <summary>MRN → MRNRegistry row (tenant-scoped via global query filter).</summary>
        private Dictionary<string, MRNRegistry> Registries { get; init; } = new();

        /// <summary>MRN → the declaration's LONAuthorizationId (if any).</summary>
        private Dictionary<string, (Guid? AuthId, string ProcedureCode)> Declarations { get; init; } = new();

        /// <summary>(LONAuthorizationId, ItemId) → waste%; only populated when inflate-for-waste is ON.</summary>
        private Dictionary<(Guid, Guid), decimal> WasteLookup { get; init; } = new();

        private bool InflateEnabled { get; init; }

        public static MrnContext Failure(string error) => new() { ErrorMessage = error };

        public static MrnContext Ok(
            Dictionary<string, MRNRegistry> registries,
            Dictionary<string, (Guid? AuthId, string ProcedureCode)> declarations,
            Dictionary<(Guid, Guid), decimal> wasteLookup,
            bool inflateEnabled) => new()
        {
            Registries = registries,
            Declarations = declarations,
            WasteLookup = wasteLookup,
            InflateEnabled = inflateEnabled
        };

        public bool IsLonMrn(string mrn)
            => Declarations.TryGetValue(mrn, out var d)
               && LonProcedureCodes.Contains(d.ProcedureCode);

        public decimal ComputeInflatedQty(ReceiptLineDto line)
        {
            if (!InflateEnabled || string.IsNullOrWhiteSpace(line.MRN)) return line.Quantity;
            if (!Declarations.TryGetValue(line.MRN, out var decl) || !decl.AuthId.HasValue) return line.Quantity;
            if (!WasteLookup.TryGetValue((decl.AuthId.Value, line.ItemId), out var wastePct)) return line.Quantity;
            if (wastePct <= 0m || wastePct >= 100m) return line.Quantity;
            return Math.Round(line.Quantity * 100m / (100m - wastePct), 4, MidpointRounding.AwayFromZero);
        }

        public void ConsumeFromRegistry(string mrn, decimal declaredQty)
        {
            if (!Registries.TryGetValue(mrn, out var reg)) return;
            reg.UsedQuantity += declaredQty;
            if (reg.UsedQuantity >= reg.TotalQuantity)
                reg.IsActive = false;
        }
    }

    private async Task<MrnContext> LoadMrnContextAsync(CreateReceiptCommand request, CancellationToken ct)
    {
        var distinctMrns = request.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.MRN))
            .Select(l => l.MRN!.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        // No MRN lines at all → no-op context (backwards compat path).
        if (distinctMrns.Count == 0)
            return MrnContext.Ok(new(), new(), new(), inflateEnabled: false);

        var registries = await _context.MRNRegistries
            .Where(r => distinctMrns.Contains(r.MRN) && !r.IsDeleted)
            .ToDictionaryAsync(r => r.MRN, ct);

        foreach (var mrn in distinctMrns)
        {
            if (!registries.TryGetValue(mrn, out var reg))
                return MrnContext.Failure($"MRN '{mrn}' is not registered for this tenant. File the customs declaration first.");
            if (!reg.IsActive)
                return MrnContext.Failure($"MRN '{mrn}' is deactivated (already fully consumed or closed).");
            if (reg.ExpiryDate.HasValue && reg.ExpiryDate.Value.Date < request.ReceiptDate.Date)
                return MrnContext.Failure($"MRN '{mrn}' expired on {reg.ExpiryDate:yyyy-MM-dd}; receipt date is {request.ReceiptDate:yyyy-MM-dd}.");

            // Aggregate over-draw check. If multiple lines of the same receipt
            // consume the same MRN, their sum must still fit in the remaining
            // quantity on the registry row.
            var demand = request.Lines
                .Where(l => string.Equals(l.MRN, mrn, StringComparison.OrdinalIgnoreCase))
                .Sum(l => l.Quantity);

            var remaining = reg.TotalQuantity - reg.UsedQuantity;
            if (demand > remaining)
                return MrnContext.Failure(
                    $"MRN '{mrn}' overdraw: requested {demand}, remaining {remaining} of {reg.TotalQuantity}. " +
                    "Split the receipt or register an additional declaration.");
        }

        // Resolve the declarations behind each MRN (for LON state + waste lookup).
        var declIds = registries.Values
            .Where(r => r.CustomsDeclarationId.HasValue)
            .Select(r => r.CustomsDeclarationId!.Value)
            .ToList();
        var declarations = await _context.CustomsDeclarations
            .Where(d => declIds.Contains(d.Id))
            .Select(d => new { d.Id, d.LONAuthorizationId, d.ProcedureCode })
            .ToDictionaryAsync(d => d.Id, ct);

        var declByMrn = new Dictionary<string, (Guid? AuthId, string ProcedureCode)>();
        foreach (var kv in registries)
        {
            if (kv.Value.CustomsDeclarationId.HasValue
                && declarations.TryGetValue(kv.Value.CustomsDeclarationId.Value, out var d))
            {
                declByMrn[kv.Key] = (d.LONAuthorizationId, d.ProcedureCode ?? string.Empty);
            }
            else
            {
                declByMrn[kv.Key] = (null, string.Empty);
            }
        }

        // Tenant inflate flag. ICurrentUserService.TenantId is null during
        // seeders / bg jobs — then inflate defaults to OFF. Fetch once to
        // avoid N+1 per line.
        bool inflateEnabled = false;
        Dictionary<(Guid, Guid), decimal> wasteLookup = new();

        var tenantId = registries.Values.FirstOrDefault()?.TenantId;
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, ct);
            inflateEnabled = tenant?.InflateImportForWaste ?? false;

            if (inflateEnabled)
            {
                var authIds = declByMrn.Values
                    .Where(v => v.AuthId.HasValue)
                    .Select(v => v.AuthId!.Value)
                    .Distinct()
                    .ToList();
                var itemIds = request.Lines.Select(l => l.ItemId).Distinct().ToList();
                if (authIds.Count > 0 && itemIds.Count > 0)
                {
                    var items = await _context.LONAuthorizationItems
                        .Where(ai => authIds.Contains(ai.LONAuthorizationId)
                                     && itemIds.Contains(ai.ImportItemId)
                                     && !ai.IsDeleted)
                        .Select(ai => new { ai.LONAuthorizationId, ai.ImportItemId, ai.AllowedWastePercentage })
                        .ToListAsync(ct);
                    foreach (var ai in items)
                        wasteLookup[(ai.LONAuthorizationId, ai.ImportItemId)] = ai.AllowedWastePercentage;
                }
            }
        }

        return MrnContext.Ok(registries, declByMrn, wasteLookup, inflateEnabled);
    }
}
