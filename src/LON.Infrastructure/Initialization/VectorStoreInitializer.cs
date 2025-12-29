using LON.Application.Common.Interfaces;
using LON.Application.KnowledgeBase.Services;
using LON.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Initialization;

/// <summary>
/// Иницијализација на Knowledge Base Vector Store
/// </summary>
public class VectorStoreInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VectorStoreInitializer> _logger;

    public VectorStoreInitializer(IServiceProvider serviceProvider, ILogger<VectorStoreInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Иницијализирај Vector Store со документи и embeddings
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var chunkingService = scope.ServiceProvider.GetRequiredService<IDocumentChunkingService>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStoreService>();

            _logger.LogInformation("🚀 Initializing Vector Store...");

            // 1. Seed документи ако ги нема
            await DocumentSeeder.SeedDocumentsAsync(context, chunkingService, embeddingService);

            // 2. Вчитај ги сите chunks во in-memory vector store
            var chunks = await context.KnowledgeDocumentChunks
                .Include(c => c.Document)
                .Where(c => c.Embedding != null && c.Embedding.Length > 0)
                .ToListAsync();

            _logger.LogInformation($"📊 Loading {chunks.Count} document chunks into vector store...");

            // In-memory vector store веќе ги чита од база, не треба експлицитно loading
            // IndexDocumentAsync веќе ги зачувало embeddings во база

            _logger.LogInformation($"✅ Vector Store initialized with {chunks.Count} chunks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Vector Store");
            throw;
        }
    }
}
