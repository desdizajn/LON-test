using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 01 (DeclarationType) е задолжително
/// </summary>
public class RequiredFieldsRule : IDeclarationRule
{
    private readonly IApplicationDbContext _context;
    
    public string RuleCode => "REQUIRED_FIELDS";
    public int Priority => 10;
    
    public RequiredFieldsRule(IApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Multiple",
            IsValid = true
        };
        
        // Box 01: Declaration Type
        if (string.IsNullOrWhiteSpace(declaration.DeclarationType))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = "Box 01: Вид на декларација е задолжителен",
                ReferenceDocument = "Правилник, Член 7"
            });
        }
        
        // Box 02: Sender (користи SenderName наместо Sender)
        if (string.IsNullOrWhiteSpace(declaration.SenderName))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = "Box 02: Испраќач/Извозник е задолжителен",
                ReferenceDocument = "Правилник, Член 8"
            });
        }
        
        // Box 33: Tariff Code (валидирај линии)
        if (!declaration.Lines.Any())
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = "Декларацијата мора да има најмалку една линија",
                ReferenceDocument = "Правилник"
            });
        }
        
        foreach (var line in declaration.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.TariffCode))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Box 33 (Линија {line.LineNumber}): Тарифна ознака е задолжителна",
                    ReferenceDocument = "Правилник, Член 15"
                });
            }
        }
        
        return await Task.FromResult(result);
    }
}
