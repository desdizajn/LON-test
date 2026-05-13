using LON.Application.Common.Interfaces;
using LON.Domain.Entities.Management;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Management.Alerts.Evaluators;

/// <summary>
/// Rule (c) — a Machine whose most-recent <see cref="MachineStateEvent"/>
/// has State=Down and ChangedAt &gt; <c>threshold</c> hours ago.
/// </summary>
public sealed class MachineDownEvaluator : IAlertRuleEvaluator
{
    private readonly IApplicationDbContext _context;

    public MachineDownEvaluator(IApplicationDbContext context) => _context = context;

    public AlertTriggerKind Kind => AlertTriggerKind.MachineDownExtended;

    public async Task<List<AlertEventDraft>> EvaluateAsync(AlertRule rule, CancellationToken ct)
    {
        var thresholdHours = (double)(rule.Threshold ?? 2m);
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-thresholdHours);

        var latestStates = await _context.MachineStateEvents
            .Where(e => e.TenantId == rule.TenantId && !e.IsDeleted)
            .GroupBy(e => e.MachineId)
            .Select(g => g.OrderByDescending(e => e.ChangedAt).First())
            .ToListAsync(ct);

        var downSince = latestStates
            .Where(e => e.State == MachineState.Down && e.ChangedAt <= cutoff)
            .ToList();

        if (downSince.Count == 0) return new List<AlertEventDraft>();

        var machineIds = downSince.Select(e => e.MachineId).Distinct().ToList();
        var machineNames = await _context.Machines
            .Where(m => machineIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        return downSince.Select(e =>
        {
            var elapsed = now - e.ChangedAt;
            var hh = (int)elapsed.TotalHours;
            var mm = elapsed.Minutes;
            var name = machineNames.TryGetValue(e.MachineId, out var n) ? n : e.MachineId.ToString();
            return new AlertEventDraft
            {
                DedupKey = $"MACHINE_DOWN:{e.MachineId}:{e.ChangedAt:yyyyMMddHH}",
                EntityType = "Machine",
                EntityId = e.MachineId,
                Title = $"Машина во дефект > {thresholdHours:F0}ч: {name}",
                Body = $"Машината „{name}\" е во состојба Down од {e.ChangedAt:yyyy-MM-dd HH:mm} (≈{hh}ч {mm}мин). Провери причина / упати на одржување.",
            };
        }).ToList();
    }
}
