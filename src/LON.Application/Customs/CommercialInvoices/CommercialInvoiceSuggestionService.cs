using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.CommercialInvoices;

/// <summary>
/// Phase 17 §E8.5 (D4) — pure-read service that drafts CI lines from a parent
/// Shipment. Called by the controller's `POST /suggest-from-shipment` endpoint
/// (hub chain action right after an EX gets created) and by the integration
/// tests directly.
/// </summary>
public interface ICommercialInvoiceSuggestionService
{
    /// <summary>
    /// Returns a draft <see cref="CommercialInvoiceDto"/> populated from
    /// <paramref name="shipmentId"/>: header fields (currency from declaration,
    /// consignee = shipment.CustomerId), one suggested line per ShipmentLine
    /// (quantity, UoM, item description). Does NOT persist anything.
    /// Failure cases: shipment missing → 404-style failure; shipment has no
    /// lines → empty Lines list (caller can still display the header form).
    /// </summary>
    Task<Result<CommercialInvoiceDto>> SuggestFromShipment(Guid shipmentId, CancellationToken ct);
}

public sealed class CommercialInvoiceSuggestionService : ICommercialInvoiceSuggestionService
{
    private readonly IApplicationDbContext _context;

    public CommercialInvoiceSuggestionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CommercialInvoiceDto>> SuggestFromShipment(Guid shipmentId, CancellationToken ct)
    {
        var shipment = await _context.Shipments
            .Include(s => s.Lines)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        if (shipment is null)
            return Result<CommercialInvoiceDto>.Failure($"Shipment '{shipmentId}' not found.");

        // Try to find a chained EX customs declaration via ClientOrder.
        Guid? declId = null;
        string? declNumber = null;
        string currency = "EUR";
        string? countryDest = null;
        string? incoterms = null;
        if (shipment.ClientOrderId.HasValue)
        {
            var ex = await _context.CustomsDeclarations
                .Where(d => d.ClientOrderId == shipment.ClientOrderId.Value
                            && d.DeclarationType == "EX"
                            && !d.IsDeleted)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (ex != null)
            {
                declId = ex.Id;
                declNumber = ex.DeclarationNumber;
                currency = string.IsNullOrWhiteSpace(ex.Currency) ? "EUR" : ex.Currency;
                countryDest = ex.CountryOfDestination;
                incoterms = ex.DeliveryTerms;
            }
        }

        var itemIds = shipment.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await _context.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var lines = shipment.Lines
            .OrderBy(l => l.LineNumber)
            .Select((l, idx) =>
            {
                items.TryGetValue(l.ItemId, out var item);
                return new CommercialInvoiceLineDto
                {
                    Id = Guid.Empty,
                    LineNumber = idx + 1,
                    ItemId = l.ItemId,
                    ItemCode = item?.Code,
                    ItemName = item?.Name,
                    Description = item is null
                        ? "(item)"
                        : (string.IsNullOrEmpty(item.Name) ? item.Code : $"{item.Code} — {item.Name}"),
                    Quantity = l.Quantity,
                    UoMId = l.UoMId,
                    UnitPrice = 0m,
                    LineTotal = 0m,
                    CountryOfOrigin = "MK",
                    TariffCodeId = null,
                };
            }).ToList();

        var dto = new CommercialInvoiceDto
        {
            Id = Guid.Empty,
            Number = string.Empty,
            ClientOrderId = shipment.ClientOrderId,
            ShipmentId = shipment.Id,
            ShipmentNumber = shipment.ShipmentNumber,
            CustomsDeclarationId = declId,
            CustomsDeclarationNumber = declNumber,
            ConsigneePartnerId = shipment.CustomerId ?? Guid.Empty,
            ConsigneeName = shipment.Customer?.Name,
            ConsigneeCode = shipment.Customer?.Code,
            ConsignorPartnerId = Guid.Empty, // user picks
            InvoiceDate = DateTime.UtcNow.Date,
            Currency = currency,
            CountryOfDestination = countryDest,
            Incoterms = incoterms ?? "FOB",
            Status = 1,
            StatusName = "Draft",
            Subtotal = 0m,
            TotalAmount = 0m,
            Lines = lines,
        };

        return Result<CommercialInvoiceDto>.Success(dto);
    }
}
