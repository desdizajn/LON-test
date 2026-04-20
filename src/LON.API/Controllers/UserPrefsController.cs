using LON.Application.UserPrefs;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P5.3.5 — per-user recent-values cache. Any form can call:
///   GET  /api/UserPrefs/field-history?fieldKey=...&limit=10
///   POST /api/UserPrefs/field-history  { fieldKey, value }
/// The UI wires a datalist to the GET result; the POST runs on submit.
/// </summary>
public class UserPrefsController : BaseController
{
    [HttpGet("field-history")]
    public async Task<IActionResult> GetFieldHistory([FromQuery] string fieldKey, [FromQuery] int limit = 10)
    {
        var result = await Mediator.Send(new GetUserFieldHistoryQuery(fieldKey, limit));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("field-history")]
    public async Task<IActionResult> RecordFieldValue([FromBody] RecordFieldValueRequest req)
    {
        var result = await Mediator.Send(new RecordUserFieldValueCommand(req.FieldKey ?? "", req.Value ?? ""));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    public record RecordFieldValueRequest
    {
        public string? FieldKey { get; init; }
        public string? Value { get; init; }
    }
}
