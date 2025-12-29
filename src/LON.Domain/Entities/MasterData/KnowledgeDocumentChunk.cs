using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// Chunk (парче) од документ за vector search
/// Секој документ се дели на помали chunks за подобро embedding и search
/// </summary>
public class KnowledgeDocumentChunk : BaseEntity
{
    /// <summary>
    /// Референца кон родителскиот документ
    /// </summary>
    public Guid DocumentId { get; set; }
    public virtual KnowledgeDocument Document { get; set; } = null!;
    
    /// <summary>
    /// Индекс на chunk во документот (0, 1, 2...)
    /// </summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>
    /// Содржина на chunk-от (обично 500-1000 карактери)
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Наслов/Контекст за chunk (нпр. "Член 5 - Царинско складирање")
    /// </summary>
    public string? ChunkTitle { get; set; }
    
    /// <summary>
    /// Vector embedding (1536 dim за text-embedding-ada-002)
    /// Складирано како JSON array за сега, подоцна може Postgres pgvector
    /// </summary>
    public string? Embedding { get; set; }
    
    /// <summary>
    /// Token count на chunk-от
    /// </summary>
    public int TokenCount { get; set; }
    
    /// <summary>
    /// Метаподатоци за chunk (референци кон други документи, keywords...)
    /// </summary>
    public string? Metadata { get; set; }
}
