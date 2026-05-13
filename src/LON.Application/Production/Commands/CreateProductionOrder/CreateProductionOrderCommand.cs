using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.CreateProductionOrder;

public record CreateProductionOrderCommand : ICommand<Result<Guid>>
{
    public Guid ItemId { get; init; }
    public decimal OrderQuantity { get; init; }
    public Guid UoMId { get; init; }
    public DateTime PlannedStartDate { get; init; }
    public DateTime PlannedEndDate { get; init; }
    public Guid? BOMId { get; init; }
    public Guid? RoutingId { get; init; }
    public string? SalesOrderReference { get; init; }
    public string? Notes { get; init; }

    /// <summary>
    /// P5.3.2 — optional Partner (customer) for whom this order is produced.
    /// When set, BOM auto-apply prefers partner-scoped BOMs (<see cref="BOM.PartnerId"/>)
    /// before falling back to the global template.
    /// </summary>
    public Guid? PartnerId { get; init; }

    /// <summary>
    /// Phase 17 §E5 — parent ClientOrder. When set, the new ProductionOrder
    /// becomes visible on the ClientOrder hub's Production tab, and a Draft
    /// ClientOrder transitions to <c>Producing</c> when its first PO is created.
    /// </summary>
    public Guid? ClientOrderId { get; init; }
}

public class CreateProductionOrderCommandHandler : ICommandHandler<CreateProductionOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateProductionOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateProductionOrderCommand request, CancellationToken cancellationToken)
    {
        // P5.3.1 BOMTemplate auto-apply: when the caller didn't pin a specific
        // BOM / Routing, resolve the default template for the Item. Picks the
        // latest-Version ACTIVE template that's currently valid (ValidFrom ≤ now
        // and (ValidTo is null or ValidTo > now)). Repeat products become
        // zero-keystroke: pass ItemId + qty, everything else is inferred.
        var resolvedBomId = request.BOMId;
        var resolvedRoutingId = request.RoutingId;
        var now = DateTime.UtcNow;

        if (!resolvedBomId.HasValue)
        {
            // P5.3.2 — prefer partner-scoped BOM when the caller supplied one,
            // fall back to the global (PartnerId == null) template. Both are
            // ordered by Version desc so the latest variant wins within each
            // scope.
            BOM? bom = null;
            if (request.PartnerId.HasValue)
            {
                bom = await _context.BOMs
                    .Where(b => b.ItemId == request.ItemId
                                && b.IsActive
                                && b.PartnerId == request.PartnerId.Value
                                && b.ValidFrom <= now
                                && (b.ValidTo == null || b.ValidTo > now))
                    .OrderByDescending(b => b.Version)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            bom ??= await _context.BOMs
                .Where(b => b.ItemId == request.ItemId
                            && b.IsActive
                            && b.PartnerId == null
                            && b.ValidFrom <= now
                            && (b.ValidTo == null || b.ValidTo > now))
                .OrderByDescending(b => b.Version)
                .FirstOrDefaultAsync(cancellationToken);

            resolvedBomId = bom?.Id;
        }

        if (!resolvedRoutingId.HasValue)
        {
            var routing = await _context.Routings
                .Where(r => r.ItemId == request.ItemId && r.IsActive)
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync(cancellationToken);
            resolvedRoutingId = routing?.Id;
        }

        // Phase 17 §E5 — validate optional ClientOrder linkage before any
        // persistence so the failure case stays cheap.
        Domain.Entities.Customs.ClientOrder? clientOrder = null;
        if (request.ClientOrderId.HasValue && request.ClientOrderId.Value != Guid.Empty)
        {
            clientOrder = await _context.ClientOrders
                .FirstOrDefaultAsync(o => o.Id == request.ClientOrderId.Value, cancellationToken);
            if (clientOrder is null)
                return Result<Guid>.Failure($"ClientOrder '{request.ClientOrderId.Value}' does not exist.");
            if (clientOrder.Status is ClientOrderStatus.Closed or ClientOrderStatus.Cancelled)
                return Result<Guid>.Failure(
                    $"ClientOrder '{clientOrder.OrderNumber}' is {clientOrder.Status} and cannot accept new production orders.");
        }

        var order = new ProductionOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"LON-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            ItemId = request.ItemId,
            OrderQuantity = request.OrderQuantity,
            ProducedQuantity = 0,
            ScrapQuantity = 0,
            UoMId = request.UoMId,
            Status = ProductionOrderStatus.Draft,
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            BOMId = resolvedBomId,
            RoutingId = resolvedRoutingId,
            SalesOrderReference = request.SalesOrderReference,
            ClientOrderId = clientOrder?.Id,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        // P6.19 fix: the handler used to SaveChanges without adding to the DbSet,
        // so the API returned Success(newGuid) while the DB stayed empty. Every
        // subsequent "Release" or MaterialIssue failed because the PO wasn't
        // persisted. Add it explicitly now.
        _context.ProductionOrders.Add(order);

        // Phase 17 §E5 — first ProductionOrder on a non-Producing ClientOrder
        // transitions Status → Producing. Matches BLUEPRINT §5.1's computed
        // status ladder (Draft → Active → Producing → Shipped → Closed).
        if (clientOrder is not null &&
            clientOrder.Status is ClientOrderStatus.Draft or ClientOrderStatus.Active)
        {
            clientOrder.Status = ClientOrderStatus.Producing;
            clientOrder.ModifiedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(order.Id);
    }
}
