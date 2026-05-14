namespace LON.Application.Finance.FxRates;

/// <summary>
/// Phase 17 §E16 — point-in-time FX lookup. Used by Invoice / margin /
/// CustomsDeclaration valuation paths that need to convert into the
/// tenant's primary currency.
///
/// Resolution order:
///   1. Exact (from, to) pair with the latest <c>EffectiveDate ≤ asOf</c>.
///   2. Inverse pair: 1 / rate(to, from).
///   3. Cross via EUR: rate(from, EUR) × rate(EUR, to).
///   4. Otherwise throws <see cref="FxRateMissingException"/>.
/// </summary>
public interface IFxRateService
{
    /// <summary>Returns 1.0 when <paramref name="from"/> == <paramref name="to"/>.</summary>
    Task<decimal> GetRateAsync(string from, string to, DateTime asOf, CancellationToken ct = default);
}

public sealed class FxRateMissingException : Exception
{
    public FxRateMissingException(string from, string to, DateTime asOf)
        : base($"No FX rate {from}→{to} effective on or before {asOf:yyyy-MM-dd}.") { }
}
