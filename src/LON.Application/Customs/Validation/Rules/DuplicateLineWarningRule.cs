using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Advisory: flag two or more declaration lines that share the same
/// (ItemId, TariffCode, CountryOfOrigin) triple. Legitimate in some cases
/// (e.g. separate lots of the same item), but a common user mistake when
/// splitting an invoice in the UI — the warning prompts a sanity check.
/// </summary>
public class DuplicateLineWarningRule : IDeclarationRule
{
    public string RuleCode => "LINES_DUPLICATE_WARNING";
    public int Priority => 30;

    public Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Lines",
            IsValid = true
        };

        var groups = declaration.Lines
            .GroupBy(l => new
            {
                l.ItemId,
                TariffCode = (l.TariffCode ?? string.Empty).Trim(),
                Country = (l.CountryOfOrigin ?? string.Empty).Trim().ToUpperInvariant()
            })
            .Where(g => g.Count() > 1);

        foreach (var g in groups)
        {
            var lineNumbers = string.Join(", ", g.Select(l => l.LineNumber).OrderBy(n => n));
            result.Warnings.Add(new ValidationWarning
            {
                Message = $"Линии {lineNumbers}: ист Item + Box 33 + Box 34. Провери дали се дупликати.",
                ReferenceDocument = "Правилник"
            });
        }

        return Task.FromResult(result);
    }
}
