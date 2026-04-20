using LON.Application.QuickEntry;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P5.2.8 — single-line quick-entry bar. Power users type a command string
/// (e.g. <c>issue PO-123</c>, <c>release PO-456</c>, <c>move BATCH-7 production</c>)
/// and the server dispatches to the matching MediatR command. Base auth
/// + base route (api/QuickEntry) inherited from BaseController.
/// </summary>
public class QuickEntryController : BaseController
{
    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] QuickEntryRequest req)
    {
        var result = await Mediator.Send(new QuickEntryCommand(req.Command ?? ""));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    public record QuickEntryRequest
    {
        public string? Command { get; init; }
    }
}
