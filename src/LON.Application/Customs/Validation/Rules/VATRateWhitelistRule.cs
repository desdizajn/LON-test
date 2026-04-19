using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Advisory: Box 47 VAT rate should be one of the three MK rates:
/// 0% (exempt), 5% (reduced), 18% (standard). Other values pass through
/// as warnings so customs-portal validation has the final say, but users
/// get an early heads-up if they've entered e.g. an outdated 10% rate.
/// </summary>
public class VATRateWhitelistRule : IDeclarationRule
{
    public string RuleCode => "BOX47_VAT_RATE_WHITELIST";
    public int Priority => 14;

    /// <summary>Rates currently in effect under MK ЗДДВ.</summary>
    private static readonly HashSet<decimal> AllowedRates = new() { 0m, 5m, 18m };

    public Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Box47_VATRate",
            IsValid = true
        };

        foreach (var line in declaration.Lines)
        {
            if (!AllowedRates.Contains(line.VATRate))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Линија {line.LineNumber}: VAT стапка {line.VATRate}% не е од стандардните MK стапки (0%, 5%, 18%).",
                    ReferenceDocument = "ЗДДВ, Член 30"
                });
            }
        }

        return Task.FromResult(result);
    }
}
