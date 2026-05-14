using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E1 — manually cancel a ClientOrder.
/// Sets Status=Cancelled + soft-deletes the order. Per Phase 17 §E14 (user
/// decision: block-delete on children), the command refuses if the order has
/// any non-deleted linked CustomsDeclaration / ProductionOrder / Shipment.
/// The user must explicitly close/cancel those first, or use a future
/// "cascade cancel" admin action (post-v1).
/// </summary>
public record CancelClientOrderCommand : ICommand<Result<Guid>>
{
    public Guid Id { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class CancelClientOrderCommandHandler : ICommandHandler<CancelClientOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelClientOrderCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CancelClientOrderCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<Guid>.Failure("Cancellation Reason is required.");

        var order = await _context.ClientOrders
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure($"ClientOrder '{request.Id}' does not exist.");

        if (order.Status == ClientOrderStatus.Closed)
            return Result<Guid>.Failure("Closed orders cannot be cancelled.");

        // §E14 block-delete policy. Children that already entered the legal
        // chain (declarations / production / shipments) must be closed out
        // explicitly before the parent can be cancelled.
        var blockers = new List<string>();
        var openDeclarations = await _context.CustomsDeclarations
            .Where(d => d.ClientOrderId == request.Id && !d.IsDeleted)
            .CountAsync(cancellationToken);
        if (openDeclarations > 0)
            blockers.Add($"{openDeclarations} CustomsDeclaration(s)");

        var openProductionOrders = await _context.ProductionOrders
            .Where(p => p.ClientOrderId == request.Id && !p.IsDeleted)
            .CountAsync(cancellationToken);
        if (openProductionOrders > 0)
            blockers.Add($"{openProductionOrders} ProductionOrder(s)");

        var openShipments = await _context.Shipments
            .Where(s => s.ClientOrderId == request.Id && !s.IsDeleted)
            .CountAsync(cancellationToken);
        if (openShipments > 0)
            blockers.Add($"{openShipments} Shipment(s)");

        if (blockers.Count > 0)
            return Result<Guid>.Failure("ClientOrderHasChildren",
                $"Cannot cancel ClientOrder while it still has non-deleted children: {string.Join(", ", blockers)}.");

        order.Status = ClientOrderStatus.Cancelled;
        order.CancellationReason = request.Reason.Trim();
        order.IsDeleted = true;
        order.DeletedAt = DateTime.UtcNow;
        order.DeletedBy = _currentUser?.AuditName ?? "System";

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(order.Id);
    }
}

/// <summary>
/// Phase 17 §E14 — restore a soft-deleted ClientOrder from the recycle bin.
/// Clears IsDeleted + DeletedAt + DeletedBy. Status returns to Cancelled (it
/// was set when Cancel ran) — the operator can move it back to Active via a
/// separate status command if desired.
/// </summary>
public record RestoreClientOrderCommand(Guid Id) : ICommand<Result<Guid>>;

public class RestoreClientOrderCommandHandler : ICommandHandler<RestoreClientOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RestoreClientOrderCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(RestoreClientOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.ClientOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure($"ClientOrder '{request.Id}' does not exist.");
        if (!order.IsDeleted)
            return Result<Guid>.Failure("ClientOrder is not soft-deleted.");

        order.IsDeleted = false;
        order.DeletedAt = null;
        order.DeletedBy = null;
        order.ModifiedAt = DateTime.UtcNow;
        order.ModifiedBy = _currentUser?.AuditName ?? "System";

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(order.Id);
    }
}
