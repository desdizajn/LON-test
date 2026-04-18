using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: процедурните кодови 4200 и 5100 (LON суспензиски систем)
/// мора да имаат поврзано активно LONAuthorization во рок на важност.
///
/// Handler-от (<see cref="Commands.CreateCustomsDeclaration.CreateCustomsDeclarationCommandHandler"/>)
/// веќе го проверува ова за create-time — овде правилото е „safety net"
/// за validate() endpoint-от и за post-save провери.
/// </summary>
public class LONAuthorizationRequiredRule : IDeclarationRule
{
    public string RuleCode => "BOX37_LON_AUTH_REQUIRED";
    public int Priority => 25;

    private static readonly HashSet<string> LonProcedureCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "4200", "5100"
    };

    private readonly IApplicationDbContext _context;

    public LONAuthorizationRequiredRule(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        const string fieldName = "LONAuthorizationId";

        if (!LonProcedureCodes.Contains(declaration.ProcedureCode ?? string.Empty))
            return ValidationRuleResult.Success(RuleCode, fieldName);

        if (declaration.LONAuthorizationId is null || declaration.LONAuthorizationId == Guid.Empty)
        {
            return ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"Процедура '{declaration.ProcedureCode}' бара активно LON одобрение. " +
                "Поврзете ја декларацијата со Odobrenie (LONAuthorizationId).",
                "УСЦЗ член 349; Правилник за LON");
        }

        var auth = await _context.LONAuthorizations
            .FirstOrDefaultAsync(a => a.Id == declaration.LONAuthorizationId.Value, cancellationToken);

        if (auth is null)
        {
            return ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"LON одобрение '{declaration.LONAuthorizationId}' не постои или не е достапно под овој tenant.",
                "УСЦЗ член 349");
        }

        if (!string.Equals(auth.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"LON одобрение '{auth.AuthorizationNumber}' не е активно (status={auth.Status}).",
                "УСЦЗ член 349");
        }

        if (auth.ExpiryDate.HasValue && auth.ExpiryDate.Value.Date < declaration.DeclarationDate.Date)
        {
            return ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"LON одобрение '{auth.AuthorizationNumber}' истекло на {auth.ExpiryDate:yyyy-MM-dd}.",
                "УСЦЗ член 349");
        }

        return ValidationRuleResult.Success(RuleCode, fieldName);
    }
}
