using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Common;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.CommercialInvoices;

/// <summary>
/// Phase 17 §E8.5 (D4) — create a new CommercialInvoice in Draft status.
///
/// Pre-conditions:
///   - <see cref="ConsigneePartnerId"/> + <see cref="ConsignorPartnerId"/> resolve to active Partners.
///   - <see cref="Lines"/> not empty; every line resolves Item + UoM.
///
/// Number: stamped via INumberSequenceService + NumberFormatter
/// (CI-{year}-{seq:D6}). Status starts at Draft.
///
/// Totals are computed server-side from line quantity × unit price (and
/// optional tax). Caller's <see cref="Subtotal"/> / <see cref="TotalAmount"/>
/// are accepted as hints but the handler recomputes to keep arithmetic
/// authoritative — a fat-fingered client cannot ship inconsistent totals.
/// </summary>
public record CreateCommercialInvoiceCommand : ICommand<Result<Guid>>
{
    public Guid? ClientOrderId { get; init; }
    public Guid? ShipmentId { get; init; }
    public Guid? CustomsDeclarationId { get; init; }
    public Guid ConsigneePartnerId { get; init; }
    public Guid ConsignorPartnerId { get; init; }
    public DateTime? InvoiceDate { get; init; }
    public string Currency { get; init; } = "EUR";
    public string? CountryOfDestination { get; init; }
    public string Incoterms { get; init; } = "FOB";
    public string? PaymentTerms { get; init; }
    public decimal? TaxAmount { get; init; }
    public string? Notes { get; init; }
    public List<CommercialInvoiceLineInput> Lines { get; init; } = new();
}

public sealed class CreateCommercialInvoiceCommandHandler
    : ICommandHandler<CreateCommercialInvoiceCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly INumberSequenceService _sequence;

    public CreateCommercialInvoiceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ICurrentTenantService currentTenant,
        INumberSequenceService sequence)
    {
        _context = context;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _sequence = sequence;
    }

    public async Task<Result<Guid>> Handle(CreateCommercialInvoiceCommand request, CancellationToken ct)
    {
        if (request.ConsigneePartnerId == Guid.Empty)
            return Result<Guid>.Failure("ConsigneePartnerId is required.");
        if (request.ConsignorPartnerId == Guid.Empty)
            return Result<Guid>.Failure("ConsignorPartnerId is required.");
        if (request.Lines is null || request.Lines.Count == 0)
            return Result<Guid>.Failure("At least one line is required.");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
            return Result<Guid>.Failure("Currency must be a 3-letter ISO code.");
        if (string.IsNullOrWhiteSpace(request.Incoterms))
            return Result<Guid>.Failure("Incoterms are required (FOB/EXW/CIF/...).");

        var resolvedTenant = await _currentTenant.GetTenantIdAsync(ct);
        if (resolvedTenant is null || resolvedTenant.Value == Guid.Empty)
            return Result<Guid>.Failure("Tenant context not resolved.");
        var tenantId = resolvedTenant.Value;

        var consigneeOk = await _context.Partners
            .AnyAsync(p => p.Id == request.ConsigneePartnerId && !p.IsDeleted, ct);
        if (!consigneeOk)
            return Result<Guid>.Failure($"Consignee partner '{request.ConsigneePartnerId}' does not exist.");

        var consignorOk = await _context.Partners
            .AnyAsync(p => p.Id == request.ConsignorPartnerId && !p.IsDeleted, ct);
        if (!consignorOk)
            return Result<Guid>.Failure($"Consignor partner '{request.ConsignorPartnerId}' does not exist.");

        if (request.ClientOrderId.HasValue && request.ClientOrderId.Value != Guid.Empty)
        {
            var ok = await _context.ClientOrders
                .AnyAsync(c => c.Id == request.ClientOrderId.Value && !c.IsDeleted, ct);
            if (!ok)
                return Result<Guid>.Failure($"ClientOrder '{request.ClientOrderId}' does not exist.");
        }

        if (request.ShipmentId.HasValue && request.ShipmentId.Value != Guid.Empty)
        {
            var ok = await _context.Shipments
                .AnyAsync(s => s.Id == request.ShipmentId.Value && !s.IsDeleted, ct);
            if (!ok)
                return Result<Guid>.Failure($"Shipment '{request.ShipmentId}' does not exist.");
        }

        if (request.CustomsDeclarationId.HasValue && request.CustomsDeclarationId.Value != Guid.Empty)
        {
            var ok = await _context.CustomsDeclarations
                .AnyAsync(d => d.Id == request.CustomsDeclarationId.Value && !d.IsDeleted, ct);
            if (!ok)
                return Result<Guid>.Failure($"CustomsDeclaration '{request.CustomsDeclarationId}' does not exist.");
        }

        // Validate referenced items + UoMs exist (single round-trip each).
        var itemIds = request.Lines.Select(l => l.ItemId).Distinct().ToList();
        var uomIds = request.Lines.Select(l => l.UoMId).Distinct().ToList();
        var itemCount = await _context.Items.CountAsync(i => itemIds.Contains(i.Id) && !i.IsDeleted, ct);
        if (itemCount != itemIds.Count)
            return Result<Guid>.Failure("One or more Items on the lines do not exist.");
        var uomCount = await _context.UnitsOfMeasure.CountAsync(u => uomIds.Contains(u.Id) && !u.IsDeleted, ct);
        if (uomCount != uomIds.Count)
            return Result<Guid>.Failure("One or more UoMs on the lines do not exist.");

        var invoiceDate = request.InvoiceDate ?? DateTime.UtcNow.Date;
        var seq = await _sequence.NextAsync("CommercialInvoice", tenantId, ct);
        var number = NumberFormatter.CommercialInvoice(invoiceDate.Year, seq);

        var ci = new CommercialInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Number = number,
            ClientOrderId = request.ClientOrderId,
            ShipmentId = request.ShipmentId,
            CustomsDeclarationId = request.CustomsDeclarationId,
            ConsigneePartnerId = request.ConsigneePartnerId,
            ConsignorPartnerId = request.ConsignorPartnerId,
            InvoiceDate = invoiceDate,
            Currency = request.Currency.ToUpperInvariant(),
            CountryOfDestination = request.CountryOfDestination?.ToUpperInvariant(),
            Incoterms = request.Incoterms.ToUpperInvariant(),
            PaymentTerms = request.PaymentTerms,
            Status = CommercialInvoiceStatus.Draft,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser?.AuditName ?? "System",
        };

        int idx = 0;
        decimal subtotal = 0m;
        foreach (var l in request.Lines)
        {
            idx++;
            var lineTotal = decimal.Round(l.Quantity * l.UnitPrice, 4, MidpointRounding.AwayFromZero);
            subtotal += lineTotal;
            ci.Lines.Add(new CommercialInvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
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
            });
        }

        ci.Subtotal = subtotal;
        ci.TaxAmount = request.TaxAmount;
        ci.TotalAmount = subtotal + (request.TaxAmount ?? 0m);

        _context.CommercialInvoices.Add(ci);
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(ci.Id);
    }
}
