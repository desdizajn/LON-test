using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LON.Worker.Jobs;

/// <summary>
/// Phase 17 §E14 — once a day, hard-deletes ClientOrders that were
/// soft-deleted &gt; 90 days ago. Other ISoftDeletable entity types follow
/// post-v1; today only ClientOrder participates in the recycle bin.
///
/// The 90-day retention matches BLUEPRINT §6.7. Customs records retention
/// (7 years) is enforced by NOT including IM/EX CustomsDeclarations in this
/// purge — those rely on the AuditLogEntry retention policy (P15.x).
/// </summary>
public class SoftDeleteRetentionJob : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);
    private const int RetentionDays = 90;

    private readonly ILogger<SoftDeleteRetentionJob> _logger;
    private readonly IServiceProvider _services;

    public SoftDeleteRetentionJob(ILogger<SoftDeleteRetentionJob> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SoftDeleteRetentionJob started; first pass in {Delay}", StartDelay);
        try { await Task.Delay(StartDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
                var expired = await ctx.ClientOrders
                    .IgnoreQueryFilters()
                    .Where(o => o.IsDeleted && o.DeletedAt != null && o.DeletedAt < cutoff)
                    .ToListAsync(stoppingToken);
                if (expired.Count > 0)
                {
                    ctx.ClientOrders.RemoveRange(expired);
                    await ctx.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation(
                        "SoftDeleteRetentionJob hard-deleted {Count} ClientOrder(s) past {Days}-day retention",
                        expired.Count, RetentionDays);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SoftDeleteRetentionJob pass failed");
            }

            try { await Task.Delay(PurgeInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SoftDeleteRetentionJob stopping");
    }
}
