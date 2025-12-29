using System.Text.Json;
using LON.Application.Common.Interfaces;
using LON.Application.KnowledgeBase.Services;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Services;

/// <summary>
/// In-memory Vector Store за semantic search
/// За продукција може да се замени со Qdrant, Pinecone, или Postgres pgvector
/// </summary>
public class InMemoryVectorStoreService : IVectorStoreService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<InMemoryVectorStoreService> _logger;
    
    public InMemoryVectorStoreService(
        IApplicationDbContext context,
        IEmbeddingService embeddingService,
        ILogger<InMemoryVectorStoreService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }
    
    public async Task IndexDocumentAsync(KnowledgeDocument document, List<KnowledgeDocumentChunk> chunks)
    {
        if (document == null || chunks == null || chunks.Count == 0)
            return;
        
        _logger.LogInformation("Indexing document {DocumentId} with {ChunkCount} chunks", 
            document.Id, chunks.Count);
        
        try
        {
            // Generate embeddings за сите chunks
            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(contents);
            
            // Зачувај embeddings во chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = JsonSerializer.Serialize(embeddings[i]);
            }
            
            // Додај document и chunks во база
            _context.KnowledgeDocuments.Add(document);
            _context.KnowledgeDocumentChunks.AddRange(chunks);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully indexed document {DocumentId}", document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", document.Id);
            throw;
        }
    }
    
    public async Task<List<SearchResult>> SearchAsync(string query, int topK = 5, double minSimilarity = 0.7)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();
        
        try
        {
            // Generate embedding за query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            
            // Load сите chunks со embeddings
            var chunks = await _context.KnowledgeDocumentChunks
                .Include(c => c.Document)
                .Where(c => c.Embedding != null && c.Document.IsActive)
                .ToListAsync();
            
            if (chunks.Count == 0)
            {
                _logger.LogWarning("No indexed chunks found for search");
                return new List<SearchResult>();
            }
            
            // Пресметај similarity за секој chunk
            var results = new List<SearchResult>();
            
            foreach (var chunk in chunks)
            {
                var chunkEmbedding = JsonSerializer.Deserialize<float[]>(chunk.Embedding!);
                if (chunkEmbedding == null) continue;
                
                var similarity = _embeddingService.CosineSimilarity(queryEmbedding, chunkEmbedding);
                
                if (similarity >= minSimilarity)
                {
                    results.Add(new SearchResult
                    {
                        ChunkId = chunk.Id,
                        DocumentId = chunk.DocumentId,
                        Content = chunk.Content,
                        ChunkTitle = chunk.ChunkTitle,
                        DocumentTitle = chunk.Document.TitleMK,
                        DocumentType = chunk.Document.DocumentType,
                        Reference = chunk.Document.Reference,
                        SimilarityScore = similarity
                    });
                }
            }
            
            // Сортирај по similarity (најдобри прво) и земи top K
            var topResults = results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(topK)
                .ToList();
            
            _logger.LogInformation("Search for '{Query}' returned {ResultCount} results", 
                query, topResults.Count);
            
            return topResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            throw;
        }
    }
    
    public async Task<List<SearchResult>> SearchByDocumentTypeAsync(string query, string documentType, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(documentType))
            return new List<SearchResult>();
        
        try
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            
            var chunks = await _context.KnowledgeDocumentChunks
                .Include(c => c.Document)
                .Where(c => c.Embedding != null 
                    && c.Document.IsActive 
                    && c.Document.DocumentType == documentType)
                .ToListAsync();
            
            var results = new List<SearchResult>();
            
            foreach (var chunk in chunks)
            {
                var chunkEmbedding = JsonSerializer.Deserialize<float[]>(chunk.Embedding!);
                if (chunkEmbedding == null) continue;
                
                var similarity = _embeddingService.CosineSimilarity(queryEmbedding, chunkEmbedding);
                
                results.Add(new SearchResult
                {
                    ChunkId = chunk.Id,
                    DocumentId = chunk.DocumentId,
                    Content = chunk.Content,
                    ChunkTitle = chunk.ChunkTitle,
                    DocumentTitle = chunk.Document.TitleMK,
                    DocumentType = chunk.Document.DocumentType,
                    Reference = chunk.Document.Reference,
                    SimilarityScore = similarity
                });
            }
            
            return results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(topK)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Type-filtered search failed for query: {Query}, type: {DocumentType}", 
                query, documentType);
            throw;
        }
    }
}
