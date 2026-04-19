using System.Text.Json;
using LON.Application.Common.Commands;
using LON.Application.Common.Importing;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using LON.Domain.Entities.Importing;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Commands.ApplyImportMapping;

/// <summary>
/// P5.1.2 — attach a column mapping to an uploaded session. Optionally saves
/// the same mapping as a reusable profile keyed on (tenant, target entity,
/// partner context, label). When <see cref="SaveAsProfileLabel"/> is set,
/// a new <see cref="ImportMappingProfile"/> is created or updated in place
/// if (target + partner + label) already exists.
/// </summary>
public record ApplyImportMappingCommand(
    Guid SessionId,
    ImportMapping Mapping,
    string TargetEntity,
    Guid? PartnerContextId = null,
    string? SaveAsProfileLabel = null) : ICommand<Result<Guid>>;

public class ApplyImportMappingCommandHandler
    : ICommandHandler<ApplyImportMappingCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IImportTargetRegistry _targets;

    public ApplyImportMappingCommandHandler(
        IApplicationDbContext context,
        IImportTargetRegistry targets)
    {
        _context = context;
        _targets = targets;
    }

    public async Task<Result<Guid>> Handle(
        ApplyImportMappingCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetEntity))
            return Result<Guid>.Failure("TargetEntity is required.");
        if (request.Mapping.Columns.Count == 0)
            return Result<Guid>.Failure("Mapping must include at least one column.");

        // P5.1.5 — target must be one of the registered schemas.
        var targetSchema = _targets.Find(request.TargetEntity);
        if (targetSchema is null)
            return Result<Guid>.Failure(
                $"Unknown target '{request.TargetEntity}'. Available: {string.Join(", ", _targets.All.Select(t => t.TargetName))}.");
        var validFieldNames = new HashSet<string>(
            targetSchema.Fields.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);
        if (session is null)
            return Result<Guid>.Failure($"Import session '{request.SessionId}' not found.");

        // Validate mapping against the uploaded headers AND the target schema —
        // every non-ignored column must reference a real header and a real
        // target field.
        var headers = JsonSerializer.Deserialize<List<string>>(session.HeadersJson) ?? new List<string>();
        var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        foreach (var col in request.Mapping.Columns)
        {
            if (!headerSet.Contains(col.SourceHeader))
                return Result<Guid>.Failure($"Mapping references unknown source column '{col.SourceHeader}'.");
            if (col.Ignore) continue;
            if (string.IsNullOrWhiteSpace(col.TargetField))
                return Result<Guid>.Failure($"Column '{col.SourceHeader}' is neither ignored nor mapped to a target field.");
            if (!validFieldNames.Contains(col.TargetField))
                return Result<Guid>.Failure(
                    $"Column '{col.SourceHeader}' maps to '{col.TargetField}', which is not a valid field on target '{targetSchema.TargetName}'.");
        }

        session.MappingJson = JsonSerializer.Serialize(request.Mapping);
        session.TargetEntity = request.TargetEntity;
        session.PartnerContextId = request.PartnerContextId;
        session.Status = ImportSessionStatus.Mapped;

        Guid profileId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(request.SaveAsProfileLabel))
        {
            var label = request.SaveAsProfileLabel.Trim();
            // Upsert behaviour — the unique index enforces one profile per
            // (tenant, target, partner, label); resolving by the same tuple
            // here lets the wizard do a "save" that doesn't throw on reuse.
            var existing = await _context.ImportMappingProfiles.FirstOrDefaultAsync(
                p => p.TargetEntity == request.TargetEntity
                     && p.PartnerContextId == request.PartnerContextId
                     && p.Label == label,
                cancellationToken);
            if (existing is null)
            {
                var profile = new ImportMappingProfile
                {
                    Id = Guid.NewGuid(),
                    Label = label,
                    TargetEntity = request.TargetEntity,
                    PartnerContextId = request.PartnerContextId,
                    MappingJson = session.MappingJson,
                    UsageCount = 1,
                    LastUsedAt = DateTime.UtcNow
                };
                await _context.ImportMappingProfiles.AddAsync(profile, cancellationToken);
                profileId = profile.Id;
            }
            else
            {
                existing.MappingJson = session.MappingJson;
                existing.UsageCount += 1;
                existing.LastUsedAt = DateTime.UtcNow;
                profileId = existing.Id;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(profileId == Guid.Empty ? session.Id : profileId);
    }
}
