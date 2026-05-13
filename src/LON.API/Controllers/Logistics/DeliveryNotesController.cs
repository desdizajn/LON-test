using System.Text;
using LON.Application.Logistics.DeliveryNotes;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers.Logistics;

/// <summary>
/// Phase 17 §E7.6 (D5) — CRUD-ish API for the polymorphic legacy
/// `Propratnica` paperwork (`DeliveryNote`). Most rows are auto-created by
/// MaterialIssue / Shipment commits in `Draft`; this controller lets the
/// operator fill in driver/vehicle/remarks, confirm (Draft → Sent) so the
/// cover sheet is finalised, or cancel before goods leave.
/// </summary>
[Route("api/Logistics/delivery-notes")]
public class DeliveryNotesController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? type = null,
        [FromQuery] int? status = null,
        [FromQuery] Guid? partnerId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await Mediator.Send(new GetDeliveryNotesQuery
        {
            Type = type,
            Status = status,
            PartnerId = partnerId,
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
        var result = await Mediator.Send(new GetDeliveryNoteByIdQuery(id));
        if (!result.IsSuccess)
        {
            // Surface "not found" as 404 so the UI can branch cleanly.
            if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(result);
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeliveryNoteBody body)
    {
        var cmd = new UpdateDeliveryNoteCommand
        {
            Id = id,
            DriverName = body.DriverName,
            VehicleRegistration = body.VehicleRegistration,
            Remarks = body.Remarks,
            DispatchDate = body.DispatchDate,
        };
        var result = await Mediator.Send(cmd);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var result = await Mediator.Send(new ConfirmDeliveryNoteCommand(id));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelDeliveryNoteBody body)
    {
        var result = await Mediator.Send(new CancelDeliveryNoteCommand(id, body.Reason));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Returns a print-friendly HTML cover-sheet. v1 is HTML-only — the browser
    /// can print to PDF; §E13/post-v1 swaps to QuestPDF for true server-side
    /// PDF generation. Endpoint name kept as `/pdf` for forward compatibility
    /// (the response Content-Type tells the client what they're getting).
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id)
    {
        var result = await Mediator.Send(new GetDeliveryNoteByIdQuery(id));
        if (!result.IsSuccess) return NotFound();
        var dn = result.Data!;

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"mk\"><head><meta charset=\"utf-8\">");
        sb.AppendLine($"<title>Propratnica {dn.Number}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font:14px/1.4 system-ui,Segoe UI,sans-serif;padding:24px;color:#111}");
        sb.AppendLine("h1{font-size:22px;margin:0 0 8px}");
        sb.AppendLine("h2{font-size:14px;margin:24px 0 8px;color:#666;text-transform:uppercase;letter-spacing:.5px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:8px}");
        sb.AppendLine("th,td{border:1px solid #999;padding:6px 10px;text-align:left;font-size:13px}");
        sb.AppendLine("th{background:#f0f0f0}");
        sb.AppendLine(".meta{display:grid;grid-template-columns:repeat(2,1fr);gap:8px 24px;margin:12px 0}");
        sb.AppendLine(".meta .row{display:flex;gap:6px}");
        sb.AppendLine(".meta .row b{min-width:160px;color:#444}");
        sb.AppendLine(".sig{margin-top:48px;display:grid;grid-template-columns:1fr 1fr;gap:48px}");
        sb.AppendLine(".sig div{border-top:1px solid #000;padding-top:6px;text-align:center;font-size:12px;color:#444}");
        sb.AppendLine("@media print{body{padding:0}}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>Пропратница {Esc(dn.Number)}</h1>");
        sb.AppendLine($"<div>Тип: <b>{Esc(dn.DocumentTypeName)}</b> · Статус: <b>{Esc(dn.StatusName)}</b></div>");

        sb.AppendLine("<h2>Податоци</h2><div class=\"meta\">");
        sb.AppendLine($"<div class=\"row\"><b>Број:</b> {Esc(dn.Number)}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Датум на испорака:</b> {dn.DispatchDate:yyyy-MM-dd}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Возач:</b> {Esc(dn.DriverName ?? "—")}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Возило:</b> {Esc(dn.VehicleRegistration ?? "—")}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Поврзан документ:</b> {dn.RelatedDocumentId}</div>");
        sb.AppendLine($"<div class=\"row\"><b>Од локација:</b> {dn.FromLocationId}</div>");
        if (dn.ToPartnerId.HasValue)
            sb.AppendLine($"<div class=\"row\"><b>Кон партнер:</b> {dn.ToPartnerId}</div>");
        if (dn.ToLocationId.HasValue)
            sb.AppendLine($"<div class=\"row\"><b>Кон локација:</b> {dn.ToLocationId}</div>");
        sb.AppendLine("</div>");

        if (!string.IsNullOrWhiteSpace(dn.Remarks))
            sb.AppendLine($"<h2>Забелешки</h2><div>{Esc(dn.Remarks)}</div>");

        sb.AppendLine("<h2>Ставки</h2>");
        sb.AppendLine("<table><thead><tr><th>Бр.</th><th>Опис</th><th>Серија</th><th>MRN</th><th>Количина</th></tr></thead><tbody>");
        var idx = 0;
        foreach (var l in dn.Lines)
        {
            idx++;
            sb.AppendLine($"<tr><td>{idx}</td><td>{Esc(l.Description)}</td><td>{Esc(l.BatchNumber ?? "—")}</td><td>{Esc(l.MRN ?? "—")}</td><td>{l.Quantity:F4}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class=\"sig\"><div>Издал</div><div>Примил</div></div>");
        sb.AppendLine("</body></html>");

        return Content(sb.ToString(), "text/html; charset=utf-8");
    }

    private static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    public record UpdateDeliveryNoteBody
    {
        public string? DriverName { get; init; }
        public string? VehicleRegistration { get; init; }
        public string? Remarks { get; init; }
        public DateTime? DispatchDate { get; init; }
    }

    public record CancelDeliveryNoteBody
    {
        public string? Reason { get; init; }
    }
}
