namespace LON.Application.Common.Importing;

/// <summary>
/// One row after mapping + defaults + transforms + LOOKUP resolution +
/// type coercion. Fields carry the runtime-typed value (string, decimal,
/// int, bool, DateTime, Guid) suitable for the target executor to hand
/// straight into an entity constructor or a MediatR command.
/// </summary>
public sealed class ResolvedImportRow
{
    public int RowIndex { get; init; }
    public Dictionary<string, object?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public T? GetOrDefault<T>(string name)
    {
        if (!Fields.TryGetValue(name, out var raw) || raw is null) return default;
        if (raw is T typed) return typed;
        try { return (T)Convert.ChangeType(raw, typeof(T)); }
        catch { return default; }
    }
}

/// <summary>
/// Aggregate dry-run / commit result shared between /run?mode=dry and /run?mode=commit.
/// </summary>
public sealed record ImportRunReport(
    string TargetEntity,
    int RowCount,
    int RowsWithErrors,
    List<ResolvedImportRow> Rows,
    bool Committable,
    bool WasCommitted,
    int EntitiesCreated);
