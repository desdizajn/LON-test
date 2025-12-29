using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation;

/// <summary>
/// Сервис за валидација на царински декларации
/// </summary>
public interface IDeclarationRuleEngine
{
    Task<DeclarationValidationResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Имплементација на Rule Engine
/// </summary>
public class DeclarationRuleEngine : IDeclarationRuleEngine
{
    private readonly IEnumerable<IDeclarationRule> _rules;
    private readonly IApplicationDbContext _context;
    
    public DeclarationRuleEngine(
        IEnumerable<IDeclarationRule> rules,
        IApplicationDbContext context)
    {
        _rules = rules;
        _context = context;
    }
    
    public async Task<DeclarationValidationResult> ValidateAsync(
        CustomsDeclaration declaration, 
        CancellationToken cancellationToken = default)
    {
        var result = new DeclarationValidationResult
        {
            DeclarationId = declaration.Id,
            IsValid = true,
            ValidationTime = DateTime.UtcNow
        };
        
        // Сортирај правила по приоритет
        var sortedRules = _rules.OrderBy(r => r.Priority);
        
        foreach (var rule in sortedRules)
        {
            var ruleResult = await rule.ValidateAsync(declaration, cancellationToken);
            
            result.RuleResults.Add(ruleResult);
            
            if (!ruleResult.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(ruleResult.Errors);
            }
            
            result.Warnings.AddRange(ruleResult.Warnings);
        }
        
        return result;
    }
}

/// <summary>
/// Резултат од валидација на целата декларација
/// </summary>
public class DeclarationValidationResult
{
    public Guid DeclarationId { get; set; }
    public bool IsValid { get; set; }
    public DateTime ValidationTime { get; set; }
    public List<ValidationRuleResult> RuleResults { get; set; } = new();
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    
    /// <summary>
    /// Враќа само грешките (не вклучува warnings)
    /// </summary>
    public List<string> GetErrorMessages()
    {
        return Errors.Select(e => e.Message).ToList();
    }
    
    /// <summary>
    /// Враќа summary за UI
    /// </summary>
    public string GetSummary()
    {
        if (IsValid)
        {
            return $"✅ Валидацијата е успешна. Проверени {RuleResults.Count} правила.";
        }
        
        var errorCount = Errors.Count;
        var warningCount = Warnings.Count;
        
        return $"❌ Валидацијата не е успешна. {errorCount} грешки, {warningCount} предупредувања.";
    }
}
