using System.Text;
using LON.Application.Customs.ClientOrders;
using LON.Application.Customs.Queries.GeneratePee060Xml;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E1 — ClientOrder CRUD endpoints. Hub UI (§E2) consumes these.
/// </summary>
public class ClientOrdersController : BaseController
{
    private readonly ApplicationDbContext _context;

    public ClientOrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? status,
        [FromQuery] Guid? customerPartnerId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool includeCancelled = false)
    {
        var result = await Mediator.Send(new GetClientOrdersQuery
        {
            Status = status,
            CustomerPartnerId = customerPartnerId,
            FromDate = fromDate,
            ToDate = toDate,
            IncludeCancelled = includeCancelled,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetClientOrderByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientOrderCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientOrderCommand command)
    {
        if (command.Id != Guid.Empty && command.Id != id)
            return BadRequest(new { errorMessage = "Route id and body id do not match." });
        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelClientOrderCommand command)
    {
        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E5 — add a <see cref="ClientOrderFinishedGood"/> row to an
    /// existing ClientOrder. Used by the hub's „Внеси готови производи (BOM)" action.
    /// </summary>
    [HttpPost("{id:guid}/finished-goods")]
    public async Task<IActionResult> AddFinishedGood(Guid id, [FromBody] AddClientOrderFinishedGoodCommand command)
    {
        var effective = command with { ClientOrderId = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E8 — list of FGs declared on this ClientOrder, joined to the
    /// current InventoryBalance positive-qty rows at any OK / Quarantine
    /// location. Powers the EX-declaration wizard's FG picker so the user
    /// only sees what's actually shippable.
    ///
    /// One row per (FG item, batch, MRN, location) inventory bucket — the
    /// wizard collapses by item when computing default shipment qty.
    /// </summary>
    [HttpGet("{id:guid}/available-fgs")]
    public async Task<IActionResult> GetAvailableFinishedGoods(Guid id)
    {
        var fgItemIds = await _context.ClientOrderFinishedGoods
            .Where(g => g.ClientOrderId == id)
            .Select(g => g.ItemId)
            .Distinct()
            .ToListAsync();
        if (fgItemIds.Count == 0)
            return Ok(Array.Empty<object>());

        var balances = await _context.InventoryBalances
            .Include(b => b.Item)
            .Include(b => b.Location).ThenInclude(l => l.Warehouse)
            .Include(b => b.UoM)
            .Where(b => fgItemIds.Contains(b.ItemId)
                        && b.Quantity > 0m
                        && b.QualityStatus != LON.Domain.Enums.QualityStatus.Blocked)
            .ToListAsync();

        var rows = balances.Select(b => new
        {
            balanceId = b.Id,
            itemId = b.ItemId,
            itemCode = b.Item.Code,
            itemName = b.Item.Name,
            batchNumber = b.BatchNumber,
            mrn = b.MRN,
            quantity = b.Quantity,
            qualityStatus = (int)b.QualityStatus,
            uoMId = b.UoMId,
            uoMCode = b.UoM != null ? b.UoM.Code : null,
            locationId = b.LocationId,
            locationCode = b.Location != null ? b.Location.Code : null,
            warehouseCode = b.Location != null && b.Location.Warehouse != null ? b.Location.Warehouse.Code : null,
        }).ToList();

        return Ok(rows);
    }

    // ───── Phase 17 cutover gap — hub aggregates across child POs/MIs ─────

    /// <summary>
    /// All MaterialIssues for any ProductionOrder linked to this ClientOrder.
    /// Used by the hub Issues tab; surfaces the audit trail of which
    /// materials moved out + when + by whom. For migrated COs (where
    /// LON.Migration aggregates LagerMaterijali → MaterialIssue) this is the
    /// canonical view of historical material flow.
    /// </summary>
    [HttpGet("{id:guid}/material-issues")]
    public async Task<IActionResult> GetMaterialIssues(Guid id)
    {
        // Bypass the global soft-delete filter so MaterialIssues whose Item
        // master is archived still render (same reason as InventoryBalance).
        var rows = await _context.MaterialIssues
            .IgnoreQueryFilters()
            .Where(m => !m.IsDeleted)
            .Where(m => _context.ProductionOrders.Any(po => po.Id == m.ProductionOrderId
                                                            && po.ClientOrderId == id))
            .Select(m => new
            {
                m.Id,
                m.IssueNumber,
                m.IssueDate,
                m.ProductionOrderId,
                ProductionOrderNumber = _context.ProductionOrders
                    .Where(po => po.Id == m.ProductionOrderId)
                    .Select(po => po.OrderNumber)
                    .FirstOrDefault(),
                m.ItemId,
                ItemCode = _context.Items.IgnoreQueryFilters()
                    .Where(i => i.Id == m.ItemId).Select(i => i.Code).FirstOrDefault(),
                ItemName = _context.Items.IgnoreQueryFilters()
                    .Where(i => i.Id == m.ItemId).Select(i => i.Name).FirstOrDefault(),
                m.Quantity,
                UoMCode = _context.UnitsOfMeasure
                    .Where(u => u.Id == m.UoMId).Select(u => u.Code).FirstOrDefault(),
                m.BatchNumber,
                m.MRN,
                DeliveryNoteNumber = _context.DeliveryNotes
                    .Where(dn => dn.RelatedDocumentId == m.Id)
                    .Select(dn => dn.Number)
                    .FirstOrDefault(),
            })
            .OrderByDescending(m => m.IssueDate)
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>
    /// BOM details for any ProductionOrder linked to this ClientOrder.
    /// One element per PO that has a BOM; the element's `lines` is the
    /// material composition. Surfaces an otherwise invisible part of the
    /// migrated CO state on the hub.
    /// </summary>
    [HttpGet("{id:guid}/boms")]
    public async Task<IActionResult> GetBoms(Guid id)
    {
        // Same archived-item resilience as the issues + inventory endpoints.
        var pos = await _context.ProductionOrders
            .IgnoreQueryFilters()
            .Where(po => !po.IsDeleted && po.ClientOrderId == id && po.BOMId != null)
            .Select(po => new { po.Id, po.OrderNumber, po.BOMId })
            .ToListAsync();
        var rows = new List<object>();
        foreach (var po in pos)
        {
            var bomId = po.BOMId!.Value;
            var bom = await _context.BOMs.IgnoreQueryFilters()
                .Where(b => b.Id == bomId)
                .Select(b => new { b.Id, BomCode = b.Code, b.Version, b.BaseQuantity })
                .FirstOrDefaultAsync();
            if (bom is null) continue;
            var lines = await _context.BOMLines.IgnoreQueryFilters()
                .Where(l => l.BOMId == bomId && !l.IsDeleted)
                .Select(l => new
                {
                    l.Id,
                    l.LineNumber,
                    l.ItemId,
                    ItemCode = _context.Items.IgnoreQueryFilters()
                        .Where(i => i.Id == l.ItemId).Select(i => i.Code).FirstOrDefault(),
                    ItemName = _context.Items.IgnoreQueryFilters()
                        .Where(i => i.Id == l.ItemId).Select(i => i.Name).FirstOrDefault(),
                    l.Quantity,
                    UoMCode = _context.UnitsOfMeasure
                        .Where(u => u.Id == l.UoMId).Select(u => u.Code).FirstOrDefault(),
                    l.ScrapPercentage,
                })
                .OrderBy(l => l.LineNumber)
                .ToListAsync();
            rows.Add(new
            {
                productionOrderId = po.Id,
                productionOrderNumber = po.OrderNumber,
                bomId = bom.Id,
                bomCode = bom.BomCode,
                bomVersion = bom.Version,
                bomBaseQuantity = bom.BaseQuantity,
                lines,
            });
        }
        return Ok(rows);
    }

    // ───────── Phase 17 §E9 — Razdolzuvanje view per ClientOrder ─────────

    /// <summary>
    /// Phase 17 §E9 — full razdolzuvanje aggregate for a single ClientOrder:
    /// IM duty charged vs. EX + Waste + Return duty credited, side-by-side
    /// columns + per-line breakdown (each IM declaration line with its
    /// <c>RazdolzenaDaNe</c> flag), variance row + reconciliation status.
    /// </summary>
    [HttpGet("{id:guid}/razdolzuvanje")]
    public async Task<IActionResult> GetRazdolzuvanje(Guid id)
    {
        var report = await Mediator.Send(new GetRazdolzuvanjeForClientOrderQuery(id));
        return Ok(report);
    }

    /// <summary>
    /// Phase 17 §E9 — flip <c>RazdolzenaDaNe</c> on a single
    /// CustomsDeclarationLine that belongs to this order's IM declarations.
    /// </summary>
    [HttpPost("{id:guid}/razdolzuvanje/mark-line")]
    public async Task<IActionResult> MarkRazdolzuvanjeLine(Guid id, [FromBody] MarkLineBody body)
    {
        if (body is null || body.LineId == Guid.Empty)
            return BadRequest(new { errorMessage = "lineId is required." });
        var result = await Mediator.Send(
            new MarkLineRazdolzenaCommand(id, body.LineId, body.RazdolzenaDaNe));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E9 — take a GuaranteeBalanceSnapshot AND, when the order is
    /// reconciled + every line carries <c>RazdolzenaDaNe</c>, transition
    /// <see cref="LON.Domain.Enums.ClientOrderStatus"/> → Closed.
    /// </summary>
    [HttpPost("{id:guid}/razdolzuvanje/snapshot")]
    public async Task<IActionResult> TakeRazdolzuvanjeSnapshot(Guid id, [FromBody] SnapshotBody? body)
    {
        var result = await Mediator.Send(new TakeRazdolzuvanjeSnapshotCommand
        {
            ClientOrderId = id,
            SnapshotDate = body?.SnapshotDate,
            Notes = body?.Notes,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E9 — print-friendly HTML view of the razdolzuvanje report
    /// (same pattern as DeliveryNote / CommercialInvoice PDFs). The browser
    /// prints to PDF; QuestPDF migration is post-v1 polish.
    /// </summary>
    [HttpGet("{id:guid}/razdolzuvanje/pdf")]
    public async Task<IActionResult> GetRazdolzuvanjePdf(Guid id)
    {
        var report = await Mediator.Send(new GetRazdolzuvanjeForClientOrderQuery(id));

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"mk\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Razdolzuvanje {Esc(report.OrderNumber)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font:14px/1.4 system-ui,Segoe UI,sans-serif;padding:24px;color:#111}");
        sb.AppendLine("h1{font-size:22px;margin:0 0 8px}");
        sb.AppendLine("h2{font-size:14px;margin:24px 0 8px;color:#666;text-transform:uppercase;letter-spacing:.5px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:8px}");
        sb.AppendLine("th,td{border:1px solid #999;padding:6px 10px;text-align:left;font-size:13px}");
        sb.AppendLine("th{background:#f0f0f0}");
        sb.AppendLine("td.num,th.num{text-align:right}");
        sb.AppendLine(".totals{display:grid;grid-template-columns:repeat(4,1fr);gap:16px;margin:12px 0}");
        sb.AppendLine(".totals .box{border:1px solid #999;padding:10px;background:#fafafa}");
        sb.AppendLine(".totals .box .v{font-size:18px;font-weight:700}");
        sb.AppendLine(".variance.bad{color:#b71c1c}");
        sb.AppendLine(".variance.ok{color:#1b5e20}");
        sb.AppendLine("@media print{body{padding:0}}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>Razdolzuvanje — {Esc(report.OrderNumber)}</h1>");
        sb.AppendLine($"<div>Authorization: <b>{Esc(report.AuthorizationNumber ?? "—")}</b> · Статус: <b>{Esc(report.StatusName)}</b></div>");

        sb.AppendLine("<h2>Salda</h2><div class=\"totals\">");
        sb.AppendLine($"<div class=\"box\"><div>IM (задолжено)</div><div class=\"v\">{report.TotalImDuty:F2}</div></div>");
        sb.AppendLine($"<div class=\"box\"><div>EX (раздолжено)</div><div class=\"v\">{report.TotalExDuty:F2}</div></div>");
        sb.AppendLine($"<div class=\"box\"><div>Waste</div><div class=\"v\">{report.TotalWasteDuty:F2}</div></div>");
        sb.AppendLine($"<div class=\"box\"><div>Return</div><div class=\"v\">{report.TotalReturnDuty:F2}</div></div>");
        sb.AppendLine("</div>");

        var varianceClass = report.IsReconciled ? "ok" : "bad";
        sb.AppendLine($"<div class=\"variance {varianceClass}\"><b>Variance:</b> {report.Variance:F4} EUR (tolerance {report.ToleranceEur:F2})");
        sb.AppendLine($" · {(report.IsReconciled ? "✓ reconciled" : "✗ outstanding")}</div>");
        sb.AppendLine($"<div>Lines flagged: <b>{report.LinesRazdolzeno}</b> / {report.TotalLines}</div>");

        sb.AppendLine("<h2>Линии (IM)</h2>");
        sb.AppendLine("<table><thead><tr><th>#</th><th>MRN</th><th>Декларација</th><th>Артикл</th><th class=\"num\">Кол.</th><th>ЕМ</th><th class=\"num\">Duty</th><th class=\"num\">VAT</th><th>Razd.</th></tr></thead><tbody>");
        foreach (var l in report.Lines)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{l.LineNumber}</td>");
            sb.AppendLine($"<td>{Esc(l.MRN)}</td>");
            sb.AppendLine($"<td>{Esc(l.DeclarationNumber)}</td>");
            sb.AppendLine($"<td>{Esc(l.ItemCode ?? "—")} — {Esc(l.ItemName ?? "")}</td>");
            sb.AppendLine($"<td class=\"num\">{l.Quantity:F4}</td>");
            sb.AppendLine($"<td>{Esc(l.UoMCode ?? "—")}</td>");
            sb.AppendLine($"<td class=\"num\">{l.DutyAmount:F2}</td>");
            sb.AppendLine($"<td class=\"num\">{l.VATAmount:F2}</td>");
            sb.AppendLine($"<td>{(l.RazdolzenaDaNe ? "✓" : "—")}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("</body></html>");
        return Content(sb.ToString(), "text/html; charset=utf-8");
    }

    /// <summary>
    /// Phase 17 §E9 — convenience wrapper that resolves the order's
    /// LONAuthorizationId + a [from,to] window and delegates to
    /// <see cref="GeneratePee060XmlQuery"/>. The hub button calls this so
    /// users don't have to round-trip via the legacy authorization-scoped
    /// PEE060 endpoint.
    /// </summary>
    [HttpGet("{id:guid}/razdolzuvanje/pee060")]
    public async Task<IActionResult> GetRazdolzuvanjePee060(
        Guid id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var order = await _context.ClientOrders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound(new { errorMessage = "ClientOrder not found." });

        // Default window: order date → today (UTC). Caller may override.
        var fromD = from ?? order.OrderDate.Date;
        var toD = to ?? DateTime.UtcNow.Date;

        var result = await Mediator.Send(new GeneratePee060XmlQuery(order.LONAuthorizationId, fromD, toD));
        if (!result.IsSuccess || result.Data == null) return BadRequest(result);
        return File(Encoding.UTF8.GetBytes(result.Data.Xml),
            "application/xml", result.Data.FileName);
    }

    private static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    public record MarkLineBody(Guid LineId, bool RazdolzenaDaNe);
    public record SnapshotBody
    {
        public DateTime? SnapshotDate { get; init; }
        public string? Notes { get; init; }
    }
}
