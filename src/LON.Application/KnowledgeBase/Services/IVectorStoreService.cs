using LON.Domain.Entities.MasterData;

namespace LON.Application.KnowledgeBase.Services;

/// <summary>
/// Vector store за semantic search врз документи
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Индексира документ со сите негови chunks
    /// </summary>
    /// <param name="document">Документ</param>
    /// <param name="chunks">Chunks од документот</param>
    Task IndexDocumentAsync(KnowledgeDocument document, List<KnowledgeDocumentChunk> chunks);
    
    /// <summary>
    /// Semantic search - најди најсличниchunks за query
    /// </summary>
    /// <param name="query">Query текст</param>
    /// <param name="topK">Број на резултати</param>
    /// <param name="minSimilarity">Минимална сличност [0-1]</param>
    /// <returns>Листа на chunks со similarity score</returns>
    Task<List<SearchResult>> SearchAsync(string query, int topK = 5, double minSimilarity = 0.7);
    
    /// <summary>
    /// Semantic search со филтер по тип на документ
    /// </summary>
    Task<List<SearchResult>> SearchByDocumentTypeAsync(string query, string documentType, int topK = 5);
}

/// <summary>
/// Резултат од semantic search
/// </summary>
public class SearchResult
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ChunkTitle { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public double SimilarityScore { get; set; }
}
