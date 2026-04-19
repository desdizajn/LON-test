namespace LON.Application.Common.Importing;

/// <summary>
/// Describes the shape of one target entity that the generic importer
/// supports (Receipts, Items, Partners, BOMs, CustomsDeclarations). The
/// wizard uses this to render field pickers and to enforce coverage rules
/// at commit time (every non-optional field must come from either a mapped
/// column, a header-level default, or a derived-at-commit computation).
/// </summary>
public interface IImportTargetSchema
{
    string TargetName { get; }
    string DisplayLabel { get; }
    IReadOnlyList<ImportTargetField> Fields { get; }
}

public enum ImportTargetFieldType
{
    String = 1,
    Decimal = 2,
    Integer = 3,
    Boolean = 4,
    Date = 5,
    DateTime = 6,
    Guid = 7,
    Enum = 8
}

/// <summary>
/// Where the value comes from during commit:
///   Row     — one cell per row (mapped column).
///   Header  — one value for all rows (header-level default).
///   Either  — wizard may use a row column OR a default.
/// </summary>
public enum ImportTargetFieldScope
{
    Row = 1,
    Header = 2,
    Either = 3
}

public sealed record ImportTargetField(
    string Name,
    string Label,
    ImportTargetFieldType Type,
    bool Required,
    ImportTargetFieldScope Scope = ImportTargetFieldScope.Either,
    IReadOnlyList<string>? EnumValues = null,
    string? LookupEntity = null,
    string? LookupField = null,
    string? Notes = null);

public interface IImportTargetRegistry
{
    IReadOnlyList<IImportTargetSchema> All { get; }
    IImportTargetSchema? Find(string targetName);
}
