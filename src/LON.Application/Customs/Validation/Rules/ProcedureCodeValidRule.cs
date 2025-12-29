using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: Box 37 (ProcedureCode) мора да биде од дозволената листа
/// </summary>
public class ProcedureCodeValidRule : IDeclarationRule
{
    private readonly IApplicationDbContext _context;
    
    public string RuleCode => "BOX37_PROCEDURE_CODE";
    public int Priority => 20;
    
    public ProcedureCodeValidRule(IApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var fieldName = "Box37_ProcedureCode";
        
        if (string.IsNullOrWhiteSpace(declaration.ProcedureCode))
        {
            return ValidationRuleResult.Failure(
                RuleCode,
                fieldName,
                "Box 37: Процедурниот код е задолжителен",
                "Правилник, Член 19"
            );
        }
        
        // Провери дали постои во CodeList
        var validCode = await _context.CodeListItems
            .AnyAsync(c => 
                c.ListType == "ProcedureCode" && 
                c.Code == declaration.ProcedureCode && 
                c.IsActive, 
                cancellationToken);
        
        if (!validCode)
        {
            var availableCodes = await _context.CodeListItems
                .Where(c => c.ListType == "ProcedureCode" && c.IsActive)
                .Select(c => new { c.Code, c.DescriptionMK })
                .ToListAsync(cancellationToken);
            
            var suggestions = $"\n\nДозволени кодови:\n{string.Join("\n", availableCodes.Select(a => $"- {a.Code}: {a.DescriptionMK}"))}";
            
            return ValidationRuleResult.Failure(
                RuleCode,
                fieldName,
                $"Box 37: Процедурниот код '{declaration.ProcedureCode}' не е валиден{suggestions}",
                "Правилник, Член 19"
            );
        }
        
        return ValidationRuleResult.Success(RuleCode, fieldName);
    }
}
