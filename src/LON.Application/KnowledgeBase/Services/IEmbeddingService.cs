namespace LON.Application.KnowledgeBase.Services;

/// <summary>
/// Service за генерирање на vector embeddings преку OpenAI
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Генерира vector embedding за текст
    /// </summary>
    /// <param name="text">Текст за embedding</param>
    /// <returns>Vector embedding (1536 dim)</returns>
    Task<float[]> GenerateEmbeddingAsync(string text);
    
    /// <summary>
    /// Генерира embeddings за множество текстови (batch)
    /// </summary>
    /// <param name="texts">Листа на текстови</param>
    /// <returns>Листа на vector embeddings</returns>
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);
    
    /// <summary>
    /// Пресметува косинус сличност меѓу два вектори
    /// </summary>
    /// <param name="vector1">Прв вектор</param>
    /// <param name="vector2">Втор вектор</param>
    /// <returns>Сличност [0-1]</returns>
    double CosineSimilarity(float[] vector1, float[] vector2);
}
