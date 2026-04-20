using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Queries.MassLocationTransferPreview;

/// <summary>
/// P5.2.7 — preview the rows that a subsequent MassLocationTransferCommand
/// would touch. The UI calls this first so the user sees "you're about to
/// move N balances totalling Q units" before committing.
///
/// Uses the same predicate as the command to guarantee parity: anything
/// returned here will be moved if the user confirms with the same filter.
/// </summary>
public sealed record MassLocationTransferPreviewQuery(
    Guid? ItemId = null,
    string? BatchNumber = null,
    string? MRN = null,
    Guid? SourceWarehouseId = null,
    Guid? SourceLocationId = null,
    QualityStatus? QualityStatus = null,
    LonProcessState? LonProcessState = null,
    Guid? TargetLocationId = null) : IRequest<Result<MassTransferPreview>>;

public sealed record MassTransferPreview(
    int BalancesMatched,
    decimal TotalQuantity,
    IReadOnlyList<PreviewRow> Rows);

public sealed record PreviewRow(
    Guid BalanceId,
    Guid ItemId,
    string? ItemCode,
    string? ItemName,
    Guid LocationId,
    string? LocationCode,
    string? BatchNumber,
    string? MRN,
    decimal Quantity,
    QualityStatus QualityStatus,
    LonProcessState? LonProcessState);

public class MassLocationTransferPreviewQueryHandler
    : IRequestHandler<MassLocationTransferPreviewQuery, Result<MassTransferPreview>>
{
    private readonly IApplicationDbContext _context;

    public MassLocationTransferPreviewQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MassTransferPreview>> Handle(
        MassLocationTransferPreviewQuery request, CancellationToken cancellationToken)
    {
        var hasFilter =
            request.ItemId.HasValue ||
            !string.IsNullOrWhiteSpace(request.BatchNumber) ||
            !string.IsNullOrWhiteSpace(request.MRN) ||
            request.SourceWarehouseId.HasValue ||
            request.SourceLocationId.HasValue ||
            request.QualityStatus.HasValue ||
            request.LonProcessState.HasValue;

        if (!hasFilter)
            return Result<MassTransferPreview>.Failure(
                "At least one filter criterion is required.");

        var query = _context.InventoryBalances
            .Include(b => b.Location)
            .Include(b => b.Item)
            .Where(b => b.Quantity > 0m);

        if (request.ItemId.HasValue)
            query = query.Where(b => b.ItemId == request.ItemId.Value);
        if (!string.IsNullOrWhiteSpace(request.BatchNumber))
        {
            var batch = request.BatchNumber.Trim();
            query = query.Where(b => b.BatchNumber == batch);
        }
        if (!string.IsNullOrWhiteSpace(request.MRN))
        {
            var mrn = request.MRN.Trim();
            query = query.Where(b => b.MRN == mrn);
        }
        if (request.SourceWarehouseId.HasValue)
            query = query.Where(b => b.Location.WarehouseId == request.SourceWarehouseId.Value);
        if (request.SourceLocationId.HasValue)
            query = query.Where(b => b.LocationId == request.SourceLocationId.Value);
        if (request.QualityStatus.HasValue)
            query = query.Where(b => b.QualityStatus == request.QualityStatus.Value);
        if (request.LonProcessState.HasValue)
            query = query.Where(b => b.LonProcessState == request.LonProcessState.Value);

        if (request.TargetLocationId.HasValue)
            query = query.Where(b => b.LocationId != request.TargetLocationId.Value);

        var rows = await query
            .OrderBy(b => b.Location.Code)
            .Take(500) // safety cap — preview UI shows a summary anyway
            .Select(b => new PreviewRow(
                b.Id,
                b.ItemId,
                b.Item.Code,
                b.Item.Name,
                b.LocationId,
                b.Location.Code,
                b.BatchNumber,
                b.MRN,
                b.Quantity,
                b.QualityStatus,
                b.LonProcessState))
            .ToListAsync(cancellationToken);

        var total = rows.Sum(r => r.Quantity);

        return Result<MassTransferPreview>.Success(new MassTransferPreview(
            rows.Count, total, rows));
    }
}
