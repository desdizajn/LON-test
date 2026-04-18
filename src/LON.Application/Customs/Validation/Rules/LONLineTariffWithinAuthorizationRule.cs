using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// Правило: ако декларацијата има LON Одобрение кое ја дефинира листата на
/// одобрени тарифи (ApprovedItems), секоја Line.TariffCode мора да постои во
/// таа листа. Правилник: IM 4200 може да се поднесе САМО за тарифи наведени
/// во одобрението (член 349 УСЦЗ).
///
/// <para>
/// Policy за authorization без ApprovedItems: allow-all. Постоечките
/// одобренија без детализирана листа (legacy migration, seed baseline) не се
/// блокираат — корисникот експлицитно додава ApprovedItems кога сака да
/// ограничи.
/// </para>
/// </summary>
public class LONLineTariffWithinAuthorizationRule : IDeclarationRule
{
    public string RuleCode => "LON_LINE_TARIFF_WITHIN_AUTH";
    public int Priority => 26;

    private readonly IApplicationDbContext _context;

    public LONLineTariffWithinAuthorizationRule(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        const string fieldName = "Lines.TariffCode";

        if (!declaration.LONAuthorizationId.HasValue || declaration.LONAuthorizationId == Guid.Empty)
            return ValidationRuleResult.Success(RuleCode, fieldName);

        // Fetch ApprovedItems once for this authorization. Ignore query filter
        // to be safe in validate() path where tenant may not yet be bound;
        // LONAuthorizationRequiredRule already proved tenant-scoped access.
        var approvedCodes = await _context.LONAuthorizationItems
            .IgnoreQueryFilters()
            .Where(ai => ai.LONAuthorizationId == declaration.LONAuthorizationId.Value && !ai.IsDeleted)
            .Select(ai => ai.ImportTariffCode)
            .ToListAsync(cancellationToken);

        // allow-all default: authorization without an explicit list is unrestricted.
        if (approvedCodes.Count == 0)
            return ValidationRuleResult.Success(RuleCode, fieldName);

        var approvedSet = new HashSet<string>(approvedCodes, StringComparer.OrdinalIgnoreCase);
        var rejectedLines = new List<string>();
        foreach (var line in declaration.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.TariffCode)) continue; // covered by other rules
            if (!approvedSet.Contains(line.TariffCode))
                rejectedLines.Add($"Line {line.LineNumber}: tariff '{line.TariffCode}'");
        }

        if (rejectedLines.Count > 0)
        {
            var authCodesText = approvedCodes.Count <= 10
                ? string.Join(", ", approvedCodes)
                : string.Join(", ", approvedCodes.Take(10)) + $" (+{approvedCodes.Count - 10} more)";

            return ValidationRuleResult.Failure(
                RuleCode, fieldName,
                $"Тарифна ознака не е дозволена со избраното LON Одобрение: " +
                $"{string.Join("; ", rejectedLines)}. " +
                $"Одобрени тарифи: {authCodesText}.",
                "УСЦЗ член 349; Правилник за LON");
        }

        return ValidationRuleResult.Success(RuleCode, fieldName);
    }
}
