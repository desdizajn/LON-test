using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Soft advisories за SAD box полиња што Правилник ги бара но можат да се
/// пополнат post-hoc: Box 30 (LocationOfGoods), Box 35 (GrossWeight), и
/// Box 47 (CalculationMethod). Warning-от не ја блокира декларацијата —
/// ја означува за преглед пред submit.
/// </summary>
public class SadFieldAdvisoriesRule : IDeclarationRule
{
    public string RuleCode => "SAD_FIELD_ADVISORIES";
    public int Priority => 12;

    public Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Multiple",
            IsValid = true
        };

        if (string.IsNullOrWhiteSpace(declaration.LocationOfGoods))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Рубрика 30: Место на стока не е пополнето. Пополни пред submit на царина.",
                ReferenceDocument = "Правилник, Член 12"
            });
        }

        foreach (var line in declaration.Lines)
        {
            if (!line.GrossWeight.HasValue || line.GrossWeight.Value <= 0m)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Рубрика 35 (Линија {line.LineNumber}): Бруто маса не е пополнета.",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }
            else if (line.NetWeight.HasValue && line.GrossWeight.Value < line.NetWeight.Value)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Линија {line.LineNumber}: Бруто маса ({line.GrossWeight.Value}) е помала од нето ({line.NetWeight.Value}) — проверка потребна.",
                    ReferenceDocument = "Правилник"
                });
            }

            if (string.IsNullOrWhiteSpace(line.CalculationMethod))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Рубрика 47 (Линија {line.LineNumber}): Начин на пресметка не е означен.",
                    ReferenceDocument = "Правилник, Член 17"
                });
            }
        }

        return Task.FromResult(result);
    }
}
