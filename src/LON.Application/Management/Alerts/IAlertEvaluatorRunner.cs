namespace LON.Application.Management.Alerts;

/// <summary>
/// Phase 17 §E10.5 — orchestration over <see cref="IAlertRuleEvaluator"/>.
/// One scoped pass loads every active <c>AlertRule</c>, dispatches to the
/// matching evaluator, dedupes the drafts against currently-Open
/// <c>AlertEvent</c>s and persists the rest.
///
/// Called by the LON.Worker hosted service on a 5-minute timer AND by the
/// management API's on-demand "run now" endpoint (admin-only).
/// </summary>
public interface IAlertEvaluatorRunner
{
    /// <summary>
    /// Executes one full pass. Returns (rulesEvaluated, alertEventsCreated).
    /// </summary>
    Task<(int RulesEvaluated, int EventsCreated)> RunOnceAsync(CancellationToken ct = default);
}
