using System.Text;
using LON.Application.Customs.CommercialInvoices;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers.Customs;

/// <summary>
/// Phase 17 §E8.5 (D4) — CRUD API for export commercial invoices that
/// accompany EX customs declarations. Replaces legacy `tblIzvozniFakturi` +
/// `tblIzvozniFakturiStavki`.
///
/// Status lifecycle: Draft → Issued (locks; PDF renderable) → optional
/// Cancelled. Draft invoices can be soft-deleted; Issued invoices can only
/// be Cancelled. The PDF endpoint returns HTML for v1 (browser print → PDF),
/// same convention as the DeliveryNote `/pdf` endpoint.
/// </summary>
[Route("api/Customs/commercial-invoices")]
public class CommercialInvoicesController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? clientOrderId = null,
        [FromQuery] Guid? consigneePartnerId = null,
        [FromQuery] int? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await Mediator.Send(new GetCommercialInvoicesQuery
        {
            ClientOrderId = clientOrderId,
            ConsigneePartnerId = consigneePartnerId,
            Status = status,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetCommercialInvoiceByIdQuery(id));
        if (!result.IsSuccess)
        {
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(result);
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommercialInvoiceCommand cmd)
    {
        var result = await Mediator.Send(cmd);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommercialInvoiceCommand cmd)
    {
        // Body's Id is canonicalised by the route Id so the wire shape is
        // permissive (caller may omit Id in the JSON body).
        var canonical = cmd with { Id = id };
        var result = await Mediator.Send(canonical);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id)
    {
        var result = await Mediator.Send(new IssueCommercialInvoiceCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelCommercialInvoiceBody body)
    {
        var result = await Mediator.Send(new CancelCommercialInvoiceCommand(id, body?.Reason));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await Mediator.Send(new DeleteCommercialInvoiceCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Draft a CI from an existing Shipment's lines. Returns a fully-populated
    /// DTO (zero `Id`s on lines = unpersisted draft) the UI can POST back to
    /// the create endpoint with the consignee/consignor/prices filled in.
    /// </summary>
    [HttpPost("suggest-from-shipment")]
    public async Task<IActionResult> SuggestFromShipment(
        [FromServices] ICommercialInvoiceSuggestionService suggester,
        [FromQuery] Guid shipmentId)
    {
        if (shipmentId == Guid.Empty)
            return BadRequest(new { errorMessage = "shipmentId query parameter is required." });
        var result = await suggester.SuggestFromShipment(shipmentId, HttpContext.RequestAborted);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Standardised export-invoice HTML (browser → print → PDF). v1 keeps this
    /// HTML-only; QuestPDF swap is a post-v1 polish (same as DeliveryNote).
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id)
    {
        var result = await Mediator.Send(new GetCommercialInvoiceByIdQuery(id));
        if (!result.IsSuccess) return NotFound();
        var ci = result.Data!;

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"mk\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Commercial Invoice {Esc(ci.Number)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font:14px/1.4 system-ui,Segoe UI,sans-serif;padding:24px;color:#111}");
        sb.AppendLine("h1{font-size:22px;margin:0 0 8px}");
        sb.AppendLine("h2{font-size:14px;margin:24px 0 8px;color:#666;text-transform:uppercase;letter-spacing:.5px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:8px}");
        sb.AppendLine("th,td{border:1px solid #999;padding:6px 10px;text-align:left;font-size:13px}");
        sb.AppendLine("th{background:#f0f0f0}");
        sb.AppendLine("td.num,th.num{text-align:right}");
        sb.AppendLine(".meta{display:grid;grid-template-columns:repeat(2,1fr);gap:8px 24px;margin:12px 0}");
        sb.AppendLine(".meta .row{display:flex;gap:6px}");
        sb.AppendLine(".meta .row b{min-width:160px;color:#444}");
        sb.AppendLine(".parties{display:grid;grid-template-columns:1fr 1fr;gap:24px;margin:16px 0}");
        sb.AppendLine(".parties .card{border:1px solid #ccc;padding:12px;background:#fafafa}");
        sb.AppendLine(".totals{margin-top:12px;display:flex;justify-content:flex-end;gap:24px}");
        sb.AppendLine(".totals .box{min-width:240px;border:1px solid #999;padding:8px 12px}");
        sb.AppendLine(".totals .box .row{display:flex;justify-content:space-between;gap:12px}");
        sb.AppendLine(".totals .box .row.grand{border-top:1px solid #000;margin-top:6px;padding-top:6px;font-weight:700}");
        sb.AppendLine(".sig{margin-top:48px;display:grid;grid-template-columns:1fr 1fr;gap:48px}");
        sb.AppendLine(".sig div{border-top:1px solid #000;padding-top:6px;text-align:center;font-size:12px;color:#444}");
        sb.AppendLine("@media print{body{padding:0}}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>Commercial Invoice {Esc(ci.Number)}</h1>");
        sb.AppendLine($"<div>Статус: <b>{Esc(ci.StatusName)}</b> · Датум: <b>{ci.InvoiceDate:yyyy-MM-dd}</b></div>");

        sb.AppendLine("<div class=\"parties\">");
        sb.AppendLine("<div class=\"card\"><b>Consignor</b><br>");
        sb.AppendLine($"{Esc(ci.ConsignorName ?? "—")}<br>{Esc(ci.ConsignorCode ?? "")}</div>");
        sb.AppendLine("<div class=\"card\"><b>Consignee</b><br>");
        sb.AppendLine($"{Esc(ci.ConsigneeName ?? "—")}<br>{Esc(ci.ConsigneeCode ?? "")}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Детали</h2><div class=\"meta\">");
        sb.AppendLine($"<div class=\"row\"><b>Број:</b> {Esc(ci.Number)}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Валута:</b> {Esc(ci.Currency)}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Incoterms:</b> {Esc(ci.Incoterms)}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Destination country:</b> {Esc(ci.CountryOfDestination ?? "—")}</div>");
        if (!string.IsNullOrWhiteSpace(ci.PaymentTerms))
            sb.AppendLine($"<div class=\"row\"><b>Payment terms:</b> {Esc(ci.PaymentTerms)}</div>");
        if (!string.IsNullOrWhiteSpace(ci.ClientOrderNumber))
            sb.AppendLine($"<div class=\"row\"><b>Client order:</b> {Esc(ci.ClientOrderNumber)}</div>");
        if (!string.IsNullOrWhiteSpace(ci.ShipmentNumber))
            sb.AppendLine($"<div class=\"row\"><b>Shipment:</b> {Esc(ci.ShipmentNumber)}</div>");
        if (!string.IsNullOrWhiteSpace(ci.CustomsDeclarationNumber))
            sb.AppendLine($"<div class=\"row\"><b>Customs declaration:</b> {Esc(ci.CustomsDeclarationNumber)}</div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Ставки</h2>");
        sb.AppendLine("<table><thead><tr><th>#</th><th>Item</th><th>Description</th><th class=\"num\">Qty</th><th>UoM</th><th class=\"num\">Unit price</th><th class=\"num\">Line total</th><th>Origin</th></tr></thead><tbody>");
        foreach (var l in ci.Lines)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{l.LineNumber}</td>");
            sb.AppendLine($"<td>{Esc(l.ItemCode ?? "—")}</td>");
            sb.AppendLine($"<td>{Esc(l.Description)}</td>");
            sb.AppendLine($"<td class=\"num\">{l.Quantity:F4}</td>");
            sb.AppendLine($"<td>{Esc(l.UoMCode ?? "—")}</td>");
            sb.AppendLine($"<td class=\"num\">{l.UnitPrice:F4}</td>");
            sb.AppendLine($"<td class=\"num\">{l.LineTotal:F4}</td>");
            sb.AppendLine($"<td>{Esc(l.CountryOfOrigin ?? "—")}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class=\"totals\"><div class=\"box\">");
        sb.AppendLine($"<div class=\"row\"><span>Subtotal</span><span>{ci.Subtotal:F4} {Esc(ci.Currency)}</span></div>");
        if (ci.TaxAmount.HasValue)
            sb.AppendLine($"<div class=\"row\"><span>Tax</span><span>{ci.TaxAmount.Value:F4} {Esc(ci.Currency)}</span></div>");
        sb.AppendLine($"<div class=\"row grand\"><span>Total</span><span>{ci.TotalAmount:F4} {Esc(ci.Currency)}</span></div>");
        sb.AppendLine("</div></div>");

        if (!string.IsNullOrWhiteSpace(ci.Notes))
            sb.AppendLine($"<h2>Notes</h2><div>{Esc(ci.Notes)}</div>");

        sb.AppendLine("<div class=\"sig\"><div>Issued by</div><div>Received by</div></div>");
        sb.AppendLine("</body></html>");

        return Content(sb.ToString(), "text/html; charset=utf-8");
    }

    private static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    public record CancelCommercialInvoiceBody
    {
        public string? Reason { get; init; }
    }
}
