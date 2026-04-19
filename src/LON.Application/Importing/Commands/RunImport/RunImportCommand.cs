using System.Text.Json;
using LON.Application.Common.Commands;
using LON.Application.Common.Importing;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Commands.RunImport;

/// <summary>
/// P5.1.6 — dry-run or commit the currently-staged <see cref="LON.Domain.Entities.Importing.ImportSession"/>.
///
/// Dry-run (<see cref="Mode"/> = false): runs the resolver + executor, then
/// rolls back. Returns the per-row report so the wizard can surface errors.
///
/// Commit (<see cref="Mode"/> = true): if the resolver produced zero errors,
/// the executor runs and a single <see cref="IApplicationDbContext.SaveChangesAsync"/>
/// persists every entity in one transaction. Any executor error aborts
/// without calling SaveChanges (atomic all-or-nothing).
/// </summary>
public record RunImportCommand(Guid SessionId, bool Commit) : ICommand<Result<ImportRunReport>>;

public class RunImportCommandHandler : ICommandHandler<RunImportCommand, Result<ImportRunReport>>
{
    private readonly IApplicationDbContext _context;
    private readonly IImportTargetRegistry _targets;
    private readonly IImportTargetExecutorRegistry _executors;

    public RunImportCommandHandler(
        IApplicationDbContext context,
        IImportTargetRegistry targets,
        IImportTargetExecutorRegistry executors)
    {
        _context = context;
        _targets = targets;
        _executors = executors;
    }

    public async Task<Result<ImportRunReport>> Handle(
        RunImportCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session is null)
            return Result<ImportRunReport>.Failure($"Import session '{request.SessionId}' not found.");
        if (string.IsNullOrWhiteSpace(session.TargetEntity))
            return Result<ImportRunReport>.Failure("Session has no target entity; apply a mapping first.");
        if (string.IsNullOrWhiteSpace(session.MappingJson))
            return Result<ImportRunReport>.Failure("Session has no mapping; apply a mapping first.");
        if (session.Status == ImportSessionStatus.Committed)
            return Result<ImportRunReport>.Failure("Session is already committed; start a new upload.");

        var schema = _targets.Find(session.TargetEntity)
            ?? throw new InvalidOperationException($"Unknown target '{session.TargetEntity}'.");

        var headers = JsonSerializer.Deserialize<List<string>>(session.HeadersJson) ?? new();
        var rows = JsonSerializer.Deserialize<List<List<string?>>>(session.RowsJson) ?? new();
        var mapping = JsonSerializer.Deserialize<ImportMapping>(session.MappingJson) ?? new();
        var defaults = string.IsNullOrWhiteSpace(session.DefaultsJson)
            ? new ImportDefaults()
            : JsonSerializer.Deserialize<ImportDefaults>(session.DefaultsJson) ?? new();
        var transforms = string.IsNullOrWhiteSpace(session.TransformsJson)
            ? new ImportTransforms()
            : JsonSerializer.Deserialize<ImportTransforms>(session.TransformsJson) ?? new();

        var resolver = new ImportRowResolver();
        var resolvedRows = await resolver.ResolveAsync(
            headers, rows.Select(r => (IReadOnlyList<string?>)r).ToList(),
            mapping, defaults, transforms, schema, _context, cancellationToken);

        var rowsWithErrors = resolvedRows.Count(r => !r.IsValid);
        var committable = rowsWithErrors == 0;

        if (!request.Commit || !committable)
        {
            return Result<ImportRunReport>.Success(new ImportRunReport(
                schema.TargetName,
                resolvedRows.Count,
                rowsWithErrors,
                resolvedRows.Take(200).ToList(),
                committable,
                WasCommitted: false,
                EntitiesCreated: 0));
        }

        // Commit path — executor populates the DbContext, then one atomic SaveChanges.
        var executor = _executors.Find(schema.TargetName);
        if (executor is null)
            return Result<ImportRunReport>.Failure($"No executor registered for target '{schema.TargetName}'.");

        var headerDefaults = BuildHeaderDefaults(resolvedRows, schema);
        var (ok, created, error) = await executor.ExecuteAsync(
            resolvedRows, headerDefaults, _context, cancellationToken);
        if (!ok)
            return Result<ImportRunReport>.Failure(error ?? "Import commit failed.");

        session.Status = ImportSessionStatus.Committed;
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ImportRunReport>.Success(new ImportRunReport(
            schema.TargetName,
            resolvedRows.Count,
            0,
            resolvedRows.Take(200).ToList(),
            Committable: true,
            WasCommitted: true,
            EntitiesCreated: created));
    }

    private static Dictionary<string, object?> BuildHeaderDefaults(
        List<ResolvedImportRow> rows, IImportTargetSchema schema)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (rows.Count == 0) return result;
        var first = rows[0];
        foreach (var field in schema.Fields.Where(f => f.Scope != ImportTargetFieldScope.Row))
        {
            if (first.Fields.TryGetValue(field.Name, out var v))
                result[field.Name] = v;
        }
        return result;
    }
}
