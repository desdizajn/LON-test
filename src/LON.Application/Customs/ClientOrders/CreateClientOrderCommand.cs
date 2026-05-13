using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Common;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E1 — create a new ClientOrder.
///
/// Pre-conditions:
///   - <see cref="CustomerPartnerId"/> resolves to an active Partner of type Customer.
///   - <see cref="LONAuthorizationId"/> resolves to an active LONAuthorization
///     (required — inward-processing orders always have a LON in v1).
///   - <see cref="OrderDate"/> defaults to today if not supplied.
///
/// Number: stamped via INumberSequenceService + NumberFormatter
/// (CO-{year}-{seq:D6}). Status starts at Draft.
/// </summary>
public record CreateClientOrderCommand : ICommand<Result<Guid>>
{
    public Guid CustomerPartnerId { get; init; }
    public Guid LONAuthorizationId { get; init; }
    public string? CustomerOrderReference { get; init; }
    public DateTime? OrderDate { get; init; }
    public DateTime? RequestedShipDate { get; init; }
    public string? Notes { get; init; }
}

public class CreateClientOrderCommandHandler : ICommandHandler<CreateClientOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _currentTenant;
    private readonly INumberSequenceService _sequence;

    public CreateClientOrderCommandHandler(
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

    public async Task<Result<Guid>> Handle(CreateClientOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerPartnerId == Guid.Empty)
            return Result<Guid>.Failure("CustomerPartnerId is required.");
        if (request.LONAuthorizationId == Guid.Empty)
            return Result<Guid>.Failure("LONAuthorizationId is required (inward-processing orders always need a LON).");

        var resolvedTenant = await _currentTenant.GetTenantIdAsync(cancellationToken);
        if (resolvedTenant is null || resolvedTenant.Value == Guid.Empty)
            return Result<Guid>.Failure("Tenant context not resolved.");
        var tenantId = resolvedTenant.Value;

        // Cheap existence checks (don't load full entities).
        var customerOk = await _context.Partners
            .AnyAsync(p => p.Id == request.CustomerPartnerId && !p.IsDeleted, cancellationToken);
        if (!customerOk)
            return Result<Guid>.Failure($"Customer partner '{request.CustomerPartnerId}' does not exist or is inactive.");

        var authOk = await _context.LONAuthorizations
            .AnyAsync(a => a.Id == request.LONAuthorizationId && !a.IsDeleted, cancellationToken);
        if (!authOk)
            return Result<Guid>.Failure($"LON authorization '{request.LONAuthorizationId}' does not exist.");

        var orderDate = request.OrderDate ?? DateTime.UtcNow.Date;
        var seq = await _sequence.NextAsync("ClientOrder", tenantId, cancellationToken);
        var orderNumber = NumberFormatter.ClientOrder(orderDate.Year, seq);

        var order = new ClientOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = orderNumber,
            CustomerPartnerId = request.CustomerPartnerId,
            LONAuthorizationId = request.LONAuthorizationId,
            CustomerOrderReference = request.CustomerOrderReference?.Trim(),
            OrderDate = orderDate,
            RequestedShipDate = request.RequestedShipDate,
            Status = Domain.Enums.ClientOrderStatus.Draft,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser?.AuditName ?? "System",
        };

        _context.ClientOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(order.Id);
    }
}
