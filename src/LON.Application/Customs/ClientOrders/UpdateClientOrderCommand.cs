using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E1 — update mutable fields on a ClientOrder.
/// Only allowed while Status ∈ {Draft, Active, Producing}. Once Shipped/Closed,
/// the order is frozen except for Cancel (separate command).
/// </summary>
public record UpdateClientOrderCommand : ICommand<Result<Guid>>
{
    public Guid Id { get; init; }
    public string? CustomerOrderReference { get; init; }
    public DateTime? RequestedShipDate { get; init; }
    public string? Notes { get; init; }
}

public class UpdateClientOrderCommandHandler : ICommandHandler<UpdateClientOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateClientOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(UpdateClientOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.ClientOrders
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure($"ClientOrder '{request.Id}' does not exist.");

        if (order.Status is ClientOrderStatus.Shipped or ClientOrderStatus.Closed or ClientOrderStatus.Cancelled)
            return Result<Guid>.Failure(
                $"ClientOrder '{order.OrderNumber}' is in status '{order.Status}' and cannot be edited.");

        if (request.CustomerOrderReference is not null)
            order.CustomerOrderReference = request.CustomerOrderReference.Trim();
        if (request.RequestedShipDate.HasValue)
            order.RequestedShipDate = request.RequestedShipDate;
        if (request.Notes is not null)
            order.Notes = request.Notes;

        order.ModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(order.Id);
    }
}
