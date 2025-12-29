using LON.Domain.Common;

namespace LON.Domain.Entities.MasterData;

/// <summary>
/// Царински документ во Knowledge Base (Правилник, Упатство, Процедура...)
/// </summary>
public class KnowledgeDocument : BaseEntity
{
    /// <summary>
    /// Тип на документ (Правилник, Упатство, SADка Упатство...)
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;
    
    /// <summary>
    /// Наслов на документот (EN)
    /// </summary>
    public string TitleEN { get; set; } = string.Empty;
    
    /// <summary>
    /// Наслов на документот (MK)
    /// </summary>
    public string TitleMK { get; set; } = string.Empty;
    
    /// <summary>
    /// Референца (нпр. Член 5, Глава 3, Box 33...)
    /// </summary>
    public string? Reference { get; set; }
    
    /// <summary>
    /// Дали документот е активен
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Содржина на документот (полн текст)
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Метаподатоци (JSON) - author, version, keywords...
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// URL кон оригинален документ (опционално)
    /// </summary>
    public string? SourceUrl { get; set; }
    
    /// <summary>
    /// Дата на документот
    /// </summary>
    public DateTime? DocumentDate { get; set; }
    
    /// <summary>
    /// Верзија на документот
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// Јазик на документот (mk, en)
    /// </summary>
    public string Language { get; set; } = "mk";
    
    /// <summary>
    /// Navigation: Document chunks за vector search
    /// </summary>
    public virtual ICollection<KnowledgeDocumentChunk> Chunks { get; set; } = new List<KnowledgeDocumentChunk>();
}
