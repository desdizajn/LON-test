using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.CommercialInvoices;

/// <summary>
/// Phase 17 §E8.5 (D4) — update a CommercialInvoice (Draft only).
///
/// Allowed when Status=Draft: any header field + complete line replacement.
/// Once Issued the only allowed action is Cancel (with reason). The Lines
/// list is the new full set — caller sends what it wants, we replace.
/// </summary>
public record UpdateCommercialInvoiceCommand : ICommand<Result<CommercialInvoiceDto>>
{
    public Guid Id { get; init; }
    public Guid? ClientOrderId { get; init; }
    public Guid? ShipmentId { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
    public Guid? ConsigneePartnerId { get; init; }
    public Guid? ConsignorPartnerId { get; init; }
    public DateTime? InvoiceDate { get; init; }
    public string? Currency { get; init; }
    public string? CountryOfDestination { get; init; }
    public string? Incoterms { get; init; }
    public string? PaymentTerms { get; init; }
    public decimal? TaxAmount { get; init; }
    public string? Notes { get; init; }
    /// <summary>If supplied, replaces ALL lines. If null, lines are untouched.</summary>
    public List<CommercialInvoiceLineInput>? Lines { get; init; }
}

public sealed class UpdateCommercialInvoiceCommandHandler
    : ICommandHandler<UpdateCommercialInvoiceCommand, Result<CommercialInvoiceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCommercialInvoiceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<CommercialInvoiceDto>> Handle(UpdateCommercialInvoiceCommand request, CancellationToken ct)
    {
        var ci = await _context.CommercialInvoices
            .Include(c => c.Lines)
                .ThenInclude(l => l.Item)
            .Include(c => c.Lines)
                .ThenInclude(l => l.UoM)
            .Include(c => c.ConsigneePartner)
            .Include(c => c.ConsignorPartner)
            .Include(c => c.ClientOrder)
            .Include(c => c.Shipment)
            .Include(c => c.CustomsDeclaration)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (ci is null)
            return Result<CommercialInvoiceDto>.Failure($"CommercialInvoice '{request.Id}' not found.");
        if (ci.Status != CommercialInvoiceStatus.Draft)
            return Result<CommercialInvoiceDto>.Failure(
                $"Only Draft commercial invoices can be edited; this one is {ci.Status}.");

        if (request.ClientOrderId.HasValue) ci.ClientOrderId = request.ClientOrderId.Value == Guid.Empty ? null : request.ClientOrderId;
        if (request.ShipmentId.HasValue) ci.ShipmentId = request.ShipmentId.Value == Guid.Empty ? null : request.ShipmentId;
        if (request.CustomsDeclarationId.HasValue) ci.CustomsDeclarationId = request.CustomsDeclarationId.Value == Guid.Empty ? null : request.CustomsDeclarationId;
        if (request.ConsigneePartnerId.HasValue && request.ConsigneePartnerId.Value != Guid.Empty)
            ci.ConsigneePartnerId = request.ConsigneePartnerId.Value;
        if (request.ConsignorPartnerId.HasValue && request.ConsignorPartnerId.Value != Guid.Empty)
            ci.ConsignorPartnerId = request.ConsignorPartnerId.Value;
        if (request.InvoiceDate.HasValue) ci.InvoiceDate = request.InvoiceDate.Value;
        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            if (request.Currency.Length != 3)
                return Result<CommercialInvoiceDto>.Failure("Currency must be a 3-letter ISO code.");
            ci.Currency = request.Currency.ToUpperInvariant();
        }
        if (request.CountryOfDestination is not null)
            ci.CountryOfDestination = string.IsNullOrWhiteSpace(request.CountryOfDestination)
                ? null
                : request.CountryOfDestination.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.Incoterms))
            ci.Incoterms = request.Incoterms.ToUpperInvariant();
        if (request.PaymentTerms is not null) ci.PaymentTerms = request.PaymentTerms;
        if (request.Notes is not null) ci.Notes = request.Notes;
        if (request.TaxAmount.HasValue) ci.TaxAmount = request.TaxAmount;

        if (request.Lines is not null)
        {
            if (request.Lines.Count == 0)
                return Result<CommercialInvoiceDto>.Failure("Cannot save invoice with zero lines.");

            var itemIds = request.Lines.Select(l => l.ItemId).Distinct().ToList();
            var uomIds = request.Lines.Select(l => l.UoMId).Distinct().ToList();
            var itemCount = await _context.Items.CountAsync(i => itemIds.Contains(i.Id) && !i.IsDeleted, ct);
            if (itemCount != itemIds.Count)
                return Result<CommercialInvoiceDto>.Failure("One or more Items on the lines do not exist.");
            var uomCount = await _context.UnitsOfMeasure.CountAsync(u => uomIds.Contains(u.Id) && !u.IsDeleted, ct);
            if (uomCount != uomIds.Count)
                return Result<CommercialInvoiceDto>.Failure("One or more UoMs on the lines do not exist.");

            // Remove all current lines + replace.
            foreach (var existing in ci.Lines.ToList())
            {
                _context.CommercialInvoiceLines.Remove(existing);
            }
            ci.Lines.Clear();

            int idx = 0;
            decimal subtotal = 0m;
            foreach (var l in request.Lines)
            {
                idx++;
                var lineTotal = decimal.Round(l.Quantity * l.UnitPrice, 4, MidpointRounding.AwayFromZero);
                subtotal += lineTotal;
                var newLine = new CommercialInvoiceLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = ci.TenantId,
                    CommercialInvoiceId = ci.Id,
                    LineNumber = idx,
                    ItemId = l.ItemId,
                    Description = string.IsNullOrWhiteSpace(l.Description) ? "(item)" : l.Description.Trim(),
                    Quantity = l.Quantity,
                    UoMId = l.UoMId,
                    UnitPrice = l.UnitPrice,
                    LineTotal = lineTotal,
                    CountryOfOrigin = l.CountryOfOrigin?.ToUpperInvariant(),
                    TariffCodeId = l.TariffCodeId,
                    Notes = l.Notes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUser?.AuditName ?? "System",
                };
                ci.Lines.Add(newLine);
                _context.CommercialInvoiceLines.Add(newLine);
            }
            ci.Subtotal = subtotal;
            ci.TotalAmount = subtotal + (ci.TaxAmount ?? 0m);
        }
        else if (request.TaxAmount.HasValue)
        {
            // No line change but tax adjusted → just refresh totals.
            ci.TotalAmount = ci.Subtotal + (ci.TaxAmount ?? 0m);
        }

        await _context.SaveChangesAsync(ct);
        // Re-read for fresh navigation properties on returned DTO.
        var refreshed = await _context.CommercialInvoices
            .Include(c => c.Lines)
                .ThenInclude(l => l.Item)
            .Include(c => c.Lines)
                .ThenInclude(l => l.UoM)
            .Include(c => c.ConsigneePartner)
            .Include(c => c.ConsignorPartner)
            .Include(c => c.ClientOrder)
            .Include(c => c.Shipment)
            .Include(c => c.CustomsDeclaration)
            .FirstAsync(c => c.Id == ci.Id, ct);
        return Result<CommercialInvoiceDto>.Success(CommercialInvoiceMapper.Map(refreshed));
    }
}
