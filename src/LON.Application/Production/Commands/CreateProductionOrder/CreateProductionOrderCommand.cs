using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using LON.Domain.Events;

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
            BOMId = request.BOMId,
            RoutingId = request.RoutingId,
            SalesOrderReference = request.SalesOrderReference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(order.Id);
    }
}
