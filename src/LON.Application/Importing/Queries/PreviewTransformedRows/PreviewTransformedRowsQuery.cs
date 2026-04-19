using System.Text.Json;
using LON.Application.Common.Importing;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Queries.PreviewTransformedRows;

/// <summary>
/// P5.1.4 — returns the first N rows with every in-memory transform applied.
/// The wizard calls this after the user picks transforms so they can see
/// the effect before committing. LOOKUP rules are skipped (need DB — they
/// run in P5.1.6 commit).
/// </summary>
public record PreviewTransformedRowsQuery(Guid SessionId, int Take = 20)
    : IRequest<Result<PreviewTransformedRowsResult>>;

public sealed record PreviewTransformedRowsResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

public class PreviewTransformedRowsQueryHandler
    : IRequestHandler<PreviewTransformedRowsQuery, Result<PreviewTransformedRowsResult>>
{
    private readonly IApplicationDbContext _context;

    public PreviewTransformedRowsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PreviewTransformedRowsResult>> Handle(
        PreviewTransformedRowsQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session is null)
            return Result<PreviewTransformedRowsResult>.Failure($"Import session '{request.SessionId}' not found.");

        var headers = JsonSerializer.Deserialize<List<string>>(session.HeadersJson) ?? new List<string>();
        var rows = JsonSerializer.Deserialize<List<List<string?>>>(session.RowsJson) ?? new List<List<string?>>();
        var transforms = string.IsNullOrWhiteSpace(session.TransformsJson)
            ? new ImportTransforms()
            : JsonSerializer.Deserialize<ImportTransforms>(session.TransformsJson) ?? new ImportTransforms();

        // header name → rules (case-insensitive lookup), pre-resolved to the
        // column index so we don't re-walk the list for every cell.
        var rulesByIndex = new Dictionary<int, List<string>>();
        foreach (var col in transforms.Columns)
        {
            var idx = headers.FindIndex(h => string.Equals(h, col.SourceHeader, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) rulesByIndex[idx] = col.Rules;
        }

        var take = Math.Clamp(request.Take, 1, 200);
        var sliced = rows.Take(take).ToList();
        var transformed = new List<IReadOnlyList<string?>>(sliced.Count);
        foreach (var row in sliced)
        {
            var outRow = new string?[Math.Max(row.Count, headers.Count)];
            for (int i = 0; i < outRow.Length; i++)
            {
                var cell = i < row.Count ? row[i] : null;
                if (rulesByIndex.TryGetValue(i, out var rules))
                    cell = ImportTransformRunner.Apply(cell, rules);
                outRow[i] = cell;
            }
            transformed.Add(outRow);
        }

        return Result<PreviewTransformedRowsResult>.Success(
            new PreviewTransformedRowsResult(headers, transformed));
    }
}
