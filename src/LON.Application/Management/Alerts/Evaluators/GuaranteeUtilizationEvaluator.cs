using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts.Evaluators;

/// <summary>
/// Rule (a) — GuaranteeAccount.CurrentBalance / TotalLimit &gt; threshold.
/// Default threshold = 0.90 (90%). Emits one event per breaching account.
/// </summary>
public sealed class GuaranteeUtilizationEvaluator : IAlertRuleEvaluator
{
    private readonly IApplicationDbContext _context;

    public GuaranteeUtilizationEvaluator(IApplicationDbContext context) => _context = context;

    public AlertTriggerKind Kind => AlertTriggerKind.GuaranteeUtilizationHigh;

    public async Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var threshold = rule.Threshold ?? 0.90m;
        var accounts = await _context.GuaranteeAccounts
            .Where(a => a.TenantId == rule.TenantId && a.IsActive && !a.IsDeleted)
            .Include(a => a.LedgerEntries)
            .ToListAsync(ct);

        var drafts = new List<AlertEventDraft>();
        foreach (var account in accounts)
        {
            if (account.TotalLimit <= 0) continue;
            var balance = account.LedgerEntries
                .Where(e => !e.IsDeleted)
                .Sum(e => e.EntryType == GuaranteeEntryType.Debit ? e.Amount : -e.Amount);
            var utilisation = balance / account.TotalLimit;
            if (utilisation < threshold) continue;

            drafts.Add(new AlertEventDraft
            {
                DedupKey = $"GUARANTEE_UTIL:{account.Id}:{(int)(utilisation * 100)}",
                EntityType = "GuaranteeAccount",
                EntityId = account.Id,
                Title = $"Гаранцијата надмина {threshold:P0}: {account.AccountNumber}",
                Body = $"Тековен биланс {balance:N2} {account.Currency} од {account.TotalLimit:N2} ({utilisation:P1}). Проверете дали нови IM ставки би ja надминале границата.",
            });
        }

        return drafts;
    }
}
