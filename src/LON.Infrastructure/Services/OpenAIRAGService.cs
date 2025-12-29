using System.Net.Http.Json;
using System.Text.Json;
using LON.Application.KnowledgeBase.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LON.Infrastructure.Services;

/// <summary>
/// RAG Service користи OpenAI GPT за генерирање одговори со контекст
/// </summary>
public class OpenAIRAGService : IRAGService
{
    private readonly IVectorStoreService _vectorStore;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAIRAGService> _logger;
    
    private const string OpenAIChatEndpoint = "https://api.openai.com/v1/chat/completions";
    
    public OpenAIRAGService(
        IVectorStoreService vectorStore,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIRAGService> logger)
    {
        _vectorStore = vectorStore;
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
        _model = configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini";
        _logger = logger;
        
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }
    
    public async Task<RAGResponse> AskQuestionAsync(string question, int maxContextChunks = 3)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new RAGResponse 
            { 
                Success = false, 
                ErrorMessage = "Прашањето не може да биде празно" 
            };
        }
        
        try
        {
            // 1. Semantic search за релевантен контекст
            var searchResults = await _vectorStore.SearchAsync(question, maxContextChunks, 0.7);
            
            if (searchResults.Count == 0)
            {
                return new RAGResponse
                {
                    Success = true,
                    Answer = "За жал, не можев да најдам релевантни информации во Knowledge Base за вашето прашање. Ве молам обидете се со пореконкретно прашање или консултирајте се со царински службеник.",
                    Sources = new List<SourceReference>()
                };
            }
            
            // 2. Креирај prompt со контекст
            var context = string.Join("\n\n---\n\n", searchResults.Select((r, i) => 
                $"[Извор {i+1}: {r.DocumentTitle} - {r.ChunkTitle}]\n{r.Content}"));
            
            var systemPrompt = @"Ти си царински експерт асистент за македонската царинска служба. 
Твоја задача е да одговараш на прашања користејќи го контекстот од царински правилници и регулативи.

ПРАВИЛА:
1. Одговарај САМО врз основа на дадениот контекст
2. Ако контекстот не содржи одговор, кажи дека не знаеш
3. Цитирај ги изворите користејќи [Извор X]
4. Биди прецизен и јасен
5. Користи македонски јазик";

            var userPrompt = $@"КОНТЕКСТ:
{context}

ПРАШАЊЕ: {question}

Одговори на прашањето користејќи го горниот контекст. Наведи ги изворите.";

            // 3. Викни OpenAI за одговор
            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 800
            };
            
            var response = await _httpClient.PostAsJsonAsync(OpenAIChatEndpoint, request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            var answer = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "Грешка при генерирање на одговор";
            
            // 4. Креирај response со извори
            return new RAGResponse
            {
                Success = true,
                Answer = answer,
                Sources = searchResults.Select(r => new SourceReference
                {
                    DocumentTitle = r.DocumentTitle,
                    Reference = r.Reference ?? r.ChunkTitle,
                    ContentSnippet = r.Content.Length > 200 
                        ? r.Content.Substring(0, 200) + "..." 
                        : r.Content,
                    RelevanceScore = r.SimilarityScore
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG failed for question: {Question}", question);
            return new RAGResponse
            {
                Success = false,
                ErrorMessage = "Грешка при обработка на прашањето: " + ex.Message
            };
        }
    }
    
    public async Task<RAGResponse> ExplainConceptAsync(string concept)
    {
        var question = $"Објасни го царинскиот концепт: {concept}. Дај конкретни примери и упатства.";
        return await AskQuestionAsync(question, 5);
    }
}

// OpenAI Chat API Response models
internal class OpenAIChatResponse
{
    public List<ChatChoice>? Choices { get; set; }
}

internal class ChatChoice
{
    public ChatMessage? Message { get; set; }
}

internal class ChatMessage
{
    public string? Content { get; set; }
}
