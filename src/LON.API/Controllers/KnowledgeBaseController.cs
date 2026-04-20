using LON.Application.Common.Interfaces;
using LON.Application.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IRAGService _ragService;
    private readonly IVectorStoreService _vectorStore;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<KnowledgeBaseController> _logger;

    public KnowledgeBaseController(
        IRAGService ragService,
        IVectorStoreService vectorStore,
        IApplicationDbContext context,
        ILogger<KnowledgeBaseController> logger)
    {
        _ragService = ragService;
        _vectorStore = vectorStore;
        _context = context;
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

    /// <summary>
    /// Health check - check KB status
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<object>> GetHealthStatus()
    {
        try
        {
            var tariffCount = await _context.TariffCodes.CountAsync();
            var regulationCount = await _context.CustomsRegulations.CountAsync();
            var codeListCount = await _context.CodeListItems.CountAsync();
            var ruleCount = await _context.DeclarationRules.CountAsync();

            var isSeeded = tariffCount > 0 || regulationCount > 0 || codeListCount > 0;

            // Check if OpenAI/Vector Store mode
            var isVectorStoreMode = false;
            try
            {
                var testResults = await _vectorStore.SearchAsync("test", 1, 0.0);
                isVectorStoreMode = testResults.Count > 0;
            }
            catch
            {
                // Vector store not available, using database mode
            }

            return Ok(new
            {
                isHealthy = isSeeded,
                status = isSeeded ? "Healthy" : "NoData",
                message = isSeeded
                    ? $"Knowledge Base е активна: {tariffCount} тарифни ознаки, {regulationCount} регулативи, {codeListCount} кодни листи, {ruleCount} правила"
                    : "Knowledge Base нема податоци. Рестартирајте ја апликацијата.",
                vectorStoreEnabled = isVectorStoreMode,
                data = new
                {
                    tariffCodes = tariffCount,
                    regulations = regulationCount,
                    codeLists = codeListCount,
                    validationRules = ruleCount
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Ok(new
            {
                isHealthy = false,
                status = "Error",
                message = "Грешка при проверка: " + ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Добиј статистики за Knowledge Base
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStatistics()
    {
        try
        {
            var tariffCount = await _context.TariffCodes.CountAsync();
            var regulationCount = await _context.CustomsRegulations.CountAsync();
            var codeListCount = await _context.CodeListItems.CountAsync();
            var ruleCount = await _context.DeclarationRules.CountAsync();
            var documentCount = await _context.KnowledgeDocuments.CountAsync();
            var chunkCount = await _context.KnowledgeDocumentChunks.CountAsync();

            return Ok(new
            {
                tariffCodes = tariffCount,
                regulations = regulationCount,
                codeLists = codeListCount,
                validationRules = ruleCount,
                documents = documentCount,
                chunks = chunkCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Пребарај тарифни ознаки
    /// </summary>
    [HttpGet("tariff-codes")]
    public async Task<ActionResult> SearchTariffCodes(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.TariffCodes.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.TariffNumber.Contains(search) ||
                t.TARBR.Contains(search) ||
                t.Description.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.TariffNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.TariffNumber,
                t.TARBR,
                t.TAROZ1,
                t.TAROZ2,
                t.TAROZ3,
                t.Description,
                t.CustomsRate,
                t.VATRate,
                t.UnitMeasure
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>
    /// Пребарај регулативи
    /// </summary>
    [HttpGet("regulations")]
    public async Task<ActionResult> SearchRegulations(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.CustomsRegulations.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                r.CelexNumber.Contains(search) ||
                r.TariffNumber.Contains(search) ||
                (r.DescriptionMK != null && r.DescriptionMK.Contains(search)) ||
                (r.DescriptionEN != null && r.DescriptionEN.Contains(search)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(r => r.CelexNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.CelexNumber,
                r.TariffNumber,
                r.DescriptionMK,
                r.DescriptionEN,
                r.LegalBasis,
                r.EffectiveDate,
                r.ExpiryDate
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>
    /// Добиј кодни листи
    /// </summary>
    [HttpGet("code-lists")]
    public async Task<ActionResult> GetCodeLists(
        [FromQuery] string? listType = null,
        [FromQuery] string? search = null)
    {
        var query = _context.CodeListItems.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(listType))
        {
            query = query.Where(c => c.ListType == listType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Code.Contains(search) ||
                c.DescriptionMK.Contains(search) ||
                (c.DescriptionEN != null && c.DescriptionEN.Contains(search)));
        }

        var items = await query
            .OrderBy(c => c.ListType)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .Select(c => new
            {
                c.Id,
                c.ListType,
                c.Code,
                c.DescriptionMK,
                c.DescriptionEN,
                c.SortOrder
            })
            .ToListAsync();

        // Group by list type for easier consumption
        var grouped = items.GroupBy(c => c.ListType)
            .ToDictionary(g => g.Key, g => g.ToList());

        return Ok(new { listTypes = grouped.Keys, items = grouped, totalItems = items.Count });
    }

    /// <summary>
    /// Добиј правила за валидација
    /// </summary>
    [HttpGet("validation-rules")]
    public async Task<ActionResult> GetValidationRules(
        [FromQuery] string? fieldName = null)
    {
        var query = _context.DeclarationRules.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            query = query.Where(r => r.FieldName.Contains(fieldName));
        }

        var items = await query
            .OrderBy(r => r.Priority)
            .Select(r => new
            {
                r.Id,
                r.RuleCode,
                r.FieldName,
                r.RuleType,
                r.ValidationLogic,
                r.ErrorMessageMK,
                r.ErrorMessageEN,
                r.Severity,
                r.Priority
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Create code list item
    /// </summary>
    [HttpPost("code-lists/items")]
    public async Task<ActionResult> CreateCodeListItem([FromBody] CodeListItemRequest request)
    {
        var item = new LON.Domain.Entities.MasterData.CodeListItem
        {
            Id = Guid.NewGuid(),
            ListType = request.ListType,
            Code = request.Code,
            DescriptionMK = request.DescriptionMK,
            DescriptionEN = request.DescriptionEN,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "User"
        };

        _context.CodeListItems.Add(item);
        await _context.SaveChangesAsync();
        return Ok(new { item.Id, item.ListType, item.Code, item.DescriptionMK, item.DescriptionEN, item.SortOrder });
    }

    /// <summary>
    /// Update code list item
    /// </summary>
    [HttpPut("code-lists/items/{id}")]
    public async Task<ActionResult> UpdateCodeListItem(Guid id, [FromBody] CodeListItemRequest request)
    {
        var item = await _context.CodeListItems.FirstOrDefaultAsync(c => c.Id == id);
        if (item == null) return NotFound();

        item.ListType = request.ListType;
        item.Code = request.Code;
        item.DescriptionMK = request.DescriptionMK;
        item.DescriptionEN = request.DescriptionEN;
        item.SortOrder = request.SortOrder;
        item.ModifiedAt = DateTime.UtcNow;
        item.ModifiedBy = "User";

        await _context.SaveChangesAsync();
        return Ok(new { item.Id, item.ListType, item.Code, item.DescriptionMK, item.DescriptionEN, item.SortOrder });
    }

    /// <summary>
    /// Delete code list item
    /// </summary>
    [HttpDelete("code-lists/items/{id}")]
    public async Task<ActionResult> DeleteCodeListItem(Guid id)
    {
        var item = await _context.CodeListItems.FirstOrDefaultAsync(c => c.Id == id);
        if (item == null) return NotFound();

        item.IsActive = false;
        item.ModifiedAt = DateTime.UtcNow;
        item.ModifiedBy = "User";

        await _context.SaveChangesAsync();
        return Ok();
    }
}

// Request DTOs
// NOTE: init-only properties (not positional records) so System.Text.Json can
// bind bodies like {"query":"..."} without tripping the primary-constructor
// "request field is required" path for records with default values. See P6.42.
public record QuestionRequest
{
    public string Question { get; init; } = string.Empty;
    public int MaxContextChunks { get; init; } = 3;
}

public record ConceptRequest
{
    public string Concept { get; init; } = string.Empty;
}

public record SearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int TopK { get; init; } = 5;
    public double MinSimilarity { get; init; } = 0.7;
    public string? DocumentType { get; init; }
}

public record CodeListItemRequest
{
    public string ListType { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string DescriptionMK { get; init; } = string.Empty;
    public string? DescriptionEN { get; init; }
    public int SortOrder { get; init; }
}
