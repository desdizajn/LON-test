using LON.Application.Ai;
using LON.Application.KnowledgeBase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E10 — surface area for the floating AI helper drawer. Two
/// concerns split across endpoints:
///   • <c>POST /recommendations</c> — deterministic structured nudges
///     surfaced by <see cref="IAiAssistantService"/> (no LLM call).
///   • <c>POST /ask</c> — free-form Q&amp;A that wraps the existing
///     <see cref="IRAGService"/> so the helper drawer's chat tab does
///     not need a separate endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiAssistantService _ai;
    private readonly IRAGService _rag;

    public AiController(IAiAssistantService ai, IRAGService rag)
    {
        _ai = ai;
        _rag = rag;
    }

    [HttpPost("recommendations")]
    public async Task<ActionResult<List<Recommendation>>> GetRecommendations(
        [FromBody] RecommendationsRequest body,
        CancellationToken ct)
    {
        if (body is null) return BadRequest("Body is required.");
        if (string.IsNullOrWhiteSpace(body.EntityType)) return BadRequest("entityType is required.");
        if (body.EntityId == Guid.Empty) return BadRequest("entityId is required.");

        var recs = await _ai.GetRecommendationsAsync(body.EntityType, body.EntityId, ct);
        return Ok(recs);
    }

    [HttpPost("suggestions/{id:guid}/acted")]
    public async Task<IActionResult> MarkActed(Guid id, CancellationToken ct)
    {
        var ok = await _ai.MarkActedAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("suggestions/{id:guid}/dismissed")]
    public async Task<IActionResult> MarkDismissed(Guid id, CancellationToken ct)
    {
        var ok = await _ai.MarkDismissedAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Free-form Q&amp;A. Proxies straight to <see cref="IRAGService"/> so the
    /// drawer's chat tab works without the frontend talking to two different
    /// API surfaces.
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<RAGResponse>> Ask([FromBody] AskRequest body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Question))
            return BadRequest("question is required.");
        var resp = await _rag.AskQuestionAsync(body.Question, Math.Clamp(body.MaxContextChunks, 1, 8));
        return Ok(resp);
    }
}

public record RecommendationsRequest
{
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string? Mode { get; init; }
}

public record AskRequest
{
    public string Question { get; init; } = string.Empty;
    public int MaxContextChunks { get; init; } = 3;
}
