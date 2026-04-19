using System.Text.Json;
using LON.Application.Common.Commands;
using LON.Application.Common.Importing;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using LON.Domain.Entities.Importing;
using LON.Domain.Enums;

namespace LON.Application.Importing.Commands.UploadImportFile;

/// <summary>
/// P5.1.1 — parse the uploaded file and persist an <see cref="ImportSession"/>.
/// Parser runs in-memory; full grid is stored as JSON so subsequent wizard
/// steps (mapping, transforms, dry-run, commit) don't require a re-upload.
/// </summary>
public record UploadImportFileCommand(
    byte[] FileBytes,
    string FileName,
    string? TargetEntity = null,
    Guid? PartnerContextId = null) : ICommand<Result<ImportSessionDto>>;

public class UploadImportFileCommandHandler
    : ICommandHandler<UploadImportFileCommand, Result<ImportSessionDto>>
{
    private const int PreviewRowLimit = 20;

    private readonly IApplicationDbContext _context;
    private readonly IImportFileParserRegistry _parsers;

    public UploadImportFileCommandHandler(
        IApplicationDbContext context,
        IImportFileParserRegistry parsers)
    {
        _context = context;
        _parsers = parsers;
    }

    public async Task<Result<ImportSessionDto>> Handle(
        UploadImportFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FileBytes.Length == 0)
            return Result<ImportSessionDto>.Failure("Uploaded file is empty.");
        if (string.IsNullOrWhiteSpace(request.FileName))
            return Result<ImportSessionDto>.Failure("File name is required.");

        ParsedImportFile parsed;
        try
        {
            using var ms = new MemoryStream(request.FileBytes, writable: false);
            parsed = _parsers.Parse(ms, request.FileName);
        }
        catch (NotSupportedException ex)
        {
            return Result<ImportSessionDto>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<ImportSessionDto>.Failure($"Failed to parse file: {ex.Message}");
        }

        if (parsed.Headers.Count == 0)
            return Result<ImportSessionDto>.Failure("No columns detected. First row must contain headers.");

        var session = new ImportSession
        {
            Id = Guid.NewGuid(),
            OriginalFileName = request.FileName,
            SourceFormat = parsed.Format,
            FileSizeBytes = request.FileBytes.Length,
            Status = ImportSessionStatus.Uploaded,
            HeadersJson = JsonSerializer.Serialize(parsed.Headers),
            RowsJson = JsonSerializer.Serialize(parsed.Rows),
            RowCount = parsed.Rows.Count,
            TargetEntity = request.TargetEntity,
            PartnerContextId = request.PartnerContextId
        };

        await _context.ImportSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var previewRows = parsed.Rows.Take(PreviewRowLimit).ToList();

        return Result<ImportSessionDto>.Success(new ImportSessionDto(
            session.Id,
            session.OriginalFileName,
            session.SourceFormat,
            session.FileSizeBytes,
            session.Status,
            parsed.Headers,
            previewRows,
            parsed.Rows.Count,
            session.TargetEntity,
            session.PartnerContextId,
            session.CreatedAt == default ? DateTime.UtcNow : session.CreatedAt));
    }
}
