using LON.Application.Common.Commands;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.MasterData;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.UserPrefs;

/// <summary>
/// P5.3.5 — recent-values cache for the current user. One query + one command.
///
/// - <see cref="GetUserFieldHistoryQuery"/>: top N (FieldKey, Value) rows
///   ordered by LastUsedAt desc. UI wires this to a datalist so repeat
///   entry becomes zero-keystroke.
/// - <see cref="RecordUserFieldValueCommand"/>: upsert (UserId, FieldKey,
///   Value). If the triple exists, bumps LastUsedAt + UsageCount. Otherwise
///   inserts new row and prunes oldest rows when the tail exceeds
///   <see cref="MaxRowsPerFieldKey"/> so the table doesn't grow unbounded.
/// </summary>

public sealed record GetUserFieldHistoryQuery(string FieldKey, int Limit = 10)
    : IRequest<Result<IReadOnlyList<UserFieldHistoryDto>>>;

public sealed record UserFieldHistoryDto(string Value, DateTime LastUsedAt, int UsageCount);

public sealed record RecordUserFieldValueCommand(string FieldKey, string Value)
    : ICommand<Result<bool>>;

public class GetUserFieldHistoryQueryHandler
    : IRequestHandler<GetUserFieldHistoryQuery, Result<IReadOnlyList<UserFieldHistoryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserFieldHistoryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserFieldHistoryDto>>> Handle(
        GetUserFieldHistoryQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FieldKey))
            return Result<IReadOnlyList<UserFieldHistoryDto>>.Failure("FieldKey is required.");

        if (!_currentUser.UserId.HasValue)
            return Result<IReadOnlyList<UserFieldHistoryDto>>.Success(Array.Empty<UserFieldHistoryDto>());

        var limit = Math.Clamp(request.Limit, 1, 50);
        var key = request.FieldKey.Trim();

        var rows = await _context.UserFieldHistories
            .Where(h => h.UserId == _currentUser.UserId.Value && h.FieldKey == key)
            .OrderByDescending(h => h.LastUsedAt)
            .Take(limit)
            .Select(h => new UserFieldHistoryDto(h.Value, h.LastUsedAt, h.UsageCount))
            .ToListAsync(ct);

        return Result<IReadOnlyList<UserFieldHistoryDto>>.Success(rows);
    }
}

public class RecordUserFieldValueCommandHandler
    : ICommandHandler<RecordUserFieldValueCommand, Result<bool>>
{
    /// <summary>Keep most-recent N rows per (User, FieldKey); prune older ones on upsert.</summary>
    public const int MaxRowsPerFieldKey = 50;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RecordUserFieldValueCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(RecordUserFieldValueCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FieldKey))
            return Result<bool>.Failure("FieldKey is required.");
        if (string.IsNullOrWhiteSpace(request.Value))
            return Result<bool>.Failure("Value is required.");
        if (!_currentUser.UserId.HasValue || !_currentUser.TenantId.HasValue)
            return Result<bool>.Failure("No authenticated user.");

        var key = request.FieldKey.Trim();
        if (key.Length > 128)
            return Result<bool>.Failure("FieldKey max length is 128.");
        var value = request.Value.Trim();
        if (value.Length > 512)
            value = value.Substring(0, 512);

        var userId = _currentUser.UserId.Value;
        var tenantId = _currentUser.TenantId.Value;
        var now = DateTime.UtcNow;

        var existing = await _context.UserFieldHistories
            .FirstOrDefaultAsync(h =>
                h.UserId == userId && h.FieldKey == key && h.Value == value,
                ct);

        if (existing is not null)
        {
            existing.LastUsedAt = now;
            existing.UsageCount += 1;
        }
        else
        {
            _context.UserFieldHistories.Add(new UserFieldHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                FieldKey = key,
                Value = value,
                LastUsedAt = now,
                UsageCount = 1,
                CreatedAt = now,
                CreatedBy = _currentUser.Username ?? "system"
            });

            // Opportunistic prune — keep only the top N most-recent for this key.
            var excess = await _context.UserFieldHistories
                .Where(h => h.UserId == userId && h.FieldKey == key)
                .OrderByDescending(h => h.LastUsedAt)
                .Skip(MaxRowsPerFieldKey - 1) // -1 because the new row isn't persisted yet
                .ToListAsync(ct);
            foreach (var old in excess)
                old.IsDeleted = true;
        }

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
