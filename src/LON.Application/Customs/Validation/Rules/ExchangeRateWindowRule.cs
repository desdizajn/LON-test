using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Advisory: when the caller provides Box 23 ExchangeRate, it must fall
/// within ±<see cref="TolerancePercent"/> of the NBRM reference rate for
/// the declaration date. Skips silently when no provider rate is available
/// (weekends, holidays, untracked currencies) — real enforcement happens
/// at the customs portal.
/// </summary>
public class ExchangeRateWindowRule : IDeclarationRule
{
    public string RuleCode => "BOX23_EXCHANGE_RATE_WINDOW";
    public int Priority => 18;

    /// <summary>Allowed deviation from NBRM reference rate (20%).</summary>
    private const decimal TolerancePercent = 20m;

    private readonly IExchangeRateProvider _rates;

    public ExchangeRateWindowRule(IExchangeRateProvider rates)
    {
        _rates = rates;
    }

    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Box23_ExchangeRate",
            IsValid = true
        };

        // MKD declarations don't have an exchange rate; skip.
        if (string.Equals(declaration.Currency, "MKD", StringComparison.OrdinalIgnoreCase))
            return result;

        if (!declaration.ExchangeRate.HasValue || declaration.ExchangeRate.Value <= 0m)
            return result; // unset — nothing to compare

        var reference = await _rates.GetRateAsync(declaration.Currency, declaration.DeclarationDate, cancellationToken);
        if (reference is null || reference.Value <= 0m)
            return result; // provider unavailable — silent skip

        var deviation = Math.Abs(declaration.ExchangeRate.Value - reference.Value) / reference.Value * 100m;
        if (deviation > TolerancePercent)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message =
                    $"Box 23: Курс {declaration.ExchangeRate.Value} отстапува {deviation:0.##}% " +
                    $"од референтниот НБРМ курс {reference.Value} за {declaration.DeclarationDate:yyyy-MM-dd} " +
                    $"(дозволено ±{TolerancePercent}%).",
                ReferenceDocument = "НБРМ реферeнтен курс"
            });
        }

        return result;
    }
}
