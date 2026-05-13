using LON.Application.Common.Interfaces;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Ai.Engines;

/// <summary>
/// Phase 17 §E10 — Razdolzuvanje pre-flight: enumerate IM lines that have
/// not yet been flagged <c>RazdolzenaDaNe = true</c>. Returned alongside the
/// hub blocked-step recommendations for the same ClientOrder so the Manager
/// sees both nudges in one panel without switching pages.
/// </summary>
public sealed class RazdolzuvanjePreflightRecommendationEngine : IRecommendationEngine
{
    private readonly IApplicationDbContext _context;

    public RazdolzuvanjePreflightRecommendationEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public string EntityType => "ClientOrder";

    public async Task<List<Recommendation>> ProduceAsync(Guid clientOrderId, CancellationToken ct)
    {
        var result = new List<Recommendation>();

        var co = await _context.ClientOrders
            .FirstOrDefaultAsync(x => x.Id == clientOrderId && !x.IsDeleted, ct);
        if (co is null) return result;

        // Only meaningful while the order is open — Closed/Cancelled orders
        // have already been reconciled or torn down.
        if (co.Status == ClientOrderStatus.Cancelled || co.Status == ClientOrderStatus.Closed)
            return result;

        var lineStats = await (
            from line in _context.CustomsDeclarationLines
            join decl in _context.CustomsDeclarations on line.CustomsDeclarationId equals decl.Id
            where !line.IsDeleted
                  && decl.ClientOrderId == clientOrderId
                  && decl.DeclarationType == "IM"
                  && decl.Status == DeclarationStatus.Cleared
            select new { line.Id, line.RazdolzenaDaNe }
        ).ToListAsync(ct);

        if (lineStats.Count == 0)
            return result;

        var pending = lineStats.Count(l => !l.RazdolzenaDaNe);
        if (pending == 0)
            return result;

        result.Add(new Recommendation
        {
            Code = "razdolzuvanje.preflight.pending-lines",
            Title = $"Razdolzuvanje: {pending} IM линија/и без означување",
            Body = $"Има {pending} IM линија/и на овој налог кои не се означени како razdolzeno. Отвори ја Razdolzuvanje страната, означи ги завршените линии и зачувај snapshot.",
            Severity = pending > 0 ? "warning" : "info",
            ActionLink = "orders.actions.razdolzuvanje",
            ActionLabel = "Отвори Razdolzuvanje",
            StructuredData = new Dictionary<string, object?>
            {
                ["pendingLines"] = pending,
                ["totalLines"] = lineStats.Count,
            },
        });

        return result;
    }
}
