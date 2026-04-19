using LON.Application.Common.Interfaces;

namespace LON.Application.Common.Importing.Executors;

/// <summary>
/// Placeholder executor for a target whose commit pipeline isn't wired up
/// yet. Dry-run still exercises the resolver + validator against this
/// target's schema; only the final save fails with a clear "not implemented"
/// message so nothing silently gets created.
/// </summary>
public class BOMsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "BOMs";
    public Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
        => Task.FromResult<(bool, int, string?)>(
            (false, 0, "BOMs import commit is not yet implemented; dry-run is supported."));
}

public class CustomsDeclarationsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "CustomsDeclarations";
    public Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
        => Task.FromResult<(bool, int, string?)>(
            (false, 0, "CustomsDeclarations import commit is not yet implemented; dry-run is supported."));
}

public class ImportTargetExecutorRegistry : IImportTargetExecutorRegistry
{
    private readonly IReadOnlyDictionary<string, IImportTargetExecutor> _byName;

    public ImportTargetExecutorRegistry(IEnumerable<IImportTargetExecutor> executors)
    {
        _byName = executors.ToDictionary(e => e.TargetName, StringComparer.OrdinalIgnoreCase);
    }

    public IImportTargetExecutor? Find(string targetName)
        => string.IsNullOrWhiteSpace(targetName) ? null
           : _byName.TryGetValue(targetName, out var e) ? e : null;
}
