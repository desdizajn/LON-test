using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management;

// ─────────────────────── P13.1 — on-time delivery ───────────────────────

/// <summary>
/// A shipment's on-time bucket. Derived from
/// <c>ShipmentDate − max(linkedPO.PlannedEndDate)</c>. Shipments that we
/// cannot link to any PO (no batch match) land in <see cref="Unknown"/>
/// — they are surfaced separately so the user sees the coverage gap
/// rather than silently biasing the on-time %.
/// </summary>
public enum OnTimeBucket
{
    OnTime = 1,
    Late1To7 = 2,
    LateOver7 = 3,
    Unknown = 99
}

public sealed record OnTimeShipmentRow(
    Guid ShipmentId,
    string ShipmentNumber,
    DateTime ShipmentDate,
    Guid? CustomerId,
    string? CustomerCode,
    string? CustomerName,
    DateTime? PlannedEndDate,
    int? DaysLate,
    OnTimeBucket Bucket);

public sealed record OnTimeCustomerRollup(
    Guid? CustomerId,
    string CustomerName,
    int TotalShipments,
    int OnTime,
    int Late1To7,
    int LateOver7,
    int Unknown,
    double OnTimePercentage);

public sealed record OnTimeReport(
    DateTime From,
    DateTime To,
    IReadOnlyList<OnTimeShipmentRow> Shipments,
    IReadOnlyList<OnTimeCustomerRollup> ByCustomer,
    OnTimeCustomerRollup Overall);

public sealed record GetOnTimeReportQuery(DateTime? From, DateTime? To)
    : IRequest<Result<OnTimeReport>>;

public sealed class GetOnTimeReportHandler : IRequestHandler<GetOnTimeReportQuery, Result<OnTimeReport>>
{
    private readonly IApplicationDbContext _context;
    public GetOnTimeReportHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<OnTimeReport>> Handle(GetOnTimeReportQuery request, CancellationToken ct)
    {
        var to = (request.To ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var from = (request.From ?? to.AddMonths(-3)).Date;

        // Only count shipments actually out the door: Shipped/Delivered.
        var shipments = await _context.Shipments.AsNoTracking()
            .Where(s => s.ShipmentDate >= from && s.ShipmentDate <= to
                    && (s.Status == ShipmentStatus.Shipped || s.Status == ShipmentStatus.Delivered))
            .Select(s => new
            {
                s.Id,
                s.ShipmentNumber,
                s.ShipmentDate,
                s.CustomerId,
                CustomerCode = s.Customer != null ? s.Customer.Code : null,
                CustomerName = s.Customer != null ? s.Customer.Name : null,
                Batches = s.Lines.Where(l => !string.IsNullOrEmpty(l.BatchNumber))
                    .Select(l => l.BatchNumber!).ToList(),
            })
            .ToListAsync(ct);

        // One pass to collect all batches referenced.
        var allBatches = shipments.SelectMany(s => s.Batches).Distinct().ToList();
        var batchToPlannedEnd = allBatches.Count == 0
            ? new Dictionary<string, DateTime>()
            : await _context.ProductionReceipts.AsNoTracking()
                .Where(pr => allBatches.Contains(pr.BatchNumber))
                .Select(pr => new { pr.BatchNumber, pr.ProductionOrder.PlannedEndDate })
                .GroupBy(x => x.BatchNumber)
                .Select(g => new { Batch = g.Key, PlannedEndDate = g.Max(x => x.PlannedEndDate) })
                .ToDictionaryAsync(x => x.Batch, x => x.PlannedEndDate, ct);

        var rows = new List<OnTimeShipmentRow>(shipments.Count);
        foreach (var s in shipments)
        {
            DateTime? plannedEnd = null;
            foreach (var batch in s.Batches)
            {
                if (batchToPlannedEnd.TryGetValue(batch, out var pd))
                {
                    if (plannedEnd is null || pd > plannedEnd.Value) plannedEnd = pd;
                }
            }

            int? daysLate = plannedEnd.HasValue
                ? (int)Math.Ceiling((s.ShipmentDate.Date - plannedEnd.Value.Date).TotalDays)
                : null;
            var bucket = !plannedEnd.HasValue ? OnTimeBucket.Unknown
                : daysLate <= 0 ? OnTimeBucket.OnTime
                : daysLate <= 7 ? OnTimeBucket.Late1To7
                : OnTimeBucket.LateOver7;

            rows.Add(new OnTimeShipmentRow(
                s.Id, s.ShipmentNumber, s.ShipmentDate,
                s.CustomerId, s.CustomerCode, s.CustomerName,
                plannedEnd, daysLate, bucket));
        }

        // Group by customer.
        var byCustomer = rows
            .GroupBy(r => new { r.CustomerId, Name = r.CustomerName ?? "—" })
            .Select(g => BuildRollup(g.Key.CustomerId, g.Key.Name, g.ToList()))
            .OrderByDescending(r => r.TotalShipments)
            .ToList();

        var overall = BuildRollup(null, "ALL", rows);

        return Result<OnTimeReport>.Success(new OnTimeReport(from, to, rows, byCustomer, overall));
    }

    private static OnTimeCustomerRollup BuildRollup(Guid? id, string name, List<OnTimeShipmentRow> rows)
    {
        var total = rows.Count;
        var onTime = rows.Count(r => r.Bucket == OnTimeBucket.OnTime);
        var late1to7 = rows.Count(r => r.Bucket == OnTimeBucket.Late1To7);
        var lateOver7 = rows.Count(r => r.Bucket == OnTimeBucket.LateOver7);
        var unknown = rows.Count(r => r.Bucket == OnTimeBucket.Unknown);
        var denom = total - unknown;
        var pct = denom > 0 ? Math.Round(onTime * 100.0 / denom, 2) : 0.0;
        return new OnTimeCustomerRollup(id, name, total, onTime, late1to7, lateOver7, unknown, pct);
    }
}

// ─────────────────────── P13.3 — production + billing by customer ───────────────────────

public sealed record CustomerSummaryRow(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    int OpenPOs,
    int CompletedPOs,
    decimal ProducedQuantity,
    int ShipmentCount,
    decimal ShippedQuantity,
    int InvoicesIssued,
    decimal InvoicedOutstanding,
    decimal InvoicedPaid,
    string Currency);

public sealed record ByCustomerReport(
    DateTime From,
    DateTime To,
    IReadOnlyList<CustomerSummaryRow> Rows);

public sealed record GetByCustomerReportQuery(DateTime? From, DateTime? To)
    : IRequest<Result<ByCustomerReport>>;

public sealed class GetByCustomerReportHandler
    : IRequestHandler<GetByCustomerReportQuery, Result<ByCustomerReport>>
{
    private readonly IApplicationDbContext _context;
    public GetByCustomerReportHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<ByCustomerReport>> Handle(GetByCustomerReportQuery request, CancellationToken ct)
    {
        var to = (request.To ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var from = (request.From ?? to.AddMonths(-6)).Date;

        // Every customer partner that has *any* touchpoint in the window:
        // PO with CustomerPartnerId OR Shipment OR Invoice.
        var poStats = await _context.ProductionOrders.AsNoTracking()
            .Where(p => p.CustomerPartnerId != null
                     && (p.PlannedStartDate <= to && p.PlannedEndDate >= from || p.ActualEndDate >= from && p.ActualEndDate <= to))
            .GroupBy(p => p.CustomerPartnerId!.Value)
            .Select(g => new
            {
                CustomerId = g.Key,
                OpenPOs = g.Count(p => p.Status != ProductionOrderStatus.Completed
                                    && p.Status != ProductionOrderStatus.Cancelled
                                    && p.Status != ProductionOrderStatus.Closed),
                CompletedPOs = g.Count(p => p.Status == ProductionOrderStatus.Completed),
                ProducedQuantity = g.Sum(p => p.ProducedQuantity),
            })
            .ToListAsync(ct);

        var shipmentStats = await _context.Shipments.AsNoTracking()
            .Where(s => s.CustomerId != null
                     && s.ShipmentDate >= from && s.ShipmentDate <= to
                     && (s.Status == ShipmentStatus.Shipped || s.Status == ShipmentStatus.Delivered))
            .GroupBy(s => s.CustomerId!.Value)
            .Select(g => new
            {
                CustomerId = g.Key,
                ShipmentCount = g.Count(),
                ShippedQuantity = g.SelectMany(s => s.Lines).Sum(l => l.Quantity),
            })
            .ToListAsync(ct);

        var invoiceStats = await _context.Invoices.AsNoTracking()
            .Where(i => i.IssueDate >= from && i.IssueDate <= to
                     && i.Status != InvoiceStatus.Cancelled)
            .GroupBy(i => new { i.PartnerId, i.Currency })
            .Select(g => new
            {
                CustomerId = g.Key.PartnerId,
                Currency = g.Key.Currency,
                InvoicesIssued = g.Count(),
                InvoicedOutstanding = g.Where(i => i.Status == InvoiceStatus.Issued).Sum(i => (decimal?)i.TotalAmount) ?? 0m,
                InvoicedPaid = g.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => (decimal?)i.TotalAmount) ?? 0m,
            })
            .ToListAsync(ct);

        var customerIds = poStats.Select(x => x.CustomerId)
            .Union(shipmentStats.Select(x => x.CustomerId))
            .Union(invoiceStats.Select(x => x.CustomerId))
            .Distinct().ToList();

        var partners = await _context.Partners.AsNoTracking()
            .Where(p => customerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.Name })
            .ToListAsync(ct);

        var rows = partners.Select(p =>
        {
            var po = poStats.FirstOrDefault(x => x.CustomerId == p.Id);
            var sh = shipmentStats.FirstOrDefault(x => x.CustomerId == p.Id);
            var inv = invoiceStats.FirstOrDefault(x => x.CustomerId == p.Id);
            return new CustomerSummaryRow(
                p.Id, p.Code, p.Name,
                po?.OpenPOs ?? 0,
                po?.CompletedPOs ?? 0,
                po?.ProducedQuantity ?? 0m,
                sh?.ShipmentCount ?? 0,
                sh?.ShippedQuantity ?? 0m,
                inv?.InvoicesIssued ?? 0,
                inv?.InvoicedOutstanding ?? 0m,
                inv?.InvoicedPaid ?? 0m,
                inv?.Currency ?? "EUR");
        })
        .OrderByDescending(r => r.ProducedQuantity + r.ShippedQuantity)
        .ToList();

        return Result<ByCustomerReport>.Success(new ByCustomerReport(from, to, rows));
    }
}

// ─────────────────────── P13.5 — exception alerts ───────────────────────

/// <summary>Severity drives the colour band on the dashboard alert list.</summary>
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public enum AlertCategory
{
    MrnExpiring = 1,
    OverdueInvoice = 2,
    MaterialShortage = 3,
    AtRiskProductionOrder = 4,
    LonAuthorizationExpiring = 5
}

public sealed record AlertRow(
    AlertCategory Category,
    AlertSeverity Severity,
    string Title,
    string Detail,
    string? LinkPath,
    DateTime? RelatedDate,
    decimal? Amount,
    string? Currency);

public sealed record AlertsFeed(
    DateTime GeneratedAt,
    IReadOnlyList<AlertRow> Rows);

public sealed record GetAlertsQuery() : IRequest<Result<AlertsFeed>>;

public sealed class GetAlertsHandler : IRequestHandler<GetAlertsQuery, Result<AlertsFeed>>
{
    private readonly IApplicationDbContext _context;
    public GetAlertsHandler(IApplicationDbContext context) { _context = context; }

    public async Task<Result<AlertsFeed>> Handle(GetAlertsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var alerts = new List<AlertRow>();

        // 1) MRNs expiring in the next 30 days (or already expired but still active).
        var soon = now.AddDays(30);
        var mrns = await _context.MRNRegistries.AsNoTracking()
            .Where(m => m.IsActive && m.ExpiryDate != null && m.ExpiryDate <= soon)
            .Select(m => new { m.MRN, m.ExpiryDate, m.RemainingQuantity, m.UndischargedQuantity })
            .ToListAsync(ct);
        foreach (var m in mrns)
        {
            var days = (int)Math.Ceiling((m.ExpiryDate!.Value.Date - now.Date).TotalDays);
            var severity = days < 0 ? AlertSeverity.Critical
                : days <= 7 ? AlertSeverity.Critical
                : AlertSeverity.Warning;
            var title = days < 0
                ? $"MRN {m.MRN} expired {-days}d ago"
                : $"MRN {m.MRN} expires in {days}d";
            alerts.Add(new AlertRow(
                AlertCategory.MrnExpiring, severity, title,
                $"Outstanding LON undischarged = {m.UndischargedQuantity:N2}",
                $"/customs/deadlines", m.ExpiryDate, m.UndischargedQuantity, null));
        }

        // 2) Overdue issued invoices.
        var overdueInvoices = await _context.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < now)
            .Select(i => new { i.Id, i.Number, i.DueDate, i.TotalAmount, i.Currency, PartnerName = i.Partner.Name })
            .ToListAsync(ct);
        foreach (var inv in overdueInvoices)
        {
            var days = (int)Math.Ceiling((now.Date - inv.DueDate.Date).TotalDays);
            var severity = days > 30 ? AlertSeverity.Critical
                : days > 7 ? AlertSeverity.Warning
                : AlertSeverity.Info;
            alerts.Add(new AlertRow(
                AlertCategory.OverdueInvoice, severity,
                $"Invoice {inv.Number} — {days}d overdue",
                $"{inv.PartnerName} · {inv.TotalAmount:N2} {inv.Currency}",
                $"/finance/invoicing", inv.DueDate, inv.TotalAmount, inv.Currency));
        }

        // 3) Material shortage — reuse the P8.5 aggregate logic inline.
        var activeStates = new[] { ProductionOrderStatus.Draft, ProductionOrderStatus.Released, ProductionOrderStatus.InProgress };
        var shortageMaterials = await _context.ProductionOrderMaterials.AsNoTracking()
            .Where(m => activeStates.Contains(m.ProductionOrder.Status)
                     && m.RequiredQuantity > m.IssuedQuantity)
            .GroupBy(m => new { m.ItemId, ItemCode = m.Item.Code, ItemName = m.Item.Name, m.UoMId, UoMCode = m.UoM.Code })
            .Select(g => new
            {
                g.Key.ItemCode,
                g.Key.ItemName,
                g.Key.UoMCode,
                Needed = g.Sum(m => m.RequiredQuantity - m.IssuedQuantity),
                POs = g.Select(m => m.ProductionOrderId).Distinct().Count(),
                ItemId = g.Key.ItemId,
            })
            .ToListAsync(ct);

        foreach (var s in shortageMaterials)
        {
            var available = await _context.InventoryBalances.AsNoTracking()
                .Where(b => b.ItemId == s.ItemId
                         && (b.QualityStatus == QualityStatus.OK || b.QualityStatus == QualityStatus.None)
                         && b.LonProcessState == LonProcessState.Imported)
                .SumAsync(b => (decimal?)b.Quantity, ct) ?? 0m;
            var deficit = s.Needed - available;
            if (deficit <= 0) continue;
            alerts.Add(new AlertRow(
                AlertCategory.MaterialShortage, AlertSeverity.Warning,
                $"{s.ItemCode} — short {deficit:N2} {s.UoMCode}",
                $"{s.ItemName} · {s.POs} PO(s) need more than current OK inventory",
                $"/production/shortage", null, deficit, null));
        }

        // 4) At-risk POs — heuristic: active PO + planned window nearly over +
        // progress far below schedule (mirror of P8.4 FE heuristic).
        var active = await _context.ProductionOrders.AsNoTracking()
            .Where(p => p.Status == ProductionOrderStatus.InProgress
                     || p.Status == ProductionOrderStatus.Released)
            .Select(p => new { p.Id, p.OrderNumber, p.OrderQuantity, p.ProducedQuantity, p.PlannedStartDate, p.PlannedEndDate })
            .ToListAsync(ct);
        foreach (var po in active)
        {
            if (po.OrderQuantity <= 0) continue;
            var total = (po.PlannedEndDate - po.PlannedStartDate).TotalDays;
            if (total <= 0) continue;
            var elapsed = (now - po.PlannedStartDate).TotalDays;
            if (elapsed < 0) continue;
            var scheduleUsed = Math.Min(1.0, elapsed / total);
            var progress = Math.Min(1.0, (double)(po.ProducedQuantity / po.OrderQuantity));
            var daysToEnd = (int)Math.Ceiling((po.PlannedEndDate - now).TotalDays);
            var gap = scheduleUsed - progress;
            if (gap >= 0.25 && daysToEnd <= 7)
            {
                alerts.Add(new AlertRow(
                    AlertCategory.AtRiskProductionOrder, AlertSeverity.Critical,
                    $"PO {po.OrderNumber} at risk ({(int)(progress * 100)}% done, {daysToEnd}d to planned end)",
                    $"Schedule used {(int)(scheduleUsed * 100)}% vs progress {(int)(progress * 100)}%",
                    $"/production/at-risk", po.PlannedEndDate, null, null));
            }
            else if (gap >= 0.10 && daysToEnd <= 14)
            {
                alerts.Add(new AlertRow(
                    AlertCategory.AtRiskProductionOrder, AlertSeverity.Warning,
                    $"PO {po.OrderNumber} trailing ({(int)(progress * 100)}% done, {daysToEnd}d to planned end)",
                    $"Schedule used {(int)(scheduleUsed * 100)}% vs progress {(int)(progress * 100)}%",
                    $"/production/at-risk", po.PlannedEndDate, null, null));
            }
        }

        // 5) LON authorisations expiring. `Status` is a free-form string on
        // the legacy-parity entity; "Active" and "Approved" are both in-use.
        var activeAuthStatuses = new[] { "Active", "Approved" };
        var lonAuths = await _context.LONAuthorizations.AsNoTracking()
            .Where(a => activeAuthStatuses.Contains(a.Status)
                     && a.ExpiryDate != null && a.ExpiryDate <= soon)
            .Select(a => new { a.AuthorizationNumber, a.ExpiryDate })
            .ToListAsync(ct);
        foreach (var a in lonAuths)
        {
            var days = (int)Math.Ceiling((a.ExpiryDate!.Value.Date - now.Date).TotalDays);
            var severity = days < 0 ? AlertSeverity.Critical
                : days <= 14 ? AlertSeverity.Critical
                : AlertSeverity.Warning;
            var title = days < 0
                ? $"LON auth {a.AuthorizationNumber} expired {-days}d ago"
                : $"LON auth {a.AuthorizationNumber} expires in {days}d";
            alerts.Add(new AlertRow(
                AlertCategory.LonAuthorizationExpiring, severity, title,
                "Approve renewal before flows block on this authorisation.",
                $"/customs/authorizations", a.ExpiryDate, null, null));
        }

        // Sort: Critical > Warning > Info, then by nearest date.
        var ordered = alerts
            .OrderByDescending(a => (int)a.Severity)
            .ThenBy(a => a.RelatedDate ?? DateTime.MaxValue)
            .ToList();

        return Result<AlertsFeed>.Success(new AlertsFeed(now, ordered));
    }
}
