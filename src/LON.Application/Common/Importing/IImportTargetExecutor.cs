using LON.Application.Common.Interfaces;

namespace LON.Application.Common.Importing;

/// <summary>
/// Per-target commit logic. Given the already-resolved rows and the
/// header-level defaults, create the target entities on the shared
/// <see cref="IApplicationDbContext"/>. The pipeline calls
/// <c>SaveChangesAsync</c> exactly once AFTER all executors finish so a
/// failed row short-circuits the whole import (all-or-nothing).
///
/// Return the number of domain entities created (for the run report).
/// Per-row errors go into the row's <see cref="ResolvedImportRow.Errors"/>
/// list and the executor returns a Failure result; the pipeline then rolls
/// back and returns the report to the caller.
/// </summary>
public interface IImportTargetExecutor
{
    string TargetName { get; }

    Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken);
}

public interface IImportTargetExecutorRegistry
{
    IImportTargetExecutor? Find(string targetName);
}
