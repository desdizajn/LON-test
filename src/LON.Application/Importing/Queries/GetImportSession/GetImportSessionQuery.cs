using System.Text.Json;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Queries.GetImportSession;

public record GetImportSessionQuery(Guid SessionId) : IRequest<Result<ImportSessionDto>>;

public class GetImportSessionQueryHandler
    : IRequestHandler<GetImportSessionQuery, Result<ImportSessionDto>>
{
    private const int PreviewRowLimit = 20;

    private readonly IApplicationDbContext _context;

    public GetImportSessionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ImportSessionDto>> Handle(
        GetImportSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session is null)
            return Result<ImportSessionDto>.Failure($"Import session '{request.SessionId}' not found.");

        var headers = JsonSerializer.Deserialize<List<string>>(session.HeadersJson) ?? new List<string>();
        var allRows = JsonSerializer.Deserialize<List<List<string?>>>(session.RowsJson) ?? new List<List<string?>>();
        var preview = allRows.Take(PreviewRowLimit).Select(r => (IReadOnlyList<string?>)r).ToList();
        ImportMapping? mapping = null;
        if (!string.IsNullOrWhiteSpace(session.MappingJson))
            mapping = JsonSerializer.Deserialize<ImportMapping>(session.MappingJson);

        return Result<ImportSessionDto>.Success(new ImportSessionDto(
            session.Id,
            session.OriginalFileName,
            session.SourceFormat,
            session.FileSizeBytes,
            session.Status,
            headers,
            preview,
            allRows.Count,
            session.TargetEntity,
            session.PartnerContextId,
            session.CreatedAt,
            mapping));
    }
}
