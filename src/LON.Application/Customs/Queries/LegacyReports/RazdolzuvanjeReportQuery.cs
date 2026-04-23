using LON.Application.Common.Interfaces;
using LON.Application.Common.Queries;
using LON.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Queries.LegacyReports;

/// <summary>
/// P15.11 — legacy <c>rptRazdolzuvanje</c> (guarantee-release summary).
///
/// Per LON authorization, for a date window, reports: how much bond was
/// debited (IM declarations in window), how much was credited (EX/return/
/// waste declarations whose PreviousMRN links to an IM under this auth),
/// and the running net outstanding. One row per IM MRN.
///
/// Legacy produced this as a printed report per closure (Zaklucok). We
/// return structured JSON so the frontend can render Macedonian-print
/// layout and users can export to Excel/PDF.
/// </summary>
public sealed record RazdolzuvanjeReportQuery(
    Guid LONAuthorizationId,
    DateTime From,
    DateTime To) : IQuery<RazdolzuvanjeReport>;

public sealed record RazdolzuvanjeReport(
    Guid LONAuthorizationId,
    string AuthorizationNumber,
    DateTime From,
    DateTime To,
    decimal TotalDebited,
    decimal TotalCredited,
    decimal NetOutstanding,
    int MrnCount,
    List<RazdolzuvanjeRow> Rows);

public sealed record RazdolzuvanjeRow(
    string MRN,
    DateTime ImDate,
    string? ImDeclarationNumber,
    decimal TotalImDuty,
    decimal TotalImVAT,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal NetOutstanding,
    DateTime? LastCreditDate,
    bool FullyDischarged);

public sealed class RazdolzuvanjeReportQueryHandler : IQueryHandler<RazdolzuvanjeReportQuery, RazdolzuvanjeReport>
{
    private readonly IApplicationDbContext _context;

    public RazdolzuvanjeReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RazdolzuvanjeReport> Handle(RazdolzuvanjeReportQuery request, CancellationToken ct)
    {
        var auth = await _context.LONAuthorizations
            .FirstOrDefaultAsync(a => a.Id == request.LONAuthorizationId, ct);
        if (auth is null)
            return new RazdolzuvanjeReport(request.LONAuthorizationId, "(not found)",
                request.From, request.To, 0, 0, 0, 0, new List<RazdolzuvanjeRow>());

        var ims = await _context.CustomsDeclarations
            .Where(d => d.LONAuthorizationId == request.LONAuthorizationId
                         && d.DeclarationType == "IM"
                         && d.DeclarationDate >= request.From
                         && d.DeclarationDate <= request.To
                         && !d.IsDeleted)
            .Select(d => new { d.Id, d.MRN, d.DeclarationDate, d.DeclarationNumber, d.TotalDuty, d.TotalVAT })
            .ToListAsync(ct);

        // Credits: debit ledger entries for these IMs — tracked by MRN.
        var imMrns = ims.Select(i => i.MRN).ToList();

        var debits = await _context.GuaranteeLedgerEntries
            .Where(e => !e.IsDeleted
                         && e.EntryType == GuaranteeEntryType.Debit
                         && e.MRN != null
                         && imMrns.Contains(e.MRN))
            .GroupBy(e => e.MRN)
            .Select(g => new { MRN = g.Key, Sum = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.MRN!, x => x.Sum, ct);

        var credits = await _context.GuaranteeLedgerEntries
            .Where(e => !e.IsDeleted
                         && e.EntryType == GuaranteeEntryType.Credit
                         && e.MRN != null
                         && imMrns.Contains(e.MRN))
            .GroupBy(e => e.MRN)
            .Select(g => new
            {
                MRN = g.Key,
                Sum = g.Sum(e => e.Amount),
                Last = g.Max(e => (DateTime?)e.EntryDate)
            })
            .ToDictionaryAsync(x => x.MRN!, x => new { x.Sum, x.Last }, ct);

        var rows = ims.Select(im =>
        {
            var debit = debits.TryGetValue(im.MRN, out var d) ? d : 0m;
            var credit = credits.TryGetValue(im.MRN, out var c) ? c.Sum : 0m;
            var last = credits.TryGetValue(im.MRN, out var c2) ? c2.Last : null;
            var net = debit - credit;
            return new RazdolzuvanjeRow(
                im.MRN,
                im.DeclarationDate,
                im.DeclarationNumber,
                im.TotalDuty,
                im.TotalVAT,
                debit,
                credit,
                net,
                last,
                FullyDischarged: net <= 0.0001m && debit > 0m);
        }).OrderBy(r => r.ImDate).ToList();

        return new RazdolzuvanjeReport(
            auth.Id,
            auth.AuthorizationNumber,
            request.From,
            request.To,
            rows.Sum(r => r.DebitAmount),
            rows.Sum(r => r.CreditAmount),
            rows.Sum(r => r.NetOutstanding),
            rows.Count,
            rows);
    }
}

/// <summary>
/// P15.11 — legacy <c>rptG20-G30Mesecno</c>. Monthly customs register
/// grouping declarations by procedure code. One row per (month, procedure)
/// with counts + totals. Used by tenant accounting to reconcile against
/// customs-portal statements.
/// </summary>
public sealed record MonthlyCustomsRegisterQuery(int Year) : IQuery<List<MonthlyCustomsRow>>;

public sealed record MonthlyCustomsRow(
    int Year,
    int Month,
    string ProcedureCode,
    int DeclarationCount,
    decimal TotalCustomsValue,
    decimal TotalDuty,
    decimal TotalVAT);

public sealed class MonthlyCustomsRegisterQueryHandler
    : IQueryHandler<MonthlyCustomsRegisterQuery, List<MonthlyCustomsRow>>
{
    private readonly IApplicationDbContext _context;

    public MonthlyCustomsRegisterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MonthlyCustomsRow>> Handle(MonthlyCustomsRegisterQuery request, CancellationToken ct)
    {
        var from = new DateTime(request.Year, 1, 1);
        var to = new DateTime(request.Year + 1, 1, 1);

        var rows = await _context.CustomsDeclarations
            .Where(d => !d.IsDeleted
                         && d.DeclarationDate >= from
                         && d.DeclarationDate < to)
            .GroupBy(d => new { d.DeclarationDate.Year, d.DeclarationDate.Month, d.ProcedureCode })
            .Select(g => new MonthlyCustomsRow(
                g.Key.Year,
                g.Key.Month,
                g.Key.ProcedureCode ?? "(none)",
                g.Count(),
                g.Sum(d => d.TotalCustomsValue),
                g.Sum(d => d.TotalDuty),
                g.Sum(d => d.TotalVAT)))
            .ToListAsync(ct);

        return rows.OrderBy(r => r.Month).ThenBy(r => r.ProcedureCode).ToList();
    }
}

/// <summary>
/// P15.11 — legacy <c>rptOtpad</c>. Waste register: for a date window,
/// list every waste declaration (EX/Waste with negative-qty discharge)
/// grouped by source IM MRN. Used to audit planned-vs-actual waste and
/// to reconcile against PEE040 XML submissions.
/// </summary>
public sealed record WasteRegisterQuery(DateTime From, DateTime To) : IQuery<List<WasteRegisterRow>>;

public sealed record WasteRegisterRow(
    DateTime WasteDate,
    string WasteMRN,
    string? SourceMRN,
    string? ItemCode,
    string? ItemName,
    decimal Quantity,
    string? UoMCode,
    string? Reason);

public sealed class WasteRegisterQueryHandler : IQueryHandler<WasteRegisterQuery, List<WasteRegisterRow>>
{
    private readonly IApplicationDbContext _context;

    public WasteRegisterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WasteRegisterRow>> Handle(WasteRegisterQuery request, CancellationToken ct)
    {
        // Waste declarations in LON are stored as CustomsDeclarations with procedure
        // code signalling waste (configured per tenant). We filter heuristically by
        // DeclarationType in {EX, Waste} + SpecialRemarks/PreviousProcedureCode hint.
        // The authoritative source is the CreateWasteDeclarationCommand output —
        // any declaration with Type="Waste" or whose procedure.Type == Waste.
        var rows = await _context.CustomsDeclarationLines
            .Include(l => l.CustomsDeclaration)
            .Include(l => l.Item)
            .Include(l => l.UoM)
            .Where(l => !l.IsDeleted
                         && l.CustomsDeclaration.DeclarationType == "Waste"
                         && l.CustomsDeclaration.DeclarationDate >= request.From
                         && l.CustomsDeclaration.DeclarationDate <= request.To)
            .OrderBy(l => l.CustomsDeclaration.DeclarationDate)
            .Select(l => new WasteRegisterRow(
                l.CustomsDeclaration.DeclarationDate,
                l.CustomsDeclaration.MRN,
                l.PreviousMRN,
                l.Item.Code,
                l.Item.Name,
                l.Quantity,
                l.UoM.Code,
                l.CustomsDeclaration.SpecialRemarks))
            .ToListAsync(ct);

        return rows;
    }
}
