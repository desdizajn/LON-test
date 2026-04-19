using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Queries.ListImportSessions;

public record ListImportSessionsQuery(int Take = 50) : IRequest<Result<List<ImportSessionSummaryDto>>>;

public sealed record ImportSessionSummaryDto(
    Guid Id,
    string OriginalFileName,
    ImportSourceFormat Format,
    long FileSizeBytes,
    ImportSessionStatus Status,
    int RowCount,
    string? TargetEntity,
    Guid? PartnerContextId,
    DateTime CreatedAt);

public class ListImportSessionsQueryHandler
    : IRequestHandler<ListImportSessionsQuery, Result<List<ImportSessionSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public ListImportSessionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ImportSessionSummaryDto>>> Handle(
        ListImportSessionsQuery request, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(request.Take, 1, 500);
        var rows = await _context.ImportSessions
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .Select(s => new ImportSessionSummaryDto(
                s.Id,
                s.OriginalFileName,
                s.SourceFormat,
                s.FileSizeBytes,
                s.Status,
                s.RowCount,
                s.TargetEntity,
                s.PartnerContextId,
                s.CreatedAt))
            .ToListAsync(cancellationToken);
        return Result<List<ImportSessionSummaryDto>>.Success(rows);
    }
}
