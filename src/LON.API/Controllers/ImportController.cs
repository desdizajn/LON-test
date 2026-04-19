using LON.Application.Importing.Commands.UploadImportFile;
using LON.Application.Importing.Queries.GetImportSession;
using LON.Application.Importing.Queries.ListImportSessions;
using Microsoft.AspNetCore.Mvc;

namespace LON.API.Controllers;

/// <summary>
/// P5.1 generic importer endpoints. Wizard flow:
///   1. POST /sessions        — upload + parse + preview
///   2. GET  /sessions/{id}   — fetch headers + preview + current state
///   3. GET  /sessions        — list recent sessions
/// Later sub-tasks will add PUT /sessions/{id}/mapping, /defaults, /transforms,
/// POST /sessions/{id}/dry-run, POST /sessions/{id}/commit.
/// </summary>
[Route("api/import")]
public class ImportController : BaseController
{
    /// <summary>Reject uploads larger than 25 MB — fits every ELON tblTransfer* dump
    /// we've seen while keeping memory pressure bounded (grid is materialised in-process).</summary>
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    [HttpPost("sessions")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> UploadSession(
        IFormFile file,
        [FromQuery] string? targetEntity = null,
        [FromQuery] Guid? partnerContextId = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { isSuccess = false, errorMessage = "No file provided." });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { isSuccess = false, errorMessage = $"File exceeds limit of {MaxUploadBytes / (1024 * 1024)} MB." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var result = await Mediator.Send(new UploadImportFileCommand(
            bytes, file.FileName, targetEntity, partnerContextId));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("sessions/{id}")]
    public async Task<IActionResult> GetSession(Guid id)
    {
        var result = await Mediator.Send(new GetImportSessionQuery(id));
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions([FromQuery] int take = 50)
    {
        var result = await Mediator.Send(new ListImportSessionsQuery(take));
        return Ok(result);
    }
}
