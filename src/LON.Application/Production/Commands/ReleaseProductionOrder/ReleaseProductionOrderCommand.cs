using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Production;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.ReleaseProductionOrder;

/// <summary>
/// P5.2.6 — Release a Draft production order into Released status in one action.
/// Copies BOM lines into <see cref="ProductionOrderMaterial"/> rows (with
/// RequiredQuantity scaled to OrderQuantity / BOM.BaseQuantity), copies
/// Routing operations into <see cref="ProductionOrderOperation"/> rows, and
/// reserves material quantities equal to RequiredQuantity.
///
/// Idempotent-ish: running it on an already-Released order returns the existing
/// id but does not duplicate children. Cannot release Completed / Cancelled.
/// </summary>
public sealed record ReleaseProductionOrderCommand(Guid ProductionOrderId)
    : ICommand<Result<Guid>>;

public sealed class ReleaseProductionOrderCommandHandler
    : ICommandHandler<ReleaseProductionOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public ReleaseProductionOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(ReleaseProductionOrderCommand request, CancellationToken ct)
    {
        var po = await _context.ProductionOrders
            .Include(p => p.Materials)
            .Include(p => p.Operations)
            .FirstOrDefaultAsync(p => p.Id == request.ProductionOrderId, ct);

        if (po == null)
            return Result<Guid>.Failure($"PO {request.ProductionOrderId} not found.");

        if (po.Status == ProductionOrderStatus.Completed || po.Status == ProductionOrderStatus.Cancelled)
            return Result<Guid>.Failure($"Cannot release a {po.Status} order.");

        if (po.Status != ProductionOrderStatus.Draft)
            // already released / in progress — idempotent return
            return Result<Guid>.Success(po.Id);

        // Expand BOM → ProductionOrderMaterials
        if (po.BOMId.HasValue && po.Materials.Count == 0)
        {
            var bom = await _context.BOMs
                .Include(b => b.Lines).ThenInclude(l => l.Item)
                .FirstOrDefaultAsync(b => b.Id == po.BOMId.Value, ct);
            if (bom == null)
                return Result<Guid>.Failure($"BOM {po.BOMId.Value} not found.");
            if (bom.BaseQuantity <= 0m)
                return Result<Guid>.Failure("BOM.BaseQuantity must be > 0 for scaling.");

            var scale = po.OrderQuantity / bom.BaseQuantity;
            int line = 0;
            foreach (var bl in bom.Lines.OrderBy(x => x.LineNumber))
            {
                line++;
                var required = bl.Quantity * scale * (1 + bl.ScrapPercentage / 100m);

                // P15.6c — snapshot the EFFECTIVE waste configuration onto the
                // PO material row: BOMLine override (if set) beats Item default.
                // Each slot is resolved independently so a BOM can override
                // only primary waste and inherit the rest.
                var it = bl.Item;
                // P15.16 — planned-vs-effective. Plan = BOM line × scrap pad,
                // effective = same on release. Diverges later if operator edits.
                var plannedPerUnit = bl.Quantity * (1 + bl.ScrapPercentage / 100m);
                _context.ProductionOrderMaterials.Add(new ProductionOrderMaterial
                {
                    Id = Guid.NewGuid(),
                    ProductionOrderId = po.Id,
                    LineNumber = line,
                    ItemId = bl.ItemId,
                    RequiredQuantity = Math.Round(required, 4),
                    IssuedQuantity = 0m,
                    ReservedQuantity = Math.Round(required, 4),
                    UoMId = bl.UoMId,
                    PlannedQuantityPerUnit = Math.Round(plannedPerUnit, 6),
                    PrimaryWasteItemId = bl.PrimaryWasteItemId ?? it?.PrimaryWasteItemId,
                    PrimaryWastePercentage = bl.PrimaryWastePercentage ?? it?.PrimaryWastePercentage,
                    SecondaryWasteItemId = bl.SecondaryWasteItemId ?? it?.SecondaryWasteItemId,
                    SecondaryWastePercentage = bl.SecondaryWastePercentage ?? it?.SecondaryWastePercentage,
                    TertiaryWasteItemId = bl.TertiaryWasteItemId ?? it?.TertiaryWasteItemId,
                    TertiaryWastePercentage = bl.TertiaryWastePercentage ?? it?.TertiaryWastePercentage,
                    ZagubaItemId = bl.ZagubaItemId ?? it?.ZagubaItemId,
                    ZagubaPercentage = bl.ZagubaPercentage ?? it?.ZagubaPercentage,
                });
            }
        }

        // Expand Routing → ProductionOrderOperations
        if (po.RoutingId.HasValue && po.Operations.Count == 0)
        {
            var routing = await _context.Routings
                .Include(r => r.Operations)
                .FirstOrDefaultAsync(r => r.Id == po.RoutingId.Value, ct);
            if (routing != null)
            {
                foreach (var op in routing.Operations.OrderBy(x => x.SequenceNumber))
                {
                    _context.ProductionOrderOperations.Add(new ProductionOrderOperation
                    {
                        Id = Guid.NewGuid(),
                        ProductionOrderId = po.Id,
                        SequenceNumber = op.SequenceNumber,
                        OperationCode = op.OperationCode,
                        Description = op.Description,
                        WorkCenterId = op.WorkCenterId,
                        StandardTimeMinutes = op.StandardTimeMinutes + op.SetupTimeMinutes,
                        ActualTimeMinutes = 0m,
                        IsCompleted = false,
                    });
                }
            }
        }

        po.Status = ProductionOrderStatus.Released;
        await _context.SaveChangesAsync(ct);
        return Result<Guid>.Success(po.Id);
    }
}
