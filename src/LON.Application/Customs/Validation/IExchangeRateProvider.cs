namespace LON.Application.Customs.Validation;

/// <summary>
/// Abstraction over the NBRM reference-rate feed (or equivalent) used by
/// the exchange-rate-window rule. A real implementation will call the NBRM
/// middle-rate endpoint; tests + dev environments use <see cref="NullExchangeRateProvider"/>.
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Returns the reference rate for 1 unit of <paramref name="currency"/> in
    /// macedonian denar (MKD) on the given date, or <c>null</c> if the rate is
    /// not available (weekend, holiday, currency not published).
    /// </summary>
    Task<decimal?> GetRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stub provider registered in DI by default. Always returns <c>null</c> so
/// the rule engine skips exchange-rate-window checks until a real provider
/// is wired up. Swapping in the real provider is a single DI line change.
/// </summary>
public sealed class NullExchangeRateProvider : IExchangeRateProvider
{
    public Task<decimal?> GetRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default)
        => Task.FromResult<decimal?>(null);
}
