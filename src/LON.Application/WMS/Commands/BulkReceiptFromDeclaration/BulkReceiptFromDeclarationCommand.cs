using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.WMS.Commands.CreateReceipt;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Commands.BulkReceiptFromDeclaration;

/// <summary>
/// P5.2.3 — Bulk Receipt from Invoice / Customs Declaration.
///
/// Given an existing import CustomsDeclaration (typically filed under an IM
/// 4200 LON procedure) and a target warehouse, explode every
/// CustomsDeclarationLine into a corresponding ReceiptLine and delegate to
/// <see cref="CreateReceiptCommand"/>. The MRN on the declaration is applied
/// to every receipt line so the existing MRN registry / inflate-for-waste /
/// LON process-state wiring kicks in untouched.
///
/// One click replaces: pasting line data row-by-row into the Receipt form
/// for 20-50 declaration lines.
/// </summary>
public sealed record BulkReceiptFromDeclarationCommand(
    Guid CustomsDeclarationId,
    Guid WarehouseId,
    Guid? TargetLocationId,
    DateTime? ReceiptDate,
    string? ReferenceNumber) : ICommand<Result<BulkReceiptFromDeclarationResult>>;

public sealed record BulkReceiptFromDeclarationResult(
    Guid ReceiptId,
    int LinesCreated,
    decimal TotalQuantity);

public sealed class BulkReceiptFromDeclarationHandler
    : ICommandHandler<BulkReceiptFromDeclarationCommand, Result<BulkReceiptFromDeclarationResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _mediator;

    public BulkReceiptFromDeclarationHandler(IApplicationDbContext context, ISender mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<Result<BulkReceiptFromDeclarationResult>> Handle(
        BulkReceiptFromDeclarationCommand request, CancellationToken cancellationToken)
    {
        var declaration = await _context.CustomsDeclarations
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.CustomsDeclarationId, cancellationToken);

        if (declaration is null)
            return Result<BulkReceiptFromDeclarationResult>.Failure(
                "declaration.not_found", "Customs declaration not found.");

        if (declaration.Lines.Count == 0)
            return Result<BulkReceiptFromDeclarationResult>.Failure(
                ErrorCodes.DeclarationEmptyLines,
                "Declaration has no lines to explode into a receipt.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, cancellationToken);
        if (warehouse is null)
            return Result<BulkReceiptFromDeclarationResult>.Failure(
                ErrorCodes.LocationNotFound, "Warehouse not found or inactive.");

        // Reuse CreateReceiptCommand so MRN registry / inflate / LON-state logic
        // stays in one place. The handler already hard-enforces MRN overdraw.
        var lines = declaration.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new ReceiptLineDto
            {
                ItemId = l.ItemId,
                UoMId = l.UoMId,
                Quantity = l.Quantity,
                LocationId = request.TargetLocationId,
                MRN = declaration.MRN,
                BatchNumber = declaration.DeclarationNumber, // legacy habit: MRN-derived batch
                QualityStatus = Domain.Enums.QualityStatus.OK,
                CustomsDeclarationId = declaration.Id,
            })
            .ToList();

        var inner = await _mediator.Send(new CreateReceiptCommand
        {
            ReceiptDate = request.ReceiptDate ?? DateTime.UtcNow,
            WarehouseId = request.WarehouseId,
            LocationId = request.TargetLocationId,
            ReferenceNumber = request.ReferenceNumber
                ?? $"BULK-{declaration.DeclarationNumber}",
            PartnerId = declaration.PartnerId,
            Lines = lines
        }, cancellationToken);

        if (!inner.IsSuccess)
            return inner.ErrorCode is { } code
                ? Result<BulkReceiptFromDeclarationResult>.Failure(code, inner.ErrorMessage ?? "bulk receipt failed")
                : Result<BulkReceiptFromDeclarationResult>.Failure(inner.ErrorMessage ?? "bulk receipt failed");

        return Result<BulkReceiptFromDeclarationResult>.Success(new BulkReceiptFromDeclarationResult(
            inner.Data,
            lines.Count,
            lines.Sum(l => l.Quantity)));
    }
}
