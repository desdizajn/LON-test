using LON.Infrastructure.Initialization;
using LON.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Services;

/// <summary>
/// Background service за иницијализација на Vector Store без да блокира API startup
/// </summary>
public class VectorStoreBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VectorStoreBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public VectorStoreBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<VectorStoreBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Провери дали е enable-ирана векторската база
        var isEnabled = _configuration.GetValue<bool>("EnableVectorStore", false);
        
        if (!isEnabled)
        {
            _logger.LogInformation("Vector Store is disabled. Skipping initialization.");
            return;
        }

        // Почекај малку пред да стартуваш (за да не оптоваруваш API при startup)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _logger.LogInformation("🚀 Starting Vector Store initialization in background...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var vectorStoreInitializer = scope.ServiceProvider.GetService<VectorStoreInitializer>();
            
            if (vectorStoreInitializer == null)
            {
                _logger.LogWarning("VectorStoreInitializer not registered. Skipping initialization.");
                return;
            }

            await vectorStoreInitializer.InitializeAsync();
            
            _logger.LogInformation("✅ Vector Store initialization completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during Vector Store initialization. The system will continue to function without RAG capabilities.");
            // Не фрламе exception за да не падне целиот систем
        }
    }
}
