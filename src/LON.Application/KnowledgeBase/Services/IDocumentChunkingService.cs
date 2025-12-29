namespace LON.Application.KnowledgeBase.Services;

/// <summary>
/// Service за делење на документи на chunks за vector search
/// </summary>
public interface IDocumentChunkingService
{
    /// <summary>
    /// Дели документ на chunks со максимална должина
    /// </summary>
    /// <param name="content">Содржина на документот</param>
    /// <param name="maxChunkSize">Максимална должина на chunk (карактери)</param>
    /// <param name="overlap">Overlap меѓу chunks за контекст (карактери)</param>
    /// <returns>Листа на chunks</returns>
    List<string> ChunkDocument(string content, int maxChunkSize = 1000, int overlap = 200);
    
    /// <summary>
    /// Дели документ на chunks по параграфи/секции
    /// </summary>
    /// <param name="content">Содржина на документот</param>
    /// <param name="sectionDelimiters">Делители за секции (нпр. "Член", "Глава")</param>
    /// <returns>Листа на chunks со контекст</returns>
    List<DocumentChunk> ChunkBySection(string content, string[] sectionDelimiters);
    
    /// <summary>
    /// Пресметува token count за chunk (приближно)
    /// </summary>
    int EstimateTokenCount(string text);
}

/// <summary>
/// Chunk со контекст/наслов
/// </summary>
public class DocumentChunk
{
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int TokenCount { get; set; }
}
