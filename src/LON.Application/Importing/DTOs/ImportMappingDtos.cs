namespace LON.Application.Importing.DTOs;

/// <summary>
/// Canonical in-memory shape of an import mapping. Serialised to
/// <c>ImportSession.MappingJson</c> and <c>ImportMappingProfile.MappingJson</c>.
/// Columns map a source header to a target field name (the target entity
/// defines which field names are valid — P5.1.5 adds target schemas).
/// </summary>
public sealed record ImportMapping(List<ImportMappingColumn> Columns)
{
    public ImportMapping() : this(new List<ImportMappingColumn>()) { }
}

public sealed record ImportMappingColumn(
    string SourceHeader,
    string? TargetField,
    bool Ignore = false);

public sealed record ImportMappingProfileDto(
    Guid Id,
    string Label,
    string TargetEntity,
    Guid? PartnerContextId,
    ImportMapping Mapping,
    int UsageCount,
    DateTime? LastUsedAt,
    DateTime CreatedAt);
