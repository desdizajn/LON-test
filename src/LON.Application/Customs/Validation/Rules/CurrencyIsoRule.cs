using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 22 (Currency) мора да биде валиден ISO 4217 3-буквен код.
/// Листата е ограничена на валути дозволени од МК царина
/// (kb/processed/currencies_box22_iso.json, 38 кодови).
/// </summary>
public class CurrencyIsoRule : IDeclarationRule
{
    public string RuleCode => "BOX22_CURRENCY_ISO";
    public int Priority => 15;

    /// <summary>
    /// Subset of ISO 4217 currencies accepted by MK customs (Box 22).
    /// Mirrors `kb/processed/currencies_box22_iso.json`. EUR is the default.
    /// </summary>
    private static readonly HashSet<string> AllowedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "EUR", "USD", "GBP", "CHF", "AUD", "CAD", "JPY", "CNY", "MKD",
        "SEK", "NOK", "DKK", "PLN", "CZK", "HUF", "RON", "BGN", "HRK",
        "RSD", "TRY", "RUB", "UAH", "ILS", "KRW", "SGD", "HKD", "NZD",
        "ZAR", "AED", "SAR", "INR", "MXN", "BRL", "ARS", "CLP", "COP",
        "THB", "TWD"
    };

    public Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        const string fieldName = "Box22_Currency";

        if (string.IsNullOrWhiteSpace(declaration.Currency))
        {
            return Task.FromResult(ValidationRuleResult.Failure(
                RuleCode, fieldName,
                "Box 22: Валутата е задолжителна.",
                "Правилник, Член 19"));
        }

        if (!AllowedCurrencies.Contains(declaration.Currency))
        {
            return Task.FromResult(ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"Box 22: Валутата '{declaration.Currency}' не е од дозволените ISO 4217 кодови (пример: EUR, USD).",
                "Правилник, Прилог — listType=Currency"));
        }

        return Task.FromResult(ValidationRuleResult.Success(RuleCode, fieldName));
    }
}
