using System.Net.Http.Json;
using System.Text.Json;
using LON.Application.KnowledgeBase.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Services;

/// <summary>
/// OpenAI Embeddings Service
/// Користи text-embedding-ada-002 model (1536 dimensions)
/// </summary>
public class OpenAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAIEmbeddingService> _logger;
    
    private const string OpenAIEmbeddingsEndpoint = "https://api.openai.com/v1/embeddings";
    
    public OpenAIEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIEmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
        _model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";
        _logger = logger;
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }
    
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));
        
        try
        {
            var request = new
            {
                input = text,
                model = _model
            };
            
            var response = await _httpClient.PostAsJsonAsync(OpenAIEmbeddingsEndpoint, request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
            
            if (result?.Data == null || result.Data.Count == 0)
                throw new InvalidOperationException("No embedding returned from OpenAI");
            
            return result.Data[0].Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {TextPreview}", 
                text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            throw;
        }
    }
    
    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        if (texts == null || texts.Count == 0)
            return new List<float[]>();
        
        try
        {
            // OpenAI supports batch embeddings (до 2048 inputs)
            var request = new
            {
                input = texts,
                model = _model
            };
            
            var response = await _httpClient.PostAsJsonAsync(OpenAIEmbeddingsEndpoint, request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
            
            if (result?.Data == null)
                throw new InvalidOperationException("No embeddings returned from OpenAI");
            
            return result.Data.Select(d => d.Embedding).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for {Count} texts", texts.Count);
            throw;
        }
    }
    
    public double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1 == null || vector2 == null)
            throw new ArgumentNullException("Vectors cannot be null");
        
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same length");
        
        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;
        
        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }
        
        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);
        
        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;
        
        return dotProduct / (magnitude1 * magnitude2);
    }
}

// OpenAI API Response models
internal class OpenAIEmbeddingResponse
{
    public List<EmbeddingData> Data { get; set; } = new();
}

internal class EmbeddingData
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
