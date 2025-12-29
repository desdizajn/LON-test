using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 33 (TariffCode) мора да биде валиден 10-цифрен TARIC код
/// </summary>
public class TariffCodeFormatRule : IDeclarationRule
{
    private readonly IApplicationDbContext _context;
    
    public string RuleCode => "BOX33_FORMAT";
    public int Priority => 15;
    
    public TariffCodeFormatRule(IApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Box33_TariffCode",
            IsValid = true
        };
        
        foreach (var line in declaration.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.TariffCode))
            {
                continue; // Ќе го фати RequiredFieldsRule
            }
            
            // Проверка на формат: Точно 10 цифри
            if (!System.Text.RegularExpressions.Regex.IsMatch(line.TariffCode, @"^\d{10}$"))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Box 33 (Линија {line.LineNumber}): Тарифната ознака '{line.TariffCode}' мора да биде точно 10 цифри",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }
        }
        
        return await Task.FromResult(result);
    }
}
