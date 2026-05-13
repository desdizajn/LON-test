using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.CommercialInvoices;

/// <summary>
/// Phase 17 §E8.5 (D4) — DTO shape returned by GET endpoints. Init-only
/// properties (memory: <c>feedback_positional_records_trap</c>) so JSON
/// binding stays robust on positional payloads.
/// </summary>
public record CommercialInvoiceDto
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public Guid? ClientOrderId { get; init; }
    public string? ClientOrderNumber { get; init; }
    public Guid? ShipmentId { get; init; }
    public string? ShipmentNumber { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
    public string? CustomsDeclarationNumber { get; init; }
    public Guid ConsigneePartnerId { get; init; }
    public string? ConsigneeName { get; init; }
    public string? ConsigneeCode { get; init; }
    public Guid ConsignorPartnerId { get; init; }
    public string? ConsignorName { get; init; }
    public string? ConsignorCode { get; init; }
    public DateTime InvoiceDate { get; init; }
    public string Currency { get; init; } = "EUR";
    public decimal Subtotal { get; init; }
    public decimal? TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string? CountryOfDestination { get; init; }
    public string Incoterms { get; init; } = string.Empty;
    public string? PaymentTerms { get; init; }
    public int Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? IssuedAt { get; init; }
    public string? IssuedBy { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelledBy { get; init; }
    public string? CancellationReason { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public List<CommercialInvoiceLineDto> Lines { get; init; } = new();
}

public record CommercialInvoiceLineDto
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ItemId { get; init; }
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public string? UoMCode { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public string? CountryOfOrigin { get; init; }
    public Guid? TariffCodeId { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Input shape for create + update + suggest endpoints. Init-only properties
/// so positional JSON binding never silently drops fields.
/// </summary>
public record CommercialInvoiceLineInput
{
    public Guid ItemId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public decimal UnitPrice { get; init; }
    public string? CountryOfOrigin { get; init; }
    public Guid? TariffCodeId { get; init; }
    public string? Notes { get; init; }
}

internal static class CommercialInvoiceMapper
{
    public static CommercialInvoiceDto Map(CommercialInvoice ci)
    {
        return new CommercialInvoiceDto
        {
            Id = ci.Id,
            Number = ci.Number,
            ClientOrderId = ci.ClientOrderId,
            ClientOrderNumber = ci.ClientOrder?.OrderNumber,
            ShipmentId = ci.ShipmentId,
            ShipmentNumber = ci.Shipment?.ShipmentNumber,
            CustomsDeclarationId = ci.CustomsDeclarationId,
            CustomsDeclarationNumber = ci.CustomsDeclaration?.DeclarationNumber,
            ConsigneePartnerId = ci.ConsigneePartnerId,
            ConsigneeName = ci.ConsigneePartner?.Name,
            ConsigneeCode = ci.ConsigneePartner?.Code,
            ConsignorPartnerId = ci.ConsignorPartnerId,
            ConsignorName = ci.ConsignorPartner?.Name,
            ConsignorCode = ci.ConsignorPartner?.Code,
            InvoiceDate = ci.InvoiceDate,
            Currency = ci.Currency,
            Subtotal = ci.Subtotal,
            TaxAmount = ci.TaxAmount,
            TotalAmount = ci.TotalAmount,
            CountryOfDestination = ci.CountryOfDestination,
            Incoterms = ci.Incoterms,
            PaymentTerms = ci.PaymentTerms,
            Status = (int)ci.Status,
            StatusName = ci.Status.ToString(),
            IssuedAt = ci.IssuedAt,
            IssuedBy = ci.IssuedBy,
            CancelledAt = ci.CancelledAt,
            CancelledBy = ci.CancelledBy,
            CancellationReason = ci.CancellationReason,
            Notes = ci.Notes,
            CreatedAt = ci.CreatedAt,
            CreatedBy = ci.CreatedBy,
            Lines = ci.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new CommercialInvoiceLineDto
                {
                    Id = l.Id,
                    LineNumber = l.LineNumber,
                    ItemId = l.ItemId,
                    ItemCode = l.Item?.Code,
                    ItemName = l.Item?.Name,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UoMId = l.UoMId,
                    UoMCode = l.UoM?.Code,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    CountryOfOrigin = l.CountryOfOrigin,
                    TariffCodeId = l.TariffCodeId,
                    Notes = l.Notes,
                }).ToList(),
        };
    }
}
