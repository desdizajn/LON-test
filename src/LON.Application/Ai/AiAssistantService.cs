using System.Text.Json;
using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Ai;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Ai;

/// <summary>
/// Phase 17 §E10 — orchestration façade. Dispatches the requested entity to
/// every registered <see cref="IRecommendationEngine"/> whose
/// <see cref="IRecommendationEngine.EntityType"/> matches, persists one
/// <see cref="AiSuggestionLog"/> row per surfaced recommendation, and
/// exposes the "acted / dismissed" feedback path.
/// </summary>
public sealed class AiAssistantService : IAiAssistantService
{
    private readonly IApplicationDbContext _context;
    private readonly IEnumerable<IRecommendationEngine> _engines;
    private readonly ICurrentUserService _currentUser;

    public AiAssistantService(
        IApplicationDbContext context,
        IEnumerable<IRecommendationEngine> engines,
        ICurrentUserService currentUser)
    {
        _context = context;
        _engines = engines;
        _currentUser = currentUser;
    }

    public async Task<List<Recommendation>> GetRecommendationsAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return new List<Recommendation>();
        if (entityId == Guid.Empty) return new List<Recommendation>();

        var tenantId = _context.CurrentTenantId
                       ?? _currentUser.TenantId
                       ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return new List<Recommendation>();

        var matched = _engines.Where(e => string.Equals(e.EntityType, entityType, StringComparison.Ordinal)).ToList();
        if (matched.Count == 0) return new List<Recommendation>();

        var collected = new List<Recommendation>();
        foreach (var engine in matched)
        {
            try
            {
                var recs = await engine.ProduceAsync(entityId, ct);
                collected.AddRange(recs);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Engines are independent; one engine's failure must not break
                // the whole panel. Swallow + continue. Failures show up as
                // empty recommendations + standard request logging upstream.
            }
        }

        // Persist a log row per surfaced recommendation; assign each its
        // server-issued Id so the UI can post back acted / dismissed.
        var now = DateTime.UtcNow;
        var persisted = new List<Recommendation>(collected.Count);
        foreach (var rec in collected)
        {
            var log = new AiSuggestionLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntityType = entityType,
                EntityId = entityId,
                RecommendationCode = rec.Code,
                RecommendationTitle = rec.Title,
                Severity = rec.Severity,
                StructuredDataJson = rec.StructuredData is null
                    ? null
                    : JsonSerializer.Serialize(rec.StructuredData),
                ActionLink = rec.ActionLink,
                GeneratedAt = now,
                UserActedOn = null,
                CreatedAt = now,
                CreatedBy = _currentUser.AuditName,
            };
            _context.AiSuggestionLogs.Add(log);
            persisted.Add(rec with { Id = log.Id });
        }

        if (persisted.Count > 0)
            await _context.SaveChangesAsync(ct);

        return persisted;
    }

    public async Task<bool> MarkActedAsync(Guid suggestionId, CancellationToken ct = default)
        => await SetActedFlagAsync(suggestionId, acted: true, ct);

    public async Task<bool> MarkDismissedAsync(Guid suggestionId, CancellationToken ct = default)
        => await SetActedFlagAsync(suggestionId, acted: false, ct);

    private async Task<bool> SetActedFlagAsync(Guid suggestionId, bool acted, CancellationToken ct)
    {
        var log = await _context.AiSuggestionLogs
            .FirstOrDefaultAsync(x => x.Id == suggestionId, ct);
        if (log is null) return false;
        log.UserActedOn = acted;
        log.UserActedAt = DateTime.UtcNow;
        log.UserActedBy = _currentUser.AuditName;
        log.ModifiedAt = DateTime.UtcNow;
        log.ModifiedBy = _currentUser.AuditName;
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
