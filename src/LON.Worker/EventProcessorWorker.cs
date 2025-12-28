using LON.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace LON.Worker;

public class EventProcessorWorker : BackgroundService
{
    private readonly ILogger<EventProcessorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public EventProcessorWorker(ILogger<EventProcessorWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Processor Worker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessages(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing events");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Event Processor Worker stopping at: {time}", DateTimeOffset.Now);
    }

    private async Task ProcessOutboxMessages(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pendingMessages = await context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (!pendingMessages.Any())
            return;

        _logger.LogInformation("Processing {count} outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                await ProcessMessage(message, context, cancellationToken);
                message.ProcessedOnUtc = DateTime.UtcNow;
                _logger.LogInformation("Processed outbox message {id} of type {type}", message.Id, message.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {id}", message.Id);
                message.Error = ex.Message;
                message.ProcessedOnUtc = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessMessage(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case "InventoryMovedEvent":
                await HandleInventoryMovedEvent(message, context, cancellationToken);
                break;

            case "MaterialIssuedEvent":
                await HandleMaterialIssuedEvent(message, context, cancellationToken);
                break;

            case "FGReceivedEvent":
                await HandleFGReceivedEvent(message, context, cancellationToken);
                break;

            case "GuaranteeDebitedEvent":
                await HandleGuaranteeDebitedEvent(message, context, cancellationToken);
                break;

            case "GuaranteeCreditedEvent":
                await HandleGuaranteeCreditedEvent(message, context, cancellationToken);
                break;

            case "CustomsClearedEvent":
                await HandleCustomsClearedEvent(message, context, cancellationToken);
                break;

            case "ReceiptCreatedEvent":
                await HandleReceiptCreatedEvent(message, context, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown event type: {type}", message.Type);
                break;
        }
    }

    private async Task HandleInventoryMovedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling InventoryMovedEvent");
        await Task.CompletedTask;
    }

    private async Task HandleMaterialIssuedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling MaterialIssuedEvent");
        await Task.CompletedTask;
    }

    private async Task HandleFGReceivedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling FGReceivedEvent");
        await Task.CompletedTask;
    }

    private async Task HandleGuaranteeDebitedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GuaranteeDebitedEvent");
        await Task.CompletedTask;
    }

    private async Task HandleGuaranteeCreditedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GuaranteeCreditedEvent");
        await Task.CompletedTask;
    }

    private async Task HandleCustomsClearedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CustomsClearedEvent");
        await Task.CompletedTask;
    }

    private async Task HandleReceiptCreatedEvent(OutboxMessage message, ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling ReceiptCreatedEvent - Creating inventory balance");
        await Task.CompletedTask;
    }
}
