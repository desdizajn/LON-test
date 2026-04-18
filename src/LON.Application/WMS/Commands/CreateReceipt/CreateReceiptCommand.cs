using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
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
    public QualityStatus QualityStatus { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
}

public class CreateReceiptCommandHandler : ICommandHandler<CreateReceiptCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateReceiptCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateReceiptCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return Result<Guid>.Failure("Receipt must contain at least one line.");

        // TenantId on Receipt / ReceiptLine / InventoryMovement / InventoryBalance
        // is auto-populated by ApplicationDbContext.SaveChangesAsync via
        // ICurrentTenantService (JWT claim / user lookup / first active tenant).

        var fallbackLocationId = await ResolveLandingLocationAsync(request, cancellationToken);
        // fallback may be null if no locations at all in the warehouse; individual lines may still
        // succeed if they specify their own LocationId.

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

            var line = new ReceiptLine
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                BatchNumber = lineDto.BatchNumber,
                MRN = lineDto.MRN,
                LocationId = lineLocationId,
                QualityStatus = lineDto.QualityStatus,
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
                Quantity = lineDto.Quantity,
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
                    b.QualityStatus == lineDto.QualityStatus,
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
                    Quantity = lineDto.Quantity,
                    UoMId = lineDto.UoMId,
                    QualityStatus = lineDto.QualityStatus,
                    ExpiryDate = lineDto.ExpiryDate
                });
            }
            else
            {
                balance.AddQuantity(lineDto.Quantity);
            }
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

    /// <summary>
    /// Resolve the landing location for this receipt.
    /// Priority: explicit LocationId > Receiving-type in warehouse > code-prefix "RCV" > first active location.
    /// </summary>
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
}
