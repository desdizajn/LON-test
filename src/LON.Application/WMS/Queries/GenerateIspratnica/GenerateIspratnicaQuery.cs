using System.Globalization;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Common.Queries;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.WMS.Queries.GenerateIspratnica;

/// <summary>
/// P15.9.1 — render the Ispratnica document for a Shipment. Returns a
/// self-contained HTML string (no external CSS / JS / images) so the
/// frontend can drop it into an iframe and <c>window.print()</c> it. The
/// layout matches the legacy ELON Ispratnica form: header with tenant +
/// counterparty + regime (EXA3 / VS7 / DOM), line table with item / batch
/// / MRN / qty, and a footer with signature blocks + zaverka.
///
/// Structured JSON fields are also returned alongside the HTML so a
/// frontend that prefers a React render can bypass the baked-in HTML.
/// </summary>
public sealed record GenerateIspratnicaQuery(Guid ShipmentId) : IQuery<Result<IspratnicaPayload>>;

public sealed record IspratnicaPayload(
    Guid ShipmentId,
    string ShipmentNumber,
    DateTime ShipmentDate,
    string Regime,
    bool IsReturn,
    string? ZaverkaNumber,
    DateTime? ZaverkaDate,
    string? CustomerName,
    string? CarrierName,
    string? TrackingNumber,
    string? SalesOrderNumber,
    List<IspratnicaLine> Lines,
    decimal TotalQuantity,
    string Html);

public sealed record IspratnicaLine(
    int LineNumber,
    string ItemCode,
    string ItemName,
    string? BatchNumber,
    string? MRN,
    decimal Quantity,
    string UoMCode,
    string? CustomsDeclarationNumber);

public sealed class GenerateIspratnicaQueryHandler
    : IQueryHandler<GenerateIspratnicaQuery, Result<IspratnicaPayload>>
{
    private readonly IApplicationDbContext _context;

    public GenerateIspratnicaQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IspratnicaPayload>> Handle(GenerateIspratnicaQuery request, CancellationToken ct)
    {
        var shipment = await _context.Shipments
            .Include(s => s.Customer)
            .Include(s => s.Carrier)
            .Include(s => s.Lines).ThenInclude(l => l.Item)
            .Include(s => s.Lines).ThenInclude(l => l.UoM)
            .FirstOrDefaultAsync(s => s.Id == request.ShipmentId, ct);

        if (shipment is null)
            return Result<IspratnicaPayload>.Failure($"Shipment '{request.ShipmentId}' not found.");

        // Join customs declaration numbers by MRN on each shipment line.
        var mrns = shipment.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.MRN))
            .Select(l => l.MRN!)
            .Distinct()
            .ToList();
        var declNumbers = mrns.Count == 0
            ? new Dictionary<string, string>()
            : await _context.CustomsDeclarations
                .Where(d => mrns.Contains(d.MRN) && !d.IsDeleted)
                .Select(d => new { d.MRN, d.DeclarationNumber })
                .ToDictionaryAsync(d => d.MRN, d => d.DeclarationNumber, ct);

        var lines = shipment.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new IspratnicaLine(
                l.LineNumber,
                l.Item?.Code ?? "",
                l.Item?.Name ?? "",
                l.BatchNumber,
                l.MRN,
                l.Quantity,
                l.UoM?.Code ?? "",
                string.IsNullOrWhiteSpace(l.MRN) ? null
                    : declNumbers.TryGetValue(l.MRN, out var dn) ? dn : null))
            .ToList();

        var regime = shipment.ShipmentRegime ?? "DOM";
        var html = BuildHtml(shipment, lines, regime);

        return Result<IspratnicaPayload>.Success(new IspratnicaPayload(
            shipment.Id,
            shipment.ShipmentNumber,
            shipment.ShipmentDate,
            regime,
            shipment.IsReturn,
            shipment.ZaverkaNumber,
            shipment.ZaverkaDate,
            shipment.Customer?.Name,
            shipment.Carrier?.Name,
            shipment.TrackingNumber,
            shipment.SalesOrderNumber,
            lines,
            lines.Sum(l => l.Quantity),
            html));
    }

    private static string BuildHtml(
        LON.Domain.Entities.WMS.Shipment shipment,
        List<IspratnicaLine> lines,
        string regime)
    {
        var regimeLabel = regime switch
        {
            "EXA3" => "Извоз (EXA3, постапка 31 51)",
            "VS7" => "Враќање на материјал (VS7, постапка 61 21)",
            "DOM" => "Домашна испорака",
            _ => regime
        };
        var title = shipment.IsReturn ? "ИСПРАТНИЦА (ВРАЌАЊЕ)" : "ИСПРАТНИЦА";
        var total = lines.Sum(l => l.Quantity).ToString("0.00", CultureInfo.InvariantCulture);

        var rows = string.Join("\n", lines.Select(l => $@"
          <tr>
            <td style='text-align:center'>{l.LineNumber}</td>
            <td><strong>{HtmlEncode(l.ItemCode)}</strong><div style='font-size:11px;color:#666'>{HtmlEncode(l.ItemName)}</div></td>
            <td>{HtmlEncode(l.BatchNumber ?? "—")}</td>
            <td><code>{HtmlEncode(l.MRN ?? "—")}</code></td>
            <td>{HtmlEncode(l.CustomsDeclarationNumber ?? "—")}</td>
            <td style='text-align:right;font-family:monospace'>{l.Quantity.ToString("0.0000", CultureInfo.InvariantCulture)} {HtmlEncode(l.UoMCode)}</td>
          </tr>"));

        return $@"<!doctype html>
<html lang='mk'>
<head>
<meta charset='utf-8' />
<title>{HtmlEncode(title)} {HtmlEncode(shipment.ShipmentNumber)}</title>
<style>
  body {{ font-family: Arial, sans-serif; font-size: 12px; margin: 20px; color: #222; }}
  h1 {{ text-align: center; margin: 0 0 4px 0; font-size: 20px; letter-spacing: 2px; }}
  .sub {{ text-align: center; color: #666; margin-bottom: 20px; }}
  .hdr {{ display: flex; justify-content: space-between; margin-bottom: 15px; }}
  .hdr > div {{ flex: 1; padding: 8px; border: 1px solid #ccc; margin: 0 4px; }}
  .hdr strong {{ display: block; text-transform: uppercase; font-size: 10px; color: #666; margin-bottom: 4px; }}
  table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
  th, td {{ border: 1px solid #ccc; padding: 6px; vertical-align: top; }}
  th {{ background: #f5f5f5; text-transform: uppercase; font-size: 10px; color: #666; }}
  .totals {{ margin-top: 10px; text-align: right; font-size: 14px; font-weight: bold; }}
  .footer {{ display: flex; justify-content: space-between; margin-top: 40px; gap: 20px; }}
  .footer > div {{ flex: 1; border-top: 1px solid #222; padding-top: 6px; text-align: center; font-size: 11px; color: #666; }}
  .zaverka {{ margin-top: 30px; padding: 10px; border: 2px solid #222; background: #fffbea; }}
  @media print {{ body {{ margin: 10mm; }} .noprint {{ display: none; }} }}
</style>
</head>
<body>
  <h1>{HtmlEncode(title)}</h1>
  <div class='sub'>{HtmlEncode(regimeLabel)}</div>

  <div class='hdr'>
    <div>
      <strong>Број</strong>
      {HtmlEncode(shipment.ShipmentNumber)}
    </div>
    <div>
      <strong>Датум</strong>
      {shipment.ShipmentDate:dd.MM.yyyy}
    </div>
    <div>
      <strong>Клиент</strong>
      {HtmlEncode(shipment.Customer?.Name ?? "—")}
    </div>
    <div>
      <strong>Превозник</strong>
      {HtmlEncode(shipment.Carrier?.Name ?? "—")}
    </div>
  </div>

  <div class='hdr'>
    <div>
      <strong>Tracking #</strong>
      {HtmlEncode(shipment.TrackingNumber ?? "—")}
    </div>
    <div>
      <strong>Sales Order</strong>
      {HtmlEncode(shipment.SalesOrderNumber ?? "—")}
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th>#</th>
        <th>Артикл</th>
        <th>Batch</th>
        <th>MRN</th>
        <th>Декларација</th>
        <th>Количина</th>
      </tr>
    </thead>
    <tbody>
      {rows}
    </tbody>
  </table>

  <div class='totals'>Вкупно: {total}</div>

  {(shipment.ZaverkaNumber != null ? $@"
  <div class='zaverka'>
    <strong>ЗАВЕРКА</strong><br/>
    Број: {HtmlEncode(shipment.ZaverkaNumber!)} · Датум: {shipment.ZaverkaDate:dd.MM.yyyy}
  </div>" : "")}

  <div class='footer'>
    <div>Подготвил<br/>_______________________</div>
    <div>Превозник<br/>_______________________</div>
    <div>Примил<br/>_______________________</div>
    {(regime != "DOM" ? "<div>Царински инспектор<br/>_______________________</div>" : "")}
  </div>

  <div class='noprint' style='margin-top:30px;text-align:center'>
    <button onclick='window.print()'>Печати</button>
  </div>
</body>
</html>";
    }

    private static string HtmlEncode(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");
}
