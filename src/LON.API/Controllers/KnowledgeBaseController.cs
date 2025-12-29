using LON.Application.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IRAGService _ragService;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILogger<KnowledgeBaseController> _logger;

    public KnowledgeBaseController(
        IRAGService ragService,
        IVectorStoreService vectorStore,
        ILogger<KnowledgeBaseController> logger)
    {
        _ragService = ragService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    /// <summary>
    /// Постави прашање за царински регулативи/процедури
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<RAGResponse>> AskQuestion([FromBody] QuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Прашањето не може да биде празно");

        var response = await _ragService.AskQuestionAsync(request.Question, request.MaxContextChunks);
        return Ok(response);
    }

    /// <summary>
    /// Побарај објаснување за концепт
    /// </summary>
    [HttpPost("explain")]
    public async Task<ActionResult<RAGResponse>> ExplainConcept([FromBody] ConceptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Concept))
            return BadRequest("Концептот не може да биде празен");

        var response = await _ragService.ExplainConceptAsync(request.Concept);
        return Ok(response);
    }

    /// <summary>
    /// Semantic search во Knowledge Base
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<List<SearchResult>>> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query не може да биде празен");

        List<SearchResult> results;

        if (!string.IsNullOrWhiteSpace(request.DocumentType))
        {
            results = await _vectorStore.SearchByDocumentTypeAsync(
                request.Query, 
                request.DocumentType, 
                request.TopK);
        }
        else
        {
            results = await _vectorStore.SearchAsync(
                request.Query, 
                request.TopK, 
                request.MinSimilarity);
        }

        return Ok(results);
    }
}

// Request DTOs
public record QuestionRequest(string Question, int MaxContextChunks = 3);
public record ConceptRequest(string Concept);
public record SearchRequest(string Query, int TopK = 5, double MinSimilarity = 0.7, string? DocumentType = null);
