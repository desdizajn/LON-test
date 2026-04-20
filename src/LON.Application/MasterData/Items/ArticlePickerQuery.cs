using LON.Application.Common.Interfaces;
using LON.Application.Common.Queries;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.MasterData.Items;

/// <summary>
/// P5.3.4 — Article picker with "A"-suffix variants.
///
/// Legacy ELON pattern: the same physical article (e.g. fabric lot 11005) can
/// be declared under different tariff codes depending on the customs scenario.
/// The alternate-tariff version is traditionally modelled as a separate Item
/// whose Code carries an "A" suffix (e.g. 11005A). When a user picks an
/// article on a customs declaration line, they need to see both the base and
/// the "A"-suffix sibling side-by-side so the tariff choice is deliberate.
///
/// This query takes a prefix search, finds matching items, and for each match
/// folds in its "A"-sibling (or its base if the match itself already carries
/// the A). Results come grouped by the normalised "base code" so the UI can
/// render one row per physical article and two variants beside each other.
/// </summary>
public sealed record ArticlePickerQuery(string? Query, int Limit = 20)
    : IQuery<IReadOnlyList<ArticlePickerGroup>>;

public sealed record ArticlePickerGroup(
    string BaseCode,
    IReadOnlyList<ArticlePickerVariant> Variants);

public sealed record ArticlePickerVariant(
    Guid Id,
    string Code,
    string Name,
    string? HSCode,
    string? CountryOfOrigin,
    bool IsASuffix);

public sealed class ArticlePickerQueryHandler
    : IQueryHandler<ArticlePickerQuery, IReadOnlyList<ArticlePickerGroup>>
{
    private readonly IApplicationDbContext _context;

    public ArticlePickerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<ArticlePickerGroup>> Handle(
        ArticlePickerQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 100);
        var q = (request.Query ?? string.Empty).Trim();

        // Pull the direct hits. LIKE prefix search keeps the result tight;
        // widening to `Contains` would fire N:N lookups against the A-sibling
        // set for no picker gain.
        var directQuery = _context.Items.Where(i => !i.IsDeleted);
        if (q.Length > 0)
        {
            directQuery = directQuery.Where(i =>
                EF.Functions.Like(i.Code, $"{q}%")
                || EF.Functions.Like(i.Name, $"%{q}%"));
        }

        var direct = await directQuery
            .OrderBy(i => i.Code)
            .Take(limit)
            .ToListAsync(ct);

        if (direct.Count == 0) return Array.Empty<ArticlePickerGroup>();

        // Figure out the "A-sibling" codes. For every hit, if its code ends in
        // 'A', the sibling is the prefix; otherwise the sibling is code + 'A'.
        // We then batch-fetch the siblings that aren't already in `direct`.
        var directIds = direct.Select(i => i.Id).ToHashSet();
        var siblingCodes = direct
            .Select(i => NormaliseBase(i.Code))
            .SelectMany(bc => new[] { bc, bc + "A" })
            .Distinct()
            .ToList();

        var siblings = await _context.Items
            .Where(i => !i.IsDeleted && siblingCodes.Contains(i.Code) && !directIds.Contains(i.Id))
            .ToListAsync(ct);

        // Group direct + siblings by normalised base. One row per physical
        // article, two variants alongside each other.
        var all = direct.Concat(siblings).ToList();
        var groups = all
            .GroupBy(i => NormaliseBase(i.Code))
            .OrderBy(g => g.Key)
            .Select(g => new ArticlePickerGroup(
                g.Key,
                g.OrderBy(i => i.Code.Length).ThenBy(i => i.Code)
                    .Select(i => new ArticlePickerVariant(
                        i.Id,
                        i.Code,
                        i.Name,
                        i.HSCode,
                        i.CountryOfOrigin,
                        IsASuffix: i.Code.Length > 0 && i.Code[^1] == 'A'))
                    .ToList()))
            .ToList();

        return groups;
    }

    /// <summary>
    /// Strip the trailing 'A' suffix so two codes that only differ by tariff
    /// variant share a grouping key. Codes without a trailing 'A' pass through.
    /// </summary>
    private static string NormaliseBase(string code)
    {
        if (string.IsNullOrEmpty(code)) return code;
        return code[^1] == 'A' ? code[..^1] : code;
    }
}
