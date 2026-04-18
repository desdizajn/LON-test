using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Customs;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Customs.Validation.Rules;

/// <summary>
/// I3 safety net: ако линијата има TariffCode чие KnigaNai.ST (TariffCode.CustomsRate)
/// може да се прочита, споредува со user-внесен DutyRate. На разлика > 0.01%
/// емитира WARNING (не блокира). Превенира класично грешно пишување („5" наместо
/// „5.5") пред submit на царина.
///
/// <para>
/// Не опфаќа preferential origin (Aneksi.ST&lt;year&gt;) — тоа е дел од Phase 4
/// кога KnigaNai цело ќе биде year-indexed. За сега се потпираме само на
/// book rate; user може да го override-не со образложение.
/// </para>
/// </summary>
public class DutyRateLookupWarningRule : IDeclarationRule
{
    public string RuleCode => "DUTY_RATE_LOOKUP_WARN";
    public int Priority => 14;

    private readonly IApplicationDbContext _context;

    public DutyRateLookupWarningRule(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ValidationRuleResult> ValidateAsync(CustomsDeclaration declaration, CancellationToken cancellationToken = default)
    {
        var result = new ValidationRuleResult
        {
            RuleCode = RuleCode,
            FieldName = "Lines.DutyRate",
            IsValid = true
        };

        var tariffCodes = declaration.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.TariffCode))
            .Select(l => l.TariffCode!)
            .Distinct()
            .ToList();

        if (tariffCodes.Count == 0)
            return result;

        var lookup = await _context.TariffCodes
            .Where(t => tariffCodes.Contains(t.TariffNumber) && t.IsActive)
            .Select(t => new { t.TariffNumber, t.CustomsRate, t.VATRate })
            .ToDictionaryAsync(t => t.TariffNumber, cancellationToken);

        foreach (var line in declaration.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.TariffCode)) continue;
            if (!lookup.TryGetValue(line.TariffCode, out var row)) continue;

            // TariffCode.CustomsRate / VATRate могат да бидат nullable во KB.
            // Ако не се познати, го прескокнуваме check-от.
            if (row.CustomsRate.HasValue && Math.Abs(line.DutyRate - row.CustomsRate.Value) > 0.01m)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Линија {line.LineNumber} ({line.TariffCode}): внесена царинска стапка {line.DutyRate}% се разликува од KnigaNai.ST ({row.CustomsRate.Value}%). Потврди пред submit.",
                    ReferenceDocument = "KnigaNai / Carinska Tarifa"
                });
            }

            if (line.VATRate > 0 && row.VATRate.HasValue && Math.Abs(line.VATRate - row.VATRate.Value) > 0.01m)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Линија {line.LineNumber} ({line.TariffCode}): внесена ДДВ {line.VATRate}% се разликува од KnigaNai ({row.VATRate.Value}%).",
                    ReferenceDocument = "CarTarPovlasteniDDV"
                });
            }
        }

        return result;
    }
}
