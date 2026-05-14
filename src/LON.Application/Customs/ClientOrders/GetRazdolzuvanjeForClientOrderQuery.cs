using LON.Application.Common.Interfaces;
using LON.Application.Common.Queries;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// Phase 17 §E9 — Razdolzuvanje view scoped to a single ClientOrder.
///
/// Aggregates IM duty charged on the order's import declarations vs.
/// duty credited via EX / Waste / Return discharges (the latter joined by
/// <c>CustomsDeclarationLine.PreviousMRN</c> back to the IM lines). Returns
/// a side-by-side header + per-line breakdown so the hub view can render
/// the legacy <c>frmRazdolzuvanjeZak</c> layout.
///
/// Tolerance for variance is <see cref="DefaultToleranceEur"/> (€0.50 per
/// BLUEPRINT §5.11 / §6.1) — used purely for the UI flag; the actual
/// transition to <see cref="Domain.Enums.ClientOrderStatus.Closed"/> is
/// guarded by the same tolerance inside <c>TakeRazdolzuvanjeSnapshotCommand</c>.
/// </summary>
public sealed record GetRazdolzuvanjeForClientOrderQuery(Guid ClientOrderId)
    : IQuery<RazdolzuvanjeForClientOrderDto>;

public sealed record RazdolzuvanjeForClientOrderDto
{
    public Guid ClientOrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public int Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public Guid LONAuthorizationId { get; init; }
    public string? AuthorizationNumber { get; init; }

    /// <summary>Σ TotalDuty over all IM declarations linked to this ClientOrder.</summary>
    public decimal TotalImDuty { get; init; }

    /// <summary>Σ TotalDuty over all EX declarations linked to this ClientOrder.</summary>
    public decimal TotalExDuty { get; init; }

    /// <summary>Σ TotalDuty over all Waste/Return declarations whose lines reference an IM line on this order.</summary>
    public decimal TotalWasteDuty { get; init; }
    public decimal TotalReturnDuty { get; init; }

    /// <summary>Sum of EX + Waste + Return duty credited (informational).</summary>
    public decimal TotalCredited { get; init; }

    /// <summary>IM duty − Credited duty. ≤ tolerance ⇒ reconciled.</summary>
    public decimal Variance { get; init; }

    /// <summary>Per BLUEPRINT §5.11 tolerance for variance (€0.50 default).</summary>
    public decimal ToleranceEur { get; init; }

    public bool IsReconciled { get; init; }

    public int TotalLines { get; init; }
    public int LinesRazdolzeno { get; init; }
    public bool AllLinesFlagged { get; init; }

    /// <summary>Per-CustomsDeclarationLine breakdown (IM lines only — they
    /// carry the bond-debit + RazdolzenaDaNe flag).</summary>
    public List<RazdolzuvanjeLineDto> Lines { get; init; } = new();
}

public sealed record RazdolzuvanjeLineDto
{
    public Guid LineId { get; init; }
    public Guid DeclarationId { get; init; }
    public string DeclarationNumber { get; init; } = string.Empty;
    public string DeclarationType { get; init; } = string.Empty;
    public string MRN { get; init; } = string.Empty;
    public DateTime DeclarationDate { get; init; }
    public int LineNumber { get; init; }
    public string? ItemCode { get; init; }
    public string? ItemName { get; init; }
    public decimal Quantity { get; init; }
    public string? UoMCode { get; init; }
    public decimal DutyAmount { get; init; }
    public decimal VATAmount { get; init; }
    public bool RazdolzenaDaNe { get; init; }
    public DateTime? RazdolzenaAt { get; init; }
    public string? RazdolzenaBy { get; init; }
}

public sealed class GetRazdolzuvanjeForClientOrderQueryHandler
    : IQueryHandler<GetRazdolzuvanjeForClientOrderQuery, RazdolzuvanjeForClientOrderDto>
{
    public const decimal DefaultToleranceEur = 0.50m;

    private readonly IApplicationDbContext _context;

    public GetRazdolzuvanjeForClientOrderQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RazdolzuvanjeForClientOrderDto> Handle(
        GetRazdolzuvanjeForClientOrderQuery request, CancellationToken ct)
    {
        // §E9 — the parent CO may be Closed (terminal status, IsDeleted=false)
        // OR Cancelled (IsDeleted=true). Both still need a razdolzuvanje view
        // for historical reference, so we explicitly bypass the soft-delete
        // filter here. Tenant scope still applies via the same query filter.
        var order = await _context.ClientOrders
            .IgnoreQueryFilters()
            .Where(o => _context.CurrentTenantId == null || o.TenantId == _context.CurrentTenantId)
            .Include(o => o.LONAuthorization)
            .FirstOrDefaultAsync(o => o.Id == request.ClientOrderId, ct);
        if (order is null)
            return new RazdolzuvanjeForClientOrderDto
            {
                ClientOrderId = request.ClientOrderId,
                OrderNumber = "(not found)",
                ToleranceEur = DefaultToleranceEur,
            };

        // Every customs declaration tied back to this ClientOrder.
        var declarations = await _context.CustomsDeclarations
            .Include(d => d.Lines)
                .ThenInclude(l => l.Item)
            .Include(d => d.Lines)
                .ThenInclude(l => l.UoM)
            .Where(d => d.ClientOrderId == request.ClientOrderId && !d.IsDeleted)
            .ToListAsync(ct);

        decimal totalIm = 0m, totalEx = 0m, totalWaste = 0m, totalReturn = 0m;
        var lineRows = new List<RazdolzuvanjeLineDto>();
        int linesRazdolzeno = 0;
        int totalLines = 0;
        var imMrns = new List<string>();

        foreach (var d in declarations.OrderBy(d => d.DeclarationDate).ThenBy(d => d.DeclarationNumber))
        {
            var type = d.DeclarationType?.Trim() ?? string.Empty;
            switch (type)
            {
                case "IM":
                    totalIm += d.TotalDuty;
                    if (!string.IsNullOrWhiteSpace(d.MRN)) imMrns.Add(d.MRN);
                    break;
                case "EX":
                    totalEx += d.TotalDuty;
                    break;
                case "Waste":
                    totalWaste += d.TotalDuty;
                    break;
                case "Return":
                    totalReturn += d.TotalDuty;
                    break;
            }

            // Render per-IM-line rows. EX/Waste/Return lines aren't shown
            // individually; their totals roll up into the credit columns.
            if (type == "IM")
            {
                foreach (var l in d.Lines.OrderBy(l => l.LineNumber))
                {
                    totalLines++;
                    if (l.RazdolzenaDaNe) linesRazdolzeno++;
                    lineRows.Add(new RazdolzuvanjeLineDto
                    {
                        LineId = l.Id,
                        DeclarationId = d.Id,
                        DeclarationNumber = d.DeclarationNumber,
                        DeclarationType = type,
                        MRN = d.MRN,
                        DeclarationDate = d.DeclarationDate,
                        LineNumber = l.LineNumber,
                        ItemCode = l.Item?.Code,
                        ItemName = l.Item?.Name,
                        Quantity = l.Quantity,
                        UoMCode = l.UoM?.Code,
                        DutyAmount = l.DutyAmount,
                        VATAmount = l.VATAmount,
                        RazdolzenaDaNe = l.RazdolzenaDaNe,
                        RazdolzenaAt = l.RazdolzenaAt,
                        RazdolzenaBy = l.RazdolzenaBy,
                    });
                }
            }
        }

        // If no waste declarations are FK-linked to the order, also fold in any
        // Waste/Return declarations whose lines reference an IM MRN we just
        // collected (legacy data may not have ClientOrderId stamped). One round
        // trip; the IM MRN list is small (<20 typical).
        if (imMrns.Count > 0)
        {
            var orphanCredits = await _context.CustomsDeclarationLines
                .Include(l => l.CustomsDeclaration)
                .Where(l => !l.IsDeleted
                            && l.PreviousMRN != null
                            && imMrns.Contains(l.PreviousMRN)
                            && (l.CustomsDeclaration.DeclarationType == "Waste"
                                || l.CustomsDeclaration.DeclarationType == "Return"
                                || l.CustomsDeclaration.DeclarationType == "EX")
                            && l.CustomsDeclaration.ClientOrderId != request.ClientOrderId
                            && !l.CustomsDeclaration.IsDeleted)
                .GroupBy(l => l.CustomsDeclaration.DeclarationType)
                .Select(g => new { Type = g.Key, Sum = g.Sum(l => l.DutyAmount) })
                .ToListAsync(ct);

            foreach (var oc in orphanCredits)
            {
                switch (oc.Type)
                {
                    case "EX": totalEx += oc.Sum; break;
                    case "Waste": totalWaste += oc.Sum; break;
                    case "Return": totalReturn += oc.Sum; break;
                }
            }
        }

        var credited = totalEx + totalWaste + totalReturn;
        var variance = totalIm - credited;
        var isReconciled = Math.Abs(variance) <= DefaultToleranceEur;

        return new RazdolzuvanjeForClientOrderDto
        {
            ClientOrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = (int)order.Status,
            StatusName = order.Status.ToString(),
            LONAuthorizationId = order.LONAuthorizationId,
            AuthorizationNumber = order.LONAuthorization?.AuthorizationNumber,
            TotalImDuty = totalIm,
            TotalExDuty = totalEx,
            TotalWasteDuty = totalWaste,
            TotalReturnDuty = totalReturn,
            TotalCredited = credited,
            Variance = variance,
            ToleranceEur = DefaultToleranceEur,
            IsReconciled = isReconciled,
            TotalLines = totalLines,
            LinesRazdolzeno = linesRazdolzeno,
            AllLinesFlagged = totalLines > 0 && linesRazdolzeno == totalLines,
            Lines = lineRows,
        };
    }
}
