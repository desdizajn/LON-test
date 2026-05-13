using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E1 — manually cancel a ClientOrder.
/// Sets Status=Cancelled + soft-deletes the order. Cascading soft-delete of
/// linked entities lands with §E14 (recycle bin).
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

        order.Status = ClientOrderStatus.Cancelled;
        order.CancellationReason = request.Reason.Trim();
        order.IsDeleted = true;
        order.DeletedAt = DateTime.UtcNow;
        order.DeletedBy = _currentUser?.AuditName ?? "System";

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(order.Id);
    }
}
