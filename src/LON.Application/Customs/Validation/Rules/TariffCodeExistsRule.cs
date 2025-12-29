using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 33 мора да постои во TARIC базата
/// </summary>
public class TariffCodeExistsRule : IDeclarationRule
{
    private readonly IApplicationDbContext _context;
    
    public string RuleCode => "BOX33_TARIC_EXISTS";
    public int Priority => 16;
    
    public TariffCodeExistsRule(IApplicationDbContext context)
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
            
            // Провери дали постои во TARIC
            var exists = await _context.TariffCodes
                .AnyAsync(t => t.TariffNumber == line.TariffCode && t.IsActive, cancellationToken);
            
            if (!exists)
            {
                // Пронајди слични кодови за suggestion
                var similarCodes = await _context.TariffCodes
                    .Where(t => t.TariffNumber.StartsWith(line.TariffCode.Substring(0, Math.Min(4, line.TariffCode.Length))) && t.IsActive)
                    .Take(3)
                    .Select(t => new { t.TariffNumber, t.Description })
                    .ToListAsync(cancellationToken);
                
                var suggestions = similarCodes.Any() 
                    ? $"\n\nДали мислевте на:\n{string.Join("\n", similarCodes.Select(s => $"- {s.TariffNumber}: {s.Description.Substring(0, Math.Min(50, s.Description.Length))}..."))}"
                    : "";
                
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Message = $"Box 33 (Линија {line.LineNumber}): Тарифната ознака '{line.TariffCode}' не постои во TARIC базата{suggestions}",
                    ReferenceDocument = "TARIC база"
                });
            }
        }
        
        return result;
    }
}
