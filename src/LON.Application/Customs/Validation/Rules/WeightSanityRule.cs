using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 35 (Gross) и Box 38 (Net) weight sanity.
/// Hard-error variant of the advisory check in <see cref="SadFieldAdvisoriesRule"/>:
/// - Negative values are rejected (data integrity).
/// - Zero is rejected when the field is explicitly provided (use null for "unspecified").
/// - NetWeight &gt; GrossWeight is rejected (logically impossible).
/// Missing values stay advisory via <see cref="SadFieldAdvisoriesRule"/>.
/// </summary>
public class WeightSanityRule : IDeclarationRule
{
    public string RuleCode => "BOX35_38_WEIGHT_SANITY";
    public int Priority => 13;

    public Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Box35_38_Weight",
            IsValid = true
        };

        foreach (var line in declaration.Lines)
        {
            if (line.GrossWeight.HasValue && line.GrossWeight.Value < 0m)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Линија {line.LineNumber}: Бруто маса не може да биде негативна ({line.GrossWeight.Value}).",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }
            if (line.NetWeight.HasValue && line.NetWeight.Value < 0m)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Линија {line.LineNumber}: Нето маса не може да биде негативна ({line.NetWeight.Value}).",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }

            if (line.GrossWeight.HasValue && line.GrossWeight.Value == 0m)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Линија {line.LineNumber}: Бруто маса е нула — остави null ако е непозната.",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }
            if (line.NetWeight.HasValue && line.NetWeight.Value == 0m)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Линија {line.LineNumber}: Нето маса е нула — остави null ако е непозната.",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }

            // Strict ordering: net must not exceed gross when both are set.
            // (SadFieldAdvisoriesRule still surfaces the "equal or close" soft case
            // as advisory; this one catches the outright inverted pair.)
            if (line.GrossWeight.HasValue && line.NetWeight.HasValue
                && line.NetWeight.Value > line.GrossWeight.Value)
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Линија {line.LineNumber}: Нето маса ({line.NetWeight.Value}) не може да биде поголема од бруто маса ({line.GrossWeight.Value}).",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }
        }

        return Task.FromResult(result);
    }
}
