using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Audit;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LON.Application.Customs.Commands.BulkUpdateCustomsDeclarationLines;

/// <summary>
/// Bulk-update a single whitelisted field on every line of a Draft declaration.
///
/// Per BLUEPRINT §7.3.1 + AGENT-PROMPTS §E0 (rescoped 2026-05-12): the
/// frontend's <c>BulkFieldUpdateButton</c> component invokes this endpoint
/// to apply one value (e.g. CountryOfOrigin = "DE") across all lines.
/// One <c>AuditLogEntry</c> row is written per affected line so the bulk
/// action is fully reconstructible.
///
/// Field whitelist (enforced server-side; client cannot widen):
///   - <c>UoMId</c>        (Guid)
///   - <c>CountryOfOrigin</c> (string, ISO 2-char)
///   - <c>TariffCode</c>      (string, customs commodity code)
///
/// Parent-level <c>Currency</c> change is intentionally NOT here —
/// Currency lives on <see cref="Domain.Entities.Customs.CustomsDeclaration"/>
/// header, so use <c>UpdateCustomsDeclarationCommand</c> instead.
///
/// Pre-conditions:
///   - Declaration must exist and be in <c>Draft</c> status (mirrors the
///     guardrail in <c>UpdateCustomsDeclarationCommand</c>).
///   - Caller provides a non-empty <c>Reason</c> (free text; persisted in
///     <c>AuditLogEntry.ChangesJson</c>).
/// </summary>
public record BulkUpdateCustomsDeclarationLinesCommand : ICommand<Result<int>>
{
    /// <summary>Parent declaration id.</summary>
    public Guid DeclarationId { get; init; }

    /// <summary>Field name (case-sensitive, must match whitelist).</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Stringified new value. Server parses per <see cref="Field"/>.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Free-text reason for audit log.</summary>
    public string Reason { get; init; } = string.Empty;
}

public class BulkUpdateCustomsDeclarationLinesCommandHandler
    : ICommandHandler<BulkUpdateCustomsDeclarationLinesCommand, Result<int>>
{
    /// <summary>
    /// Whitelist of fields a caller may target. Anything else returns 400.
    /// </summary>
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal)
    {
        nameof(Domain.Entities.Customs.CustomsDeclarationLine.UoMId),
        nameof(Domain.Entities.Customs.CustomsDeclarationLine.CountryOfOrigin),
        nameof(Domain.Entities.Customs.CustomsDeclarationLine.TariffCode),
    };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public BulkUpdateCustomsDeclarationLinesCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(
        BulkUpdateCustomsDeclarationLinesCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<int>.Failure("Reason is required for bulk updates (audit trail).");

        if (!AllowedFields.Contains(request.Field))
            return Result<int>.Failure(
                $"Field '{request.Field}' is not in the bulk-update whitelist. " +
                $"Allowed: {string.Join(", ", AllowedFields)}.");

        var declaration = await _context.CustomsDeclarations
            .FirstOrDefaultAsync(d => d.Id == request.DeclarationId, cancellationToken);

        if (declaration is null)
            return Result<int>.Failure($"Declaration '{request.DeclarationId}' does not exist.");

        if (declaration.Status != DeclarationStatus.Draft)
        {
            return Result<int>.Failure(
                $"Declaration '{declaration.DeclarationNumber}' is in status '{declaration.Status}' " +
                "and cannot be bulk-edited. Bulk update only applies to Draft declarations.");
        }

        var lines = await _context.CustomsDeclarationLines
            .Where(l => l.CustomsDeclarationId == request.DeclarationId)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
            return Result<int>.Success(0);

        var audits = new List<AuditLogEntry>();
        var now = DateTime.UtcNow;
        var actor = _currentUser?.UserId ?? Guid.Empty;
        var actorName = _currentUser?.AuditName ?? "System";

        foreach (var line in lines)
        {
            // Snapshot old value for audit BEFORE mutating.
            string? oldValue = request.Field switch
            {
                nameof(Domain.Entities.Customs.CustomsDeclarationLine.UoMId) => line.UoMId.ToString(),
                nameof(Domain.Entities.Customs.CustomsDeclarationLine.CountryOfOrigin) => line.CountryOfOrigin,
                nameof(Domain.Entities.Customs.CustomsDeclarationLine.TariffCode) => line.TariffCode,
                _ => null, // unreachable; guarded above
            };

            // Apply the change.
            switch (request.Field)
            {
                case nameof(Domain.Entities.Customs.CustomsDeclarationLine.UoMId):
                    if (!Guid.TryParse(request.Value, out var uomId))
                        return Result<int>.Failure($"Value '{request.Value}' is not a valid Guid for UoMId.");
                    line.UoMId = uomId;
                    break;
                case nameof(Domain.Entities.Customs.CustomsDeclarationLine.CountryOfOrigin):
                    line.CountryOfOrigin = request.Value;
                    break;
                case nameof(Domain.Entities.Customs.CustomsDeclarationLine.TariffCode):
                    line.TariffCode = request.Value;
                    break;
            }

            audits.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                TenantId = line.TenantId,
                EntityType = nameof(Domain.Entities.Customs.CustomsDeclarationLine),
                EntityId = line.Id,
                Action = "BulkUpdate",
                ChangesJson = JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        field = request.Field,
                        old = oldValue,
                        @new = request.Value,
                        reason = request.Reason,
                    },
                }),
                UserId = actor == Guid.Empty ? null : actor,
                UserName = actorName,
                OccurredAt = now,
            });
        }

        _context.AuditLogEntries.AddRange(audits);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(lines.Count);
    }
}
