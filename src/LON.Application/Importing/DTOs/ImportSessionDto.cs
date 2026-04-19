using LON.Domain.Enums;

namespace LON.Application.Importing.DTOs;

/// <summary>
/// Serialised view of an <see cref="LON.Domain.Entities.Importing.ImportSession"/>
/// returned to the wizard UI. <see cref="PreviewRows"/> is capped at 20 rows to
/// keep the payload small; <see cref="TotalRowCount"/> surfaces the full count
/// so the UI can warn before a dry-run.
/// </summary>
public sealed record ImportSessionDto(
    Guid Id,
    string OriginalFileName,
    ImportSourceFormat Format,
    long FileSizeBytes,
    ImportSessionStatus Status,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> PreviewRows,
    int TotalRowCount,
    string? TargetEntity,
    Guid? PartnerContextId,
    DateTime CreatedAt,
    ImportMapping? Mapping = null);
