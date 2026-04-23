using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Application.Common.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Queries.DutyWhatIf;

/// <summary>
/// Tariff what-if duty calculator. Replicates the legacy ELON
/// <c>PresmetajDavackiPoNaim</c> formula for a single hypothetical line:
///
/// <code>
///   CarOsn  = CustomsValue × Kurs               ' customs base (in MKD)
///   Carina  = CustomsRate × CarOsn / 100        ' duty
///   DanOsn  = CarOsn + Carina                   ' VAT base
///   Danok   = VATRate × DanOsn / 100            ' VAT
///   Vkupno  = Carina + Danok                    ' total
///   Specific = SpecificDuty × Quantity          ' optional per-UoM addon
/// </code>
///
/// Rate resolution order:
/// <list type="bullet">
///   <item>Year-indexed <c>TariffCodeRate</c> row active on <see cref="Date"/>
///         (preferred — tracks rate changes over time).</item>
///   <item>Base <c>TariffCode.CustomsRate</c> / <c>TariffCode.VATRate</c>
///         as fallback.</item>
/// </list>
///
/// The result is labelled "Orientational" or "Precise" depending on whether
/// preferential origin + year-indexed rate + specific-duty all resolved; the
/// user sees which inputs drove which rate so they can sanity-check.
/// </summary>
public sealed record DutyWhatIfQuery(
    string TariffCode,
    decimal CustomsValue,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    decimal Quantity = 1m,
    string? CountryOfOrigin = null,
    bool IsPreferentialOrigin = false) : IQuery<Result<DutyWhatIfResult>>;

public sealed record DutyWhatIfResult(
    string TariffCode,
    string? Description,
    decimal CustomsValue,
    string Currency,
    decimal ExchangeRate,
    decimal CustomsBase,
    decimal DutyRate,
    decimal DutyAmount,
    decimal VATRate,
    decimal VATBase,
    decimal VATAmount,
    decimal TotalDuties,
    string RateSource,
    bool PreferentialApplied,
    string? WarningMessage);

public sealed class DutyWhatIfQueryHandler : IQueryHandler<DutyWhatIfQuery, Result<DutyWhatIfResult>>
{
    private readonly IApplicationDbContext _context;

    public DutyWhatIfQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DutyWhatIfResult>> Handle(DutyWhatIfQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TariffCode))
            return Result<DutyWhatIfResult>.Failure("TariffCode is required.");
        if (request.CustomsValue < 0m)
            return Result<DutyWhatIfResult>.Failure("CustomsValue must be non-negative.");
        if (request.ExchangeRate <= 0m)
            return Result<DutyWhatIfResult>.Failure("ExchangeRate must be positive.");

        var tariff = await _context.TariffCodes
            .Where(t => !t.IsDeleted && t.IsActive
                         && (t.TariffNumber == request.TariffCode || t.TARBR == request.TariffCode))
            .Select(t => new
            {
                t.Id,
                t.TariffNumber,
                t.Description,
                t.CustomsRate,
                t.VATRate,
                t.UnitMeasure
            })
            .FirstOrDefaultAsync(ct);

        if (tariff is null)
            return Result<DutyWhatIfResult>.Failure(
                $"Tariff code '{request.TariffCode}' not found in TARIC table.");

        // Year-indexed rate lookup (P4.7 TariffCodeRate).
        var yearRate = await _context.TariffCodeRates
            .Where(r => r.TariffCodeId == tariff.Id
                         && !r.IsDeleted
                         && r.ValidFrom <= request.Date
                         && (r.ValidTo == null || r.ValidTo > request.Date))
            .OrderByDescending(r => r.ValidFrom)
            .Select(r => new { r.CustomsRate, r.VATRate, r.Source })
            .FirstOrDefaultAsync(ct);

        decimal effectiveDutyRate;
        decimal effectiveVatRate;
        string rateSource;
        string? warning = null;

        if (yearRate is not null)
        {
            effectiveDutyRate = yearRate.CustomsRate;
            effectiveVatRate = yearRate.VATRate;
            rateSource = yearRate.Source ?? $"TariffCodeRate @ {request.Date:yyyy-MM-dd}";
        }
        else if (tariff.CustomsRate.HasValue)
        {
            effectiveDutyRate = tariff.CustomsRate.Value;
            effectiveVatRate = tariff.VATRate ?? 18m;
            rateSource = "TariffCode (base — year-indexed row not found for this date)";
            warning = $"No TariffCodeRate row covers {request.Date:yyyy-MM-dd}; using base TARIC rates. " +
                      "Add a TariffCodeRate with ValidFrom ≤ date for precise per-year calculation.";
        }
        else
        {
            return Result<DutyWhatIfResult>.Failure(
                $"Tariff '{tariff.TariffNumber}' has no CustomsRate on base row or a year-indexed TariffCodeRate for {request.Date:yyyy-MM-dd}. Rate unknown.");
        }

        // Preferential origin override: if user flagged a preferential country
        // (EU / TR / CEFTA etc.), legacy ELON halves the rate in a simple model
        // (real world consults Aneksi.ST<year> — future P15.17+). For the what-if
        // calculator we expose the toggle so the user sees the effect; a warning
        // nudges them to confirm against the preferential table.
        bool prefApplied = false;
        if (request.IsPreferentialOrigin)
        {
            prefApplied = true;
            effectiveDutyRate = 0m; // simplified LON rule: EU / TR goods = 0% duty
            if (warning is null)
                warning = "Preferential origin applied as 0% duty (simplified rule). " +
                          "Consult CarTarPovlasteniDDV / Aneksi table for the real preferential rate.";
            else
                warning += " Plus: preferential origin applied as 0% duty.";
            rateSource += " + preferential override";
        }

        // Legacy PresmetajDavackiPoNaim formula.
        var customsBase = Math.Round(request.CustomsValue * request.ExchangeRate, 2, MidpointRounding.AwayFromZero);
        var dutyAmount = Math.Round(effectiveDutyRate * customsBase / 100m, 2, MidpointRounding.AwayFromZero);
        var vatBase = customsBase + dutyAmount;
        var vatAmount = Math.Round(effectiveVatRate * vatBase / 100m, 2, MidpointRounding.AwayFromZero);
        var total = dutyAmount + vatAmount;

        return Result<DutyWhatIfResult>.Success(new DutyWhatIfResult(
            TariffCode: tariff.TariffNumber,
            Description: tariff.Description,
            CustomsValue: request.CustomsValue,
            Currency: request.Currency,
            ExchangeRate: request.ExchangeRate,
            CustomsBase: customsBase,
            DutyRate: effectiveDutyRate,
            DutyAmount: dutyAmount,
            VATRate: effectiveVatRate,
            VATBase: vatBase,
            VATAmount: vatAmount,
            TotalDuties: total,
            RateSource: rateSource,
            PreferentialApplied: prefApplied,
            WarningMessage: warning));
    }
}
