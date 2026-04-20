using LON.Application.Common.Interfaces;
using LON.Application.WMS.Commands.CreateReceipt;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

/// <summary>
/// Collapses every ResolvedImportRow into ONE <see cref="Receipt"/> whose
/// lines come from the rows. Header-level fields (receiptDate, warehouseId,
/// partnerId, purchaseOrderNumber, referenceNumber) must be present; the
/// resolver already merged defaults into every row so we just read them
/// off the first row.
///
/// Inline creation rather than delegating to <see cref="CreateReceiptCommand"/>
/// because the command path does MRN lookups + inflate-for-waste that
/// aren't appropriate for bulk import (the importer assumes MRN/Qty are
/// already correct per the uploaded grid). If the user wants MRN-aware
/// receipts, they can still use the regular Receipts screen.
/// </summary>
public class ReceiptsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "Receipts";

    public async Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return (true, 0, null);
        var head = rows[0];
        var warehouseId = head.GetOrDefault<Guid>("warehouseCode");
        if (warehouseId == Guid.Empty)
            return (false, 0, "warehouseCode (header) is required.");
        var receiptDate = head.GetOrDefault<DateTime>("receiptDate");
        if (receiptDate == default)
            return (false, 0, "receiptDate (header) is required.");
        var partnerId = head.GetOrDefault<Guid?>("partnerCode");

        // Pick a landing location for lines that don't carry one — the
        // same fallback logic the regular CreateReceiptCommand uses.
        Guid? fallbackLocationId = head.GetOrDefault<Guid?>("locationCode");
        if (!fallbackLocationId.HasValue)
        {
            fallbackLocationId = await context.Locations
                .Where(l => l.WarehouseId == warehouseId && l.Type == LocationType.Receiving && l.IsActive)
                .Select(l => (Guid?)l.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!fallbackLocationId.HasValue)
                fallbackLocationId = await context.Locations
                    .Where(l => l.WarehouseId == warehouseId && l.IsActive)
                    .Select(l => (Guid?)l.Id)
                    .FirstOrDefaultAsync(cancellationToken);
        }
        if (!fallbackLocationId.HasValue)
            return (false, 0, $"No active location found in warehouse for imported receipt.");

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = $"IMP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            ReceiptDate = receiptDate,
            PartnerId = partnerId,
            WarehouseId = warehouseId,
            PurchaseOrderNumber = head.GetOrDefault<string>("purchaseOrderNumber"),
            ReferenceNumber = head.GetOrDefault<string>("referenceNumber")
        };
        await context.Receipts.AddAsync(receipt, cancellationToken);

        int lineNumber = 1;
        foreach (var row in rows)
        {
            var itemId = row.GetOrDefault<Guid>("itemCode");
            var uomId = row.GetOrDefault<Guid>("uomCode");
            var qty = row.GetOrDefault<decimal>("quantity");
            if (itemId == Guid.Empty || uomId == Guid.Empty || qty == 0m)
                return (false, lineNumber - 1, $"Row {row.RowIndex}: itemCode, uomCode and quantity are required.");

            var lineLocationId = row.GetOrDefault<Guid?>("locationCode") ?? fallbackLocationId.Value;
            var qualityName = row.GetOrDefault<string>("qualityStatus");
            QualityStatus quality;
            if (string.IsNullOrWhiteSpace(qualityName)
                || !Enum.TryParse<QualityStatus>(qualityName, true, out quality)
                || quality == QualityStatus.None)
            {
                // P6.21 — blank/unknown/None collapses to OK so downstream filters
                // that match `== QualityStatus.OK` find the balance we just book.
                quality = QualityStatus.OK;
            }

            receipt.Lines.Add(new ReceiptLine
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                LineNumber = lineNumber++,
                ItemId = itemId,
                Quantity = qty,
                UoMId = uomId,
                BatchNumber = row.GetOrDefault<string>("batchNumber"),
                MRN = row.GetOrDefault<string>("mrn"),
                LocationId = lineLocationId,
                QualityStatus = quality,
                ExpiryDate = row.GetOrDefault<DateTime?>("expiryDate"),
                CustomsDeclarationId = row.GetOrDefault<Guid?>("customsDeclarationNumber")
            });

            context.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(),
                MovementNumber = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = receiptDate,
                Type = MovementType.Receipt,
                ItemId = itemId,
                BatchNumber = row.GetOrDefault<string>("batchNumber"),
                MRN = row.GetOrDefault<string>("mrn"),
                FromLocationId = null,
                ToLocationId = lineLocationId,
                Quantity = qty,
                UoMId = uomId,
                ReferenceNumber = receipt.ReceiptNumber,
                ReferenceId = receipt.Id
            });

            var balance = await context.InventoryBalances.FirstOrDefaultAsync(b =>
                b.ItemId == itemId
                && b.LocationId == lineLocationId
                && b.BatchNumber == row.GetOrDefault<string>("batchNumber")
                && b.MRN == row.GetOrDefault<string>("mrn")
                && b.UoMId == uomId
                && b.QualityStatus == quality, cancellationToken);
            if (balance is null)
            {
                await context.InventoryBalances.AddAsync(new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    ItemId = itemId,
                    LocationId = lineLocationId,
                    BatchNumber = row.GetOrDefault<string>("batchNumber"),
                    MRN = row.GetOrDefault<string>("mrn"),
                    Quantity = qty,
                    UoMId = uomId,
                    QualityStatus = quality,
                    ExpiryDate = row.GetOrDefault<DateTime?>("expiryDate")
                }, cancellationToken);
            }
            else
            {
                balance.AddQuantity(qty);
            }
        }

        receipt.AddDomainEvent(new ReceiptCreatedEvent
        {
            ReceiptId = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate
        });
        return (true, 1, null);
    }
}
