namespace LON.Application.KnowledgeBase.Services;

/// <summary>
/// RAG (Retrieval-Augmented Generation) Service за интелигентни одговори
/// </summary>
public interface IRAGService
{
    /// <summary>
    /// Одговори на прашање со контекст од Knowledge Base
    /// </summary>
    /// <param name="question">Прашање на македонски</param>
    /// <param name="maxContextChunks">Макс број на chunks за контекст</param>
    /// <returns>Одговор со references</returns>
    Task<RAGResponse> AskQuestionAsync(string question, int maxContextChunks = 3);
    
    /// <summary>
    /// Објасни царински концепт или правило
    /// </summary>
    /// <param name="concept">Концепт (нпр. "Box 33", "Inward Processing")</param>
    /// <returns>Објаснување со примери</returns>
    Task<RAGResponse> ExplainConceptAsync(string concept);
}

/// <summary>
/// RAG Response со одговор и извори
/// </summary>
public class RAGResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SourceReference> Sources { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Извор/референца за одговорот
/// </summary>
public class SourceReference
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string ContentSnippet { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
