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
        
        // Accept codes from two sources: (a) curated CustomsProcedures table
        // (our supported procedures) or (b) KB-seeded CodeListItems with
        // ListType=ProcedureCode (full MK regulation list). OR so a freshly-
        // seeded instance without full KB still validates '4200'.
        var inProcedures = await _context.CustomsProcedures
            .AnyAsync(p => p.Code == declaration.ProcedureCode && p.IsActive, cancellationToken);

        var inCodeList = !inProcedures && await _context.CodeListItems
            .AnyAsync(c =>
                c.ListType == "ProcedureCode" &&
                c.Code == declaration.ProcedureCode &&
                c.IsActive,
                cancellationToken);

        if (inProcedures || inCodeList)
            return ValidationRuleResult.Success(RuleCode, fieldName);

        var availableProcs = await _context.CustomsProcedures
            .Where(p => p.IsActive)
            .Select(p => new { p.Code, p.Name })
            .ToListAsync(cancellationToken);

        var suggestions = availableProcs.Count > 0
            ? $"\n\nДозволени кодови:\n{string.Join("\n", availableProcs.Select(a => $"- {a.Code}: {a.Name}"))}"
            : string.Empty;

        return ValidationRuleResult.Failure(
            RuleCode,
            fieldName,
            $"Box 37: Процедурниот код '{declaration.ProcedureCode}' не е валиден{suggestions}",
            "Правилник, Член 19"
        );
    }
}
