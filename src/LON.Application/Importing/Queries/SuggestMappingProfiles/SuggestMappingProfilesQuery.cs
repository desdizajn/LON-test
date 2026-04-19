using System.Text.Json;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Importing.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Importing.Queries.SuggestMappingProfiles;

/// <summary>
/// P5.1.2 — returns profiles matching (target entity, partner context), ordered
/// by <c>LastUsedAt</c> desc then <c>UsageCount</c> desc. The wizard picks
/// the top profile as the default; if there is exactly one match, zero-click
/// apply is possible.
/// </summary>
public record SuggestMappingProfilesQuery(
    string TargetEntity,
    Guid? PartnerContextId = null) : IRequest<Result<List<ImportMappingProfileDto>>>;

public class SuggestMappingProfilesQueryHandler
    : IRequestHandler<SuggestMappingProfilesQuery, Result<List<ImportMappingProfileDto>>>
{
    private readonly IApplicationDbContext _context;

    public SuggestMappingProfilesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ImportMappingProfileDto>>> Handle(
        SuggestMappingProfilesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetEntity))
            return Result<List<ImportMappingProfileDto>>.Failure("TargetEntity is required.");

        // Partner-scoped profiles are preferred; tenant-wide (PartnerContextId=null)
        // come after. Order by LastUsedAt then UsageCount so "just used" beats
        // "used many times years ago".
        var query = _context.ImportMappingProfiles
            .Where(p => p.TargetEntity == request.TargetEntity);
        if (request.PartnerContextId.HasValue)
            query = query.Where(p => p.PartnerContextId == request.PartnerContextId || p.PartnerContextId == null);
        else
            query = query.Where(p => p.PartnerContextId == null);

        var rows = await query
            .OrderByDescending(p => p.PartnerContextId != null) // partner-specific first
            .ThenByDescending(p => p.LastUsedAt)
            .ThenByDescending(p => p.UsageCount)
            .Take(50)
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(p => new ImportMappingProfileDto(
            p.Id,
            p.Label,
            p.TargetEntity,
            p.PartnerContextId,
            JsonSerializer.Deserialize<ImportMapping>(p.MappingJson) ?? new ImportMapping(),
            p.UsageCount,
            p.LastUsedAt,
            p.CreatedAt)).ToList();

        return Result<List<ImportMappingProfileDto>>.Success(dtos);
    }
}
