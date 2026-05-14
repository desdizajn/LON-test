using LON.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Finance.FxRates;

/// <summary>
/// Phase 17 §E16 — default <see cref="IFxRateService"/> implementation,
/// pure-EF over <c>FxRates</c>. Falls through inverse and cross-via-EUR
/// before giving up.
/// </summary>
public sealed class FxRateService : IFxRateService
{
    private readonly IApplicationDbContext _context;

    public FxRateService(IApplicationDbContext context) => _context = context;

    public async Task<decimal> GetRateAsync(string from, string to, DateTime asOf, CancellationToken ct = default)
    {
        var fromCcy = (from ?? string.Empty).Trim().ToUpperInvariant();
        var toCcy = (to ?? string.Empty).Trim().ToUpperInvariant();
        if (fromCcy.Length != 3 || toCcy.Length != 3)
            throw new ArgumentException("Currency must be 3-char ISO code.");
        if (fromCcy == toCcy) return 1m;

        var direct = await LookupAsync(fromCcy, toCcy, asOf, ct);
        if (direct.HasValue) return direct.Value;

        var inverse = await LookupAsync(toCcy, fromCcy, asOf, ct);
        if (inverse.HasValue && inverse.Value != 0m) return 1m / inverse.Value;

        // Cross via EUR (the canonical pivot for Macedonian Denar workflows).
        if (fromCcy != "EUR" && toCcy != "EUR")
        {
            var fromEur = await LookupAsync(fromCcy, "EUR", asOf, ct);
            var eurTo = await LookupAsync("EUR", toCcy, asOf, ct);
            if (fromEur.HasValue && eurTo.HasValue) return fromEur.Value * eurTo.Value;

            var eurFrom = await LookupAsync("EUR", fromCcy, asOf, ct);
            var toEur = await LookupAsync(toCcy, "EUR", asOf, ct);
            if (eurFrom.HasValue && eurFrom.Value != 0m && toEur.HasValue && toEur.Value != 0m)
                return (1m / eurFrom.Value) * (1m / toEur.Value);
        }

        throw new FxRateMissingException(fromCcy, toCcy, asOf);
    }

    private async Task<decimal?> LookupAsync(string from, string to, DateTime asOf, CancellationToken ct)
    {
        return await _context.FxRates
            .Where(r => r.FromCurrency == from && r.ToCurrency == to && r.EffectiveDate <= asOf && !r.IsDeleted)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(ct);
    }
}
