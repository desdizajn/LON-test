using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation;

/// <summary>
/// Интерфејс за валидациско правило за царинска декларација
/// </summary>
public interface IDeclarationRule
{
    /// <summary>
    /// Код на правилото (напр. BOX33_FORMAT, BOX40_TARIFF_MATCH)
    /// </summary>
    string RuleCode { get; }
    
    /// <summary>
    /// Приоритет (помал број = повисок приоритет)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Валидира декларација и враќа резултат
    /// </summary>
    Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Резултат од валидација на правило
/// </summary>
public class ValidationRuleResult
{
    public bool IsValid { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    
    public static ValidationRuleResult Success(string ruleCode, string fieldName)
    {
        return new ValidationRuleResult
        {
            IsValid = true,
            RuleCode = ruleCode,
            FieldName = fieldName
        };
    }
    
    public static ValidationRuleResult Failure(string ruleCode, string fieldName, string errorMessage, string? referenceDocument = null)
    {
        return new ValidationRuleResult
        {
            IsValid = false,
            RuleCode = ruleCode,
            FieldName = fieldName,
            Errors = new List<ValidationError>
            {
                new ValidationError
                {
                    Message = errorMessage,
                    ReferenceDocument = referenceDocument
                }
            }
        };
    }
}

/// <summary>
/// Валидациска грешка
/// </summary>
public class ValidationError
{
    public string Message { get; set; } = string.Empty;
    public string? ReferenceDocument { get; set; }
    public string? SuggestedValue { get; set; }
}

/// <summary>
/// Валидациско предупредување
/// </summary>
public class ValidationWarning
{
    public string Message { get; set; } = string.Empty;
    public string? ReferenceDocument { get; set; }
}
