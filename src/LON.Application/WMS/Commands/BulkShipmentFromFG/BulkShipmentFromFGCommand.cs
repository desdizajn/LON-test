using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Customs.Commands.CreateExportDeclaration;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.BulkShipmentFromFG;

/// <summary>
/// P5.2.4 — Bulk Shipment from Finished-Goods selection.
///
/// Replaces the legacy "tick multiple FG rows → generate shipment + EX in
/// sequence" workflow. Inputs are a filter predicate (Item/Batch/MRN/PO/
/// location/warehouse/partner). The handler:
///
/// 1. Resolves matching positive-quantity InventoryBalances (FEFO via
///    CreatedAt ascending).
/// 2. Emits one <see cref="Shipment"/> with one <see cref="ShipmentLine"/> per
///    matched balance (no consolidation — traceability per physical balance
///    beats a smaller line count).
/// 3. Drains each source balance, writes an <see cref="InventoryMovement"/>
///    with Type=Shipment.
/// 4. When <c>CreateExportDeclaration=true</c> and every line's MRN matches
///    the caller-supplied <c>Mrn</c> filter (or there is a single distinct
///    MRN), chains to <see cref="CreateExportDeclarationCommand"/> so the
///    customs side of the workflow lands in the same atomic commit.
/// </summary>
public sealed record BulkShipmentFromFGCommand(
    Guid? ItemId,
    string? BatchNumber,
    string? Mrn,
    Guid? LocationId,
    Guid? SourceWarehouseId,
    Guid? ProductionOrderId,
    Guid? PartnerId,
    Guid? CustomsProcedureId,
    string? DeclarationNumber,
    DateTime? ShipmentDate,
    string? Reference,
    bool CreateExportDeclaration = false,
    /// <summary>Phase 17 §E8 — when supplied, stamps Shipment + chained EX
    /// declaration with the parent ClientOrder so the hub Shipments tab +
    /// Razdolzuvanje aggregations have a clean join path.</summary>
    Guid? ClientOrderId = null) : ICommand<Result<BulkShipmentFromFGResult>>;

public sealed record BulkShipmentFromFGResult(
    Guid ShipmentId,
    string ShipmentNumber,
    int LinesCreated,
    decimal TotalQuantity,
    Guid? ExportDeclarationId);

public sealed class BulkShipmentFromFGHandler
    : ICommandHandler<BulkShipmentFromFGCommand, Result<BulkShipmentFromFGResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _mediator;
    private readonly INumberSequenceService _sequence;
    private readonly ICurrentTenantService _tenantService;

    public BulkShipmentFromFGHandler(
        IApplicationDbContext context,
        ISender mediator,
        INumberSequenceService sequence,
        ICurrentTenantService tenantService)
    {
        _context = context;
        _mediator = mediator;
        _sequence = sequence;
        _tenantService = tenantService;
    }

    public async Task<Result<BulkShipmentFromFGResult>> Handle(
        BulkShipmentFromFGCommand request, CancellationToken cancellationToken)
    {
        if (!HasFilter(request))
            return Result<BulkShipmentFromFGResult>.Failure(
                ErrorCodes.TransferNoFilter,
                "At least one filter is required (itemId, batchNumber, mrn, locationId, sourceWarehouseId, productionOrderId).");

        // Build the FG pool query. Only positive quantities move; self-selected
        // LonProcessState==null or ==Imported both qualify (FG lands null after
        // ProductionReceipt, but partners may later ship imported stock direct).
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
        if (!string.IsNullOrWhiteSpace(request.Mrn))
        {
            var mrn = request.Mrn.Trim().ToUpperInvariant();
            query = query.Where(b => b.MRN == mrn);
        }
        if (request.LocationId.HasValue)
            query = query.Where(b => b.LocationId == request.LocationId.Value);
        if (request.SourceWarehouseId.HasValue)
            query = query.Where(b => b.Location.WarehouseId == request.SourceWarehouseId.Value);

        // ProductionOrderId filter: walk ProductionReceipts → batches to know
        // which FG balances belong to the PO. Skipped unless supplied.
        if (request.ProductionOrderId.HasValue)
        {
            var poBatches = await _context.ProductionReceipts
                .Where(pr => pr.ProductionOrderId == request.ProductionOrderId.Value
                             && !pr.IsDeleted
                             && pr.BatchNumber != null)
                .Select(pr => pr.BatchNumber!)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (poBatches.Count == 0)
                return Result<BulkShipmentFromFGResult>.Failure(
                    ErrorCodes.TransferNoMatch,
                    "Production order has no finished-goods receipts yet.");
            query = query.Where(b => b.BatchNumber != null && poBatches.Contains(b.BatchNumber));
        }

        var balances = await query.OrderBy(b => b.CreatedAt).ToListAsync(cancellationToken);
        if (balances.Count == 0)
            return Result<BulkShipmentFromFGResult>.Failure(
                ErrorCodes.TransferNoMatch,
                "No positive-quantity finished-goods inventory matched the filter.");

        var shipmentDate = request.ShipmentDate ?? DateTime.UtcNow;
        // Phase 17 §E12 — per-tenant SQL SEQUENCE for monotonic shipment numbers.
        var tenantId = await _tenantService.GetTenantIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("No active tenant context for Shipment numbering.");
        var seq = await _sequence.NextAsync("Shipment", tenantId, cancellationToken);
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            ShipmentNumber = LON.Domain.Common.NumberFormatter.Shipment(shipmentDate.Year, seq),
            ShipmentDate = shipmentDate,
            CustomerId = request.PartnerId,
            Status = ShipmentStatus.Draft,
            TrackingNumber = null,
            SalesOrderNumber = request.Reference,
            // Phase 17 §E8 — link to parent ClientOrder so hub Shipments tab
            // can filter via a single SQL IN-clause.
            ClientOrderId = request.ClientOrderId,
        };
        _context.Shipments.Add(shipment);

        // Phase 17 §E11 — emit ShipmentCreatedEvent so DomainEventLog
        // captures the create alongside the audit interceptor.
        shipment.AddDomainEvent(new LON.Domain.Events.ShipmentCreatedEvent
        {
            ShipmentId = shipment.Id,
            ShipmentNumber = shipment.ShipmentNumber,
            ShipmentDate = shipment.ShipmentDate,
        });

        int lineIdx = 0;
        decimal totalQty = 0m;
        foreach (var b in balances)
        {
            lineIdx++;
            var line = new ShipmentLine
            {
                Id = Guid.NewGuid(),
                ShipmentId = shipment.Id,
                LineNumber = lineIdx,
                ItemId = b.ItemId,
                BatchNumber = b.BatchNumber,
                MRN = b.MRN,
                Quantity = b.Quantity,
                UoMId = b.UoMId,
                CustomsDeclarationId = null,
            };
            shipment.Lines.Add(line);

            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"SHP-{shipmentDate:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = shipmentDate,
                Type = MovementType.Shipment,
                ItemId = b.ItemId,
                BatchNumber = b.BatchNumber,
                MRN = b.MRN,
                FromLocationId = b.LocationId,
                ToLocationId = null,
                Quantity = b.Quantity,
                UoMId = b.UoMId,
                ReferenceNumber = shipment.ShipmentNumber,
                ReferenceId = shipment.Id,
                Notes = request.Reference,
            };
            _context.InventoryMovements.Add(movement);

            totalQty += b.Quantity;
            b.Quantity = 0m; // drained; zero-row left for audit parity
        }

        Guid? exportDeclarationId = null;
        if (request.CreateExportDeclaration)
        {
            // Need a single distinct MRN across all ship lines so the
            // downstream Export handler can do its pro-rata credit math.
            var distinctMrns = balances
                .Select(b => b.MRN)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToList();
            if (distinctMrns.Count != 1)
                return Result<BulkShipmentFromFGResult>.Failure(
                    ErrorCodes.ExportMrnRequired,
                    "Bulk shipment covers " +
                    (distinctMrns.Count == 0 ? "0" : distinctMrns.Count.ToString()) +
                    " distinct MRNs; chained export declaration requires exactly 1.");
            if (!request.CustomsProcedureId.HasValue)
                return Result<BulkShipmentFromFGResult>.Failure(
                    ErrorCodes.ProcedureNotFound,
                    "CustomsProcedureId is required when CreateExportDeclaration=true.");

            var exportCmd = new CreateExportDeclarationCommand
            {
                DeclarationNumber = request.DeclarationNumber
                    ?? $"EX-{shipmentDate:yyyyMMdd}-{shipment.ShipmentNumber[^8..]}",
                MRN = string.Empty, // handler mints one when empty
                DeclarationDate = shipmentDate,
                CustomsProcedureId = request.CustomsProcedureId.Value,
                PartnerId = request.PartnerId,
                Currency = "EUR",
                TotalCustomsValue = 0m,
                // Phase 17 §E8 — propagate ClientOrder linkage so the hub
                // Declarations tab sees both IM and EX without extra joins.
                ClientOrderId = request.ClientOrderId,
                Lines = shipment.Lines.Select(sl => new ExportLineDto
                {
                    ItemId = sl.ItemId,
                    Quantity = sl.Quantity,
                    UoMId = sl.UoMId,
                    BatchNumber = sl.BatchNumber ?? string.Empty,
                    LocationId = null,
                    SourceMRN = sl.MRN ?? string.Empty,
                    DischargeQuantity = sl.Quantity,
                }).ToList(),
            };

            var exportResult = await _mediator.Send(exportCmd, cancellationToken);
            if (!exportResult.IsSuccess)
                return exportResult.ErrorCode is { } code
                    ? Result<BulkShipmentFromFGResult>.Failure(code, exportResult.ErrorMessage ?? "export failed")
                    : Result<BulkShipmentFromFGResult>.Failure(exportResult.ErrorMessage ?? "export failed");
            exportDeclarationId = exportResult.Data;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<BulkShipmentFromFGResult>.Success(new BulkShipmentFromFGResult(
            shipment.Id,
            shipment.ShipmentNumber,
            shipment.Lines.Count,
            totalQty,
            exportDeclarationId));
    }

    private static bool HasFilter(BulkShipmentFromFGCommand r) =>
        r.ItemId.HasValue
        || !string.IsNullOrWhiteSpace(r.BatchNumber)
        || !string.IsNullOrWhiteSpace(r.Mrn)
        || r.LocationId.HasValue
        || r.SourceWarehouseId.HasValue
        || r.ProductionOrderId.HasValue;
}
