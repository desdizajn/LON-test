using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using LON.Domain.Entities.Traceability;
using LON.Domain.Entities.WMS;
using LON.Domain.Enums;
using LON.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.CreateProductionReceipt;

public record CreateProductionReceiptCommand : ICommand<Result<Guid>>
{
    public Guid ProductionOrderId { get; init; }
    public DateTime ReceiptDate { get; init; }
    public Guid ItemId { get; init; }
    public decimal Quantity { get; init; }
    public decimal? ScrapQuantity { get; init; }
    public Guid UoMId { get; init; }
    public Guid LocationId { get; init; }
    public string BatchNumber { get; init; } = string.Empty;
    public QualityStatus QualityStatus { get; init; } = QualityStatus.OK;
    public Guid? ReceivedByEmployeeId { get; init; }
    /// <summary>
    /// Optional explicit list of WIP portions consumed into this FG batch. When
    /// provided, the handler decrements the matching InProduction balances and
    /// weights TraceLinks accordingly. When omitted, the handler links to every
    /// MaterialIssue on the PO without decrementing WIP — in that mode WIP is
    /// reconciled on PO close / P2.6 flows.
    /// </summary>
    public List<MaterialConsumptionDto>? MaterialConsumption { get; init; }
}

public record MaterialConsumptionDto
{
    public Guid MaterialIssueId { get; init; }
    public decimal Quantity { get; init; }
}

public class CreateProductionReceiptCommandHandler
    : ICommandHandler<CreateProductionReceiptCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateProductionReceiptCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(
        CreateProductionReceiptCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0m)
            return Result<Guid>.Failure("Quantity must be positive.");
        if (request.ScrapQuantity is < 0m)
            return Result<Guid>.Failure("ScrapQuantity cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.BatchNumber))
            return Result<Guid>.Failure("BatchNumber is required for a production receipt.");

        var order = await _context.ProductionOrders
            .FirstOrDefaultAsync(po => po.Id == request.ProductionOrderId, cancellationToken);
        if (order is null)
            return Result<Guid>.Failure($"ProductionOrder '{request.ProductionOrderId}' not found.");
        if (order.Status == ProductionOrderStatus.Cancelled
            || order.Status == ProductionOrderStatus.Completed
            || order.Status == ProductionOrderStatus.Closed)
            return Result<Guid>.Failure(
                $"Cannot receive production into ProductionOrder in status {order.Status}.");
        if (order.ItemId != request.ItemId)
            return Result<Guid>.Failure(
                "ProductionReceipt Item must match the ProductionOrder's output Item.");

        // Optional WIP consumption. Validate upfront so we can fail fast before any
        // write; collect resolved issues + matching InProduction balances for the
        // persist step.
        List<ResolvedConsumption> consumptions = new();
        if (request.MaterialConsumption is { Count: > 0 })
        {
            var check = await ResolveConsumptionsAsync(order.Id, request.MaterialConsumption, cancellationToken);
            if (!check.IsSuccess) return Result<Guid>.Failure(check.ErrorMessage!);
            consumptions = check.Items!;
        }

        // Roll qty onto the order; enforce no-over-production vs OrderQuantity.
        var newProduced = order.ProducedQuantity + request.Quantity;
        var newScrap = order.ScrapQuantity + (request.ScrapQuantity ?? 0m);
        if (newProduced + newScrap > order.OrderQuantity)
        {
            return Result<Guid>.Failure(
                $"Production receipt would exceed ordered quantity. " +
                $"Ordered {order.OrderQuantity}, produced-after={newProduced}, scrap-after={newScrap}.");
        }

        var receiptNumber = $"PR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";
        var pr = new ProductionReceipt
        {
            Id = Guid.NewGuid(),
            ReceiptNumber = receiptNumber,
            ReceiptDate = request.ReceiptDate,
            ProductionOrderId = order.Id,
            ItemId = request.ItemId,
            BatchNumber = request.BatchNumber,
            Quantity = request.Quantity,
            ScrapQuantity = request.ScrapQuantity,
            UoMId = request.UoMId,
            LocationId = request.LocationId,
            QualityStatus = request.QualityStatus,
            ReceivedByEmployeeId = request.ReceivedByEmployeeId
        };
        _context.ProductionReceipts.Add(pr);

        _context.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            MovementNumber = $"MOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            MovementDate = request.ReceiptDate,
            Type = MovementType.ProductionReceipt,
            ItemId = request.ItemId,
            BatchNumber = request.BatchNumber,
            MRN = null,
            FromLocationId = null,
            ToLocationId = request.LocationId,
            Quantity = request.Quantity,
            UoMId = request.UoMId,
            ReferenceNumber = receiptNumber,
            ReferenceId = pr.Id
        });

        // Upsert FG balance. No LON state on FG — lineage is captured by TraceLink;
        // the balance itself is treated as domestic product until an EX declaration
        // re-attaches it to the LON chain in P2.6.
        var fgBalance = await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                b.ItemId == request.ItemId
                && b.LocationId == request.LocationId
                && b.BatchNumber == request.BatchNumber
                && b.UoMId == request.UoMId
                && b.QualityStatus == request.QualityStatus
                && b.MRN == null,
            cancellationToken);
        if (fgBalance is null)
        {
            _context.InventoryBalances.Add(new InventoryBalance
            {
                Id = Guid.NewGuid(),
                ItemId = request.ItemId,
                LocationId = request.LocationId,
                BatchNumber = request.BatchNumber,
                MRN = null,
                Quantity = request.Quantity,
                UoMId = request.UoMId,
                QualityStatus = request.QualityStatus,
                ExpiryDate = null,
                LonProcessState = null
            });
        }
        else
        {
            fgBalance.AddQuantity(request.Quantity);
        }

        // TraceLinks: forward chain from each feeding MaterialIssue to this FG
        // batch. Two modes:
        //   * Explicit consumption: one TraceLink per entry, quantity taken from the
        //     caller's breakdown; the InProduction sibling balance is decremented.
        //   * Auto mode: one TraceLink per MaterialIssue on the PO, quantity echoes
        //     the full issue qty (informational lineage; no WIP decrement).
        if (consumptions.Count > 0)
        {
            foreach (var c in consumptions)
            {
                _context.TraceLinks.Add(new TraceLink
                {
                    Id = Guid.NewGuid(),
                    SourceType = "MaterialIssue",
                    SourceId = c.Issue.Id,
                    SourceBatchNumber = c.Issue.BatchNumber,
                    SourceMRN = c.Issue.MRN,
                    TargetType = "ProductionReceipt",
                    TargetId = pr.Id,
                    TargetBatchNumber = request.BatchNumber,
                    TargetMRN = null,
                    ItemId = c.Issue.ItemId,
                    Quantity = c.Quantity,
                    LinkDate = request.ReceiptDate
                });

                if (c.WipBalance is not null)
                    c.WipBalance.SubtractQuantity(c.Quantity);
            }
        }
        else
        {
            var issues = await _context.MaterialIssues
                .Where(mi => mi.ProductionOrderId == order.Id)
                .ToListAsync(cancellationToken);
            foreach (var mi in issues)
            {
                _context.TraceLinks.Add(new TraceLink
                {
                    Id = Guid.NewGuid(),
                    SourceType = "MaterialIssue",
                    SourceId = mi.Id,
                    SourceBatchNumber = mi.BatchNumber,
                    SourceMRN = mi.MRN,
                    TargetType = "ProductionReceipt",
                    TargetId = pr.Id,
                    TargetBatchNumber = request.BatchNumber,
                    TargetMRN = null,
                    ItemId = mi.ItemId,
                    Quantity = mi.Quantity,
                    LinkDate = request.ReceiptDate
                });
            }
        }

        // Order bookkeeping.
        order.ProducedQuantity = newProduced;
        order.ScrapQuantity = newScrap;
        if (order.Status == ProductionOrderStatus.Draft || order.Status == ProductionOrderStatus.Released)
        {
            order.Status = ProductionOrderStatus.InProgress;
            order.ActualStartDate ??= request.ReceiptDate;
        }
        if (order.ProducedQuantity + order.ScrapQuantity >= order.OrderQuantity)
        {
            order.Status = ProductionOrderStatus.Completed;
            order.ActualEndDate ??= request.ReceiptDate;

            order.AddDomainEvent(new ProductionOrderCompletedEvent
            {
                ProductionOrderId = order.Id,
                OrderNumber = order.OrderNumber,
                ItemId = order.ItemId,
                ProducedQuantity = order.ProducedQuantity
            });
        }

        order.AddDomainEvent(new FGReceivedEvent
        {
            ProductionOrderId = order.Id,
            ItemId = request.ItemId,
            BatchNumber = request.BatchNumber,
            Quantity = request.Quantity,
            LocationId = request.LocationId
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(pr.Id);
    }

    private sealed class ResolvedConsumption
    {
        public MaterialIssue Issue { get; init; } = null!;
        public InventoryBalance? WipBalance { get; init; }
        public decimal Quantity { get; init; }
    }

    private sealed class ConsumptionResolution
    {
        public bool IsSuccess => ErrorMessage is null;
        public string? ErrorMessage { get; init; }
        public List<ResolvedConsumption>? Items { get; init; }
    }

    private async Task<ConsumptionResolution> ResolveConsumptionsAsync(
        Guid orderId,
        List<MaterialConsumptionDto> spec,
        CancellationToken ct)
    {
        var issueIds = spec.Select(s => s.MaterialIssueId).Distinct().ToList();
        var issues = await _context.MaterialIssues
            .Where(mi => issueIds.Contains(mi.Id))
            .ToListAsync(ct);

        var items = new List<ResolvedConsumption>(spec.Count);
        foreach (var entry in spec)
        {
            if (entry.Quantity <= 0m)
                return new ConsumptionResolution
                {
                    ErrorMessage = $"MaterialConsumption for issue '{entry.MaterialIssueId}': quantity must be positive."
                };

            var issue = issues.FirstOrDefault(i => i.Id == entry.MaterialIssueId);
            if (issue is null)
                return new ConsumptionResolution
                {
                    ErrorMessage = $"MaterialIssue '{entry.MaterialIssueId}' not found."
                };
            if (issue.ProductionOrderId != orderId)
                return new ConsumptionResolution
                {
                    ErrorMessage = $"MaterialIssue '{entry.MaterialIssueId}' does not belong to this ProductionOrder."
                };

            // WIP sibling balance (LonProcessState=InProduction) keyed on the same
            // batch+MRN+UoM+Quality as the source issue. Only present when the
            // issued material was LON-imported — non-LON lines leave no WIP trail.
            var wip = await _context.InventoryBalances.FirstOrDefaultAsync(b =>
                    b.ItemId == issue.ItemId
                    && b.BatchNumber == issue.BatchNumber
                    && b.MRN == issue.MRN
                    && b.UoMId == issue.UoMId
                    && b.LonProcessState == LonProcessState.InProduction,
                ct);

            if (wip is not null && wip.Quantity < entry.Quantity)
            {
                return new ConsumptionResolution
                {
                    ErrorMessage =
                        $"Insufficient WIP for issue '{entry.MaterialIssueId}' " +
                        $"(batch '{issue.BatchNumber}' MRN '{issue.MRN}'): " +
                        $"demand {entry.Quantity}, available {wip.Quantity}."
                };
            }

            items.Add(new ResolvedConsumption
            {
                Issue = issue,
                WipBalance = wip,
                Quantity = entry.Quantity
            });
        }

        return new ConsumptionResolution { Items = items };
    }
}
