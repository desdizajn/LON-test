using LON.Application.Management.Alerts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LON.Worker.Jobs;

/// <summary>
/// Phase 17 §E10.5 — periodic evaluator. Every 5 minutes, runs one pass via
/// <see cref="IAlertEvaluatorRunner"/>. The runner is scope-resolved per pass
/// (fresh DbContext) and emits 0..N new <c>AlertEvent</c> rows with dedupe
/// against currently-Open events. Same runner is exposed via API for the
/// admin "run now" button + integration tests.
/// </summary>
public class AlertEvaluatorJob : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EvalInterval = TimeSpan.FromMinutes(5);

    private readonly ILogger<AlertEvaluatorJob> _logger;
    private readonly IServiceProvider _services;

    public AlertEvaluatorJob(ILogger<AlertEvaluatorJob> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertEvaluatorJob started; first run in {Delay}", StartDelay);
        try { await Task.Delay(StartDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IAlertEvaluatorRunner>();
                var (rules, events) = await runner.RunOnceAsync(stoppingToken);
                _logger.LogInformation(
                    "AlertEvaluator pass complete: rules={Rules}, newEvents={Events}",
                    rules, events);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AlertEvaluator pass failed");
            }

            try { await Task.Delay(EvalInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("AlertEvaluatorJob stopping");
    }
}
