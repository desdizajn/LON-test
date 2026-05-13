using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Customs;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E5 — adds a <see cref="ClientOrderFinishedGood"/> row to an
/// existing ClientOrder (used by the hub's „Внеси готови производи (BOM)"
/// action).
///
/// Pre-conditions:
///   - ClientOrder exists and is not Closed / Cancelled.
///   - Item exists; UoM exists.
///   - Optional BOM (when supplied) must reference the same item.
/// </summary>
public record AddClientOrderFinishedGoodCommand : ICommand<Result<Guid>>
{
    public Guid ClientOrderId { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public Guid UoMId { get; init; }
    public Guid? BOMId { get; init; }
    public decimal? UnitPriceForeign { get; init; }
    public string Currency { get; init; } = "EUR";
    public string? Notes { get; init; }
}

public class AddClientOrderFinishedGoodCommandHandler
    : ICommandHandler<AddClientOrderFinishedGoodCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenant;

    public AddClientOrderFinishedGoodCommandHandler(
        IApplicationDbContext context,
        ICurrentTenantService currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    public async Task<Result<Guid>> Handle(AddClientOrderFinishedGoodCommand request, CancellationToken cancellationToken)
    {
        if (request.ClientOrderId == Guid.Empty)
            return Result<Guid>.Failure("ClientOrderId is required.");
        if (request.ItemId == Guid.Empty)
            return Result<Guid>.Failure("ItemId is required.");
        if (request.UoMId == Guid.Empty)
            return Result<Guid>.Failure("UoMId is required.");
        if (request.Quantity <= 0m)
            return Result<Guid>.Failure("Quantity must be > 0.");

        var order = await _context.ClientOrders
            .FirstOrDefaultAsync(o => o.Id == request.ClientOrderId, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure($"ClientOrder '{request.ClientOrderId}' does not exist.");
        if (order.Status is ClientOrderStatus.Closed or ClientOrderStatus.Cancelled)
            return Result<Guid>.Failure(
                $"ClientOrder '{order.OrderNumber}' is {order.Status} and cannot be modified.");

        if (request.BOMId.HasValue)
        {
            var bomOk = await _context.BOMs
                .AnyAsync(b => b.Id == request.BOMId.Value && b.ItemId == request.ItemId, cancellationToken);
            if (!bomOk)
                return Result<Guid>.Failure(
                    $"BOM '{request.BOMId.Value}' does not exist or is not for item '{request.ItemId}'.");
        }

        var tenantId = await _currentTenant.GetTenantIdAsync(cancellationToken);
        if (tenantId is null || tenantId.Value == Guid.Empty)
            return Result<Guid>.Failure("Tenant context not resolved.");

        var fg = new ClientOrderFinishedGood
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ClientOrderId = order.Id,
            ItemId = request.ItemId,
            Quantity = request.Quantity,
            UoMId = request.UoMId,
            BOMId = request.BOMId,
            UnitPriceForeign = request.UnitPriceForeign,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.Trim().ToUpperInvariant(),
            Notes = request.Notes,
        };

        _context.ClientOrderFinishedGoods.Add(fg);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(fg.Id);
    }
}
