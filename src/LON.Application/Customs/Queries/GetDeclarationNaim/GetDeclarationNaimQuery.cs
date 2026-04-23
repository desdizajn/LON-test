using LON.Application.Common.Interfaces;
using LON.Application.Common.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Queries.GetDeclarationNaim;

/// <summary>
/// P15.4 — legacy <c>NaimU5</c> rollup (<c>cmdVnesiNaim_Click</c>
/// + <c>cmdFormiraj_Click</c> in ELON). Groups every
/// <see cref="Domain.Entities.Customs.CustomsDeclarationLine"/> of a
/// declaration by the (TariffCode, UoM, CountryOfOrigin) triple and
/// returns one aggregated "naimenovanie" row per group with summed
/// quantity, customs value, weights, duty, VAT, and other charges.
///
/// <para>Used by:</para>
/// <list type="bullet">
///   <item>PEE060 XML row assembly (monthly Zadolzuvanje/Razdolzuvanje).</item>
///   <item>Customs register printouts that must present one line per
///         tariff×origin instead of one line per invoice item.</item>
///   <item>The declaration-detail UI under a "Наименованија" tab so the
///         operator can see the grouped view the customs officer will see.</item>
/// </list>
///
/// <para>Groups are numbered sequentially (NaimRBr 1..N) in the same
/// order ELON assigns them: DISTINCT on the triple, ordered by TariffCode,
/// then UoM, then Country.</para>
/// </summary>
public sealed record GetDeclarationNaimQuery(Guid DeclarationId) : IQuery<List<NaimRow>>;

public sealed record NaimRow(
    int NaimNumber,
    string? TariffCode,
    Guid UoMId,
    string UoMCode,
    string? CountryOfOrigin,
    decimal TotalQuantity,
    decimal TotalCustomsValue,
    decimal? TotalGrossWeight,
    decimal? TotalNetWeight,
    decimal TotalDutyAmount,
    decimal TotalVATAmount,
    decimal TotalOtherCharges,
    decimal WeightedAverageDutyRate,
    decimal WeightedAverageVATRate,
    int LineCount,
    List<int> LineNumbers);

public sealed class GetDeclarationNaimQueryHandler
    : IQueryHandler<GetDeclarationNaimQuery, List<NaimRow>>
{
    private readonly IApplicationDbContext _context;

    public GetDeclarationNaimQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<NaimRow>> Handle(GetDeclarationNaimQuery request, CancellationToken ct)
    {
        var lines = await _context.CustomsDeclarationLines
            .Include(l => l.UoM)
            .Where(l => l.CustomsDeclarationId == request.DeclarationId && !l.IsDeleted)
            .ToListAsync(ct);

        if (lines.Count == 0) return new List<NaimRow>();

        // Legacy grouping key: (TariffCode, EdMerCar, ZemjaPoteklo).
        // Nulls are normalised to empty string for stable grouping; the DTO
        // keeps the original null to preserve semantics downstream.
        var groups = lines
            .GroupBy(l => new
            {
                Tariff = l.TariffCode ?? "",
                Uom = l.UoMId,
                UomCode = l.UoM != null ? l.UoM.Code : "",
                Country = l.CountryOfOrigin ?? ""
            })
            .OrderBy(g => g.Key.Tariff)
            .ThenBy(g => g.Key.UomCode)
            .ThenBy(g => g.Key.Country)
            .ToList();

        var result = new List<NaimRow>(groups.Count);
        int naim = 1;
        foreach (var g in groups)
        {
            var customsValue = g.Sum(l => l.CustomsValue);
            // Weighted average: Σ(rate × value) / Σ(value). Falls back to
            // simple mean when total value is zero (edge case, zero-value
            // sample lines).
            decimal weightedDuty = customsValue > 0m
                ? Math.Round(g.Sum(l => l.DutyRate * l.CustomsValue) / customsValue, 4, MidpointRounding.AwayFromZero)
                : (g.Count() > 0 ? g.Average(l => l.DutyRate) : 0m);
            decimal weightedVat = customsValue > 0m
                ? Math.Round(g.Sum(l => l.VATRate * l.CustomsValue) / customsValue, 4, MidpointRounding.AwayFromZero)
                : (g.Count() > 0 ? g.Average(l => l.VATRate) : 0m);

            result.Add(new NaimRow(
                NaimNumber: naim++,
                TariffCode: string.IsNullOrEmpty(g.Key.Tariff) ? null : g.Key.Tariff,
                UoMId: g.Key.Uom,
                UoMCode: g.Key.UomCode,
                CountryOfOrigin: string.IsNullOrEmpty(g.Key.Country) ? null : g.Key.Country,
                TotalQuantity: g.Sum(l => l.Quantity),
                TotalCustomsValue: customsValue,
                TotalGrossWeight: g.Any(l => l.GrossWeight.HasValue) ? g.Sum(l => l.GrossWeight ?? 0m) : (decimal?)null,
                TotalNetWeight: g.Any(l => l.NetWeight.HasValue) ? g.Sum(l => l.NetWeight ?? 0m) : (decimal?)null,
                TotalDutyAmount: g.Sum(l => l.DutyAmount),
                TotalVATAmount: g.Sum(l => l.VATAmount),
                TotalOtherCharges: g.Sum(l => l.OtherCharges),
                WeightedAverageDutyRate: weightedDuty,
                WeightedAverageVATRate: weightedVat,
                LineCount: g.Count(),
                LineNumbers: g.Select(l => l.LineNumber).OrderBy(n => n).ToList()));
        }

        return result;
    }
}
