using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Production.Commands.CreateMaterialIssue;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Production.Commands.IssueAllMaterials;

/// <summary>
/// P5.2.1 — Issue all still-unissued materials for a Released production order
/// in one click. Walks <see cref="LON.Domain.Entities.Production.ProductionOrderMaterial"/>
/// rows and delegates to the existing MaterialIssue command with FEFO auto-pick.
/// The delta is RequiredQuantity − IssuedQuantity per material. When an item
/// hits zero remaining demand it is skipped. Returns the IssueNumber id on
/// success; on any per-line failure the whole call is reverted (single
/// SaveChanges scope via the underlying handler).
/// </summary>
public sealed record IssueAllMaterialsCommand(
    Guid ProductionOrderId,
    DateTime IssueDate,
    Guid? IssuedByEmployeeId = null) : ICommand<Result<Guid>>;

public sealed class IssueAllMaterialsCommandHandler
    : ICommandHandler<IssueAllMaterialsCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public IssueAllMaterialsCommandHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<Guid>> Handle(IssueAllMaterialsCommand request, CancellationToken ct)
    {
        var po = await _context.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == request.ProductionOrderId, ct);
        if (po == null) return Result<Guid>.Failure("PO not found.");
        if (po.Status == ProductionOrderStatus.Cancelled || po.Status == ProductionOrderStatus.Completed)
            return Result<Guid>.Failure($"Cannot bulk-issue to {po.Status} PO.");

        var materials = await _context.ProductionOrderMaterials
            .Where(m => m.ProductionOrderId == po.Id && !m.IsDeleted)
            .ToListAsync(ct);

        if (materials.Count == 0)
            return Result<Guid>.Failure("PO has no materials to issue. Release the PO first.");

        var lines = new List<MaterialIssueLineDto>();
        foreach (var m in materials)
        {
            var remaining = m.RequiredQuantity - m.IssuedQuantity;
            if (remaining <= 0m) continue;
            lines.Add(new MaterialIssueLineDto
            {
                ItemId = m.ItemId,
                Quantity = remaining,
                UoMId = m.UoMId,
                // BatchNumber/MRN left null → underlying handler runs FEFO auto-pick
            });
        }

        if (lines.Count == 0)
            return Result<Guid>.Failure("Сите материјали за овој PO се веќе издадени.");

        var inner = new CreateMaterialIssueCommand
        {
            ProductionOrderId = po.Id,
            IssueDate = request.IssueDate,
            IssuedByEmployeeId = request.IssuedByEmployeeId,
            Lines = lines,
        };
        return await _mediator.Send(inner, ct);
    }
}
