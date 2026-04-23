using LON.Application.Common.Interfaces;
using LON.Application.Common.Queries;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Queries.GetSkart;

/// <summary>
/// P15.3 — list all Skart reports for the current tenant, newest first.
/// Optional filters: only-open, item, MRN. Used by the <c>/warehouse/skart</c>
/// register page.
/// </summary>
public record GetSkartQuery(bool OpenOnly = false, Guid? ItemId = null, string? MRN = null)
    : IQuery<List<SkartRow>>;

public record SkartRow(
    Guid Id,
    string SkartNumber,
    DateTime ReportedAt,
    Guid ReceiptLineId,
    string? ReceiptNumber,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string? BatchNumber,
    string? MRN,
    decimal SkartQuantity,
    Guid UoMId,
    string UoMCode,
    string Reason,
    SkartResolution Resolution,
    DateTime? ResolvedAt,
    string? ResolutionNote);

public class GetSkartQueryHandler : IQueryHandler<GetSkartQuery, List<SkartRow>>
{
    private readonly IApplicationDbContext _context;

    public GetSkartQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SkartRow>> Handle(GetSkartQuery request, CancellationToken ct)
    {
        var q = _context.Skarts
            .Include(s => s.Item)
            .Include(s => s.UoM)
            .Include(s => s.ReceiptLine).ThenInclude(l => l.Receipt)
            .Where(s => !s.IsDeleted);

        if (request.OpenOnly)
            q = q.Where(s => s.Resolution == SkartResolution.Open);
        if (request.ItemId.HasValue)
            q = q.Where(s => s.ItemId == request.ItemId.Value);
        if (!string.IsNullOrWhiteSpace(request.MRN))
            q = q.Where(s => s.MRN == request.MRN);

        var rows = await q
            .OrderByDescending(s => s.ReportedAt)
            .Select(s => new SkartRow(
                s.Id,
                s.SkartNumber,
                s.ReportedAt,
                s.ReceiptLineId,
                s.ReceiptLine != null && s.ReceiptLine.Receipt != null
                    ? s.ReceiptLine.Receipt.ReceiptNumber
                    : null,
                s.ItemId,
                s.Item.Code,
                s.Item.Name,
                s.BatchNumber,
                s.MRN,
                s.SkartQuantity,
                s.UoMId,
                s.UoM.Code,
                s.Reason,
                s.Resolution,
                s.ResolvedAt,
                s.ResolutionNote))
            .ToListAsync(ct);

        return rows;
    }
}
