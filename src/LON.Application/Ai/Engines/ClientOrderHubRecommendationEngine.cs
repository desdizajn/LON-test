using LON.Application.Common.Interfaces;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Ai.Engines;

/// <summary>
/// Phase 17 §E10 — "detect blocked next step" recommendations for an open
/// ClientOrder. The engine walks the hub state machine top-down and emits
/// the FIRST applicable nudge plus the Razdolzuvanje pre-flight check when
/// it's nearing closure.
///
/// All checks are structured DB queries — no LLM calls. Confidence is 1.0
/// (deterministic). The free-form Q&A surface (which DOES call OpenAI) is
/// the other tab of the helper drawer.
/// </summary>
public sealed class ClientOrderHubRecommendationEngine : IRecommendationEngine
{
    private readonly IApplicationDbContext _context;

    public ClientOrderHubRecommendationEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public string EntityType => "ClientOrder";

    public async Task<List<Recommendation>> ProduceAsync(Guid clientOrderId, CancellationToken ct)
    {
        var result = new List<Recommendation>();

        var co = await _context.ClientOrders
            .FirstOrDefaultAsync(x => x.Id == clientOrderId, ct);
        if (co is null || co.IsDeleted)
            return result;

        // Cancelled / Closed orders have nothing actionable left.
        if (co.Status == ClientOrderStatus.Cancelled || co.Status == ClientOrderStatus.Closed)
            return result;

        var fgCount = await _context.ClientOrderFinishedGoods
            .CountAsync(fg => fg.ClientOrderId == clientOrderId && !fg.IsDeleted, ct);

        var declarations = await _context.CustomsDeclarations
            .Where(d => d.ClientOrderId == clientOrderId && !d.IsDeleted)
            .Select(d => new { d.Id, d.DeclarationType, d.Status })
            .ToListAsync(ct);

        var imDecls = declarations.Where(d => d.DeclarationType == "IM").ToList();
        var imCleared = imDecls.Count(d => d.Status == DeclarationStatus.Cleared);
        var exDecls = declarations.Where(d => d.DeclarationType == "EX").ToList();

        // (a) Draft order with no finished-goods picked yet → suggest BOM.
        if (co.Status == ClientOrderStatus.Draft && fgCount == 0)
        {
            result.Add(new Recommendation
            {
                Code = "hub.draft.no-fgs",
                Title = "Внеси готови производи (BOM)",
                Body = "Налогот е во Draft без избрани готови производи. Започни со BOM акцијата за да го дефинираш составот.",
                Severity = "info",
                ActionLink = "orders.actions.bom",
                ActionLabel = "Креирај BOM",
            });
            return result;
        }

        // (b) Order is Active / Producing but no IM declarations exist yet.
        if (co.Status >= ClientOrderStatus.Active && imDecls.Count == 0)
        {
            result.Add(new Recommendation
            {
                Code = "hub.active.no-im",
                Title = "Креирај увозна декларација",
                Body = "Не постои IM декларација сврзана со овој налог. Креирај IM за да започне приемот.",
                Severity = "warning",
                ActionLink = "orders.actions.imDeclaration",
                ActionLabel = "Креирај IM",
            });
        }

        // (c) IM cleared but inventory not yet received.
        if (imCleared > 0)
        {
            var hasReceiptLink = await _context.ReceiptLines
                .Where(rl => !rl.IsDeleted && rl.CustomsDeclarationId != null)
                .Join(_context.CustomsDeclarations,
                    rl => rl.CustomsDeclarationId,
                    d => d.Id,
                    (rl, d) => new { d.ClientOrderId })
                .AnyAsync(x => x.ClientOrderId == clientOrderId, ct);
            if (!hasReceiptLink)
            {
                result.Add(new Recommendation
                {
                    Code = "hub.cleared.no-receipt",
                    Title = "Прими стока во магацин",
                    Body = "IM декларацијата е заверена но нема прием. Изврши Bulk receipt од декларацијата.",
                    Severity = "warning",
                    ActionLink = "orders.actions.receive",
                    ActionLabel = "Прими",
                });
            }
        }

        // (d) Inventory in hand but no producer assigned → suggest Podelba.
        var inventory = await (
            from b in _context.InventoryBalances
            join d in _context.CustomsDeclarations on b.MRN equals d.MRN
            where !b.IsDeleted
                  && d.ClientOrderId == clientOrderId
                  && !d.IsDeleted
            select new { b.AssignedProducerId, b.Quantity }
        ).ToListAsync(ct);
        if (inventory.Count > 0)
        {
            var unassigned = inventory.Where(x => x.AssignedProducerId == null && x.Quantity > 0).ToList();
            if (unassigned.Count > 0)
            {
                result.Add(new Recommendation
                {
                    Code = "hub.inventory.no-producer",
                    Title = "Распредели до подизведувач (Podelba)",
                    Body = $"{unassigned.Count} баланс(а) на залиха без означен подизведувач. Изврши Podelba за да продолжиш кон производство.",
                    Severity = "info",
                    ActionLink = "orders.actions.podelba",
                    ActionLabel = "Podelba",
                    StructuredData = new Dictionary<string, object?>
                    {
                        ["unassignedCount"] = unassigned.Count,
                    },
                });
            }
        }

        // (e) Production has materials issued but no EX declaration yet.
        var hasMaterialIssue = await (
            from mi in _context.MaterialIssues
            join po in _context.ProductionOrders on mi.ProductionOrderId equals po.Id
            where !mi.IsDeleted && po.ClientOrderId == clientOrderId
            select mi.Id
        ).AnyAsync(ct);
        if (hasMaterialIssue && exDecls.Count == 0)
        {
            result.Add(new Recommendation
            {
                Code = "hub.production.no-ex",
                Title = "Креирај извозна декларација",
                Body = "Материјалите се издадени но нема EX. Креирај EX + Shipment за да го затвориш кругот.",
                Severity = "info",
                ActionLink = "orders.actions.exDeclaration",
                ActionLabel = "Креирај EX",
            });
        }

        return result;
    }
}
