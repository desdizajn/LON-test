using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts.Evaluators;

/// <summary>
/// Rule (d) — EmployeeCertification whose ExpiryDate is within
/// <c>threshold</c> days from now.
/// </summary>
public sealed class CertificationExpiringEvaluator : IAlertRuleEvaluator
{
    private readonly IApplicationDbContext _context;

    public CertificationExpiringEvaluator(IApplicationDbContext context) => _context = context;

    public AlertTriggerKind Kind => AlertTriggerKind.CertificationExpiringSoon;

    public async Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var days = (int)(rule.Threshold ?? 30m);
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(days);

        var certs = await _context.EmployeeCertifications
            .Where(c => c.TenantId == rule.TenantId
                        && !c.IsDeleted
                        && c.ExpiryDate != null
                        && c.ExpiryDate <= cutoff
                        && c.ExpiryDate >= now)
            .Select(c => new { c.Id, c.EmployeeId, Name = c.CertificationName, c.ExpiryDate })
            .ToListAsync(ct);
        if (certs.Count == 0) return new List<AlertEventDraft>();

        var empIds = certs.Select(c => c.EmployeeId).Distinct().ToList();
        var employees = await _context.Employees
            .Where(e => empIds.Contains(e.Id))
            .Select(e => new { e.Id, FullName = e.FirstName + " " + e.LastName })
            .ToDictionaryAsync(e => e.Id, e => e.FullName, ct);

        return certs.Select(c =>
        {
            var person = employees.TryGetValue(c.EmployeeId, out var n) ? n : c.EmployeeId.ToString();
            var daysLeft = (c.ExpiryDate!.Value.Date - now.Date).Days;
            return new AlertEventDraft
            {
                DedupKey = $"CERT_EXPIRING:{c.Id}",
                EntityType = "EmployeeCertification",
                EntityId = c.Id,
                Title = $"Сертификат истекува за {daysLeft}д: {person}",
                Body = $"Сертификатот „{c.Name}\" на {person} истекува на {c.ExpiryDate:yyyy-MM-dd} (за {daysLeft} дена). Организирај подновување.",
            };
        }).ToList();
    }
}
