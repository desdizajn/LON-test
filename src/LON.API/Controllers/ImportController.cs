using LON.Application.Importing.Commands.ApplyImportMapping;
using LON.Application.Importing.Commands.CreateKw12ImportBundle;
using LON.Application.Importing.Commands.DeleteMappingProfile;
using LON.Application.Importing.Commands.RunImport;
using LON.Application.Importing.Commands.SetImportDefaults;
using LON.Application.Importing.Commands.SetImportTransforms;
using LON.Application.Importing.Commands.UploadImportFile;
using LON.Application.Importing.DTOs;
using LON.Application.Importing.Queries.GetImportSession;
using LON.Application.Importing.Queries.GetImportTargets;
using LON.Application.Importing.Queries.ListImportSessions;
using LON.Application.Importing.Queries.PreviewTransformedRows;
using LON.Application.Importing.Queries.SuggestMappingProfiles;
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

    /// <summary>
    /// P6.34 — KW12 preset. One upload, one call. Parses every sheet in the
    /// workbook, creates one ImportSession per recognised sheet (Matriks →
    /// Items, Faktura → CustomsDeclarations, Transport → Receipts), and returns
    /// the ordered session IDs. Wizard then walks the user through
    /// mapping/defaults/commit for each session in sequence.
    /// </summary>
    [HttpPost("presets/kw12")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> CreateKw12Bundle(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { isSuccess = false, errorMessage = "No file provided." });
        if (file.Length > MaxUploadBytes)
            return BadRequest(new { isSuccess = false, errorMessage = $"File exceeds limit of {MaxUploadBytes / (1024 * 1024)} MB." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var result = await Mediator.Send(new CreateKw12ImportBundleCommand(bytes, file.FileName));
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

    // ---------- P5.1.2 — column mapping + named profiles ----------

    public record ApplyMappingRequest(
        ImportMapping Mapping,
        string TargetEntity,
        Guid? PartnerContextId,
        string? SaveAsProfileLabel);

    [HttpPut("sessions/{id}/mapping")]
    public async Task<IActionResult> ApplyMapping(Guid id, [FromBody] ApplyMappingRequest req)
    {
        var result = await Mediator.Send(new ApplyImportMappingCommand(
            id, req.Mapping, req.TargetEntity, req.PartnerContextId, req.SaveAsProfileLabel));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("mapping-profiles")]
    public async Task<IActionResult> SuggestProfiles(
        [FromQuery] string targetEntity,
        [FromQuery] Guid? partnerContextId = null)
    {
        var result = await Mediator.Send(new SuggestMappingProfilesQuery(targetEntity, partnerContextId));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("mapping-profiles/{id}")]
    public async Task<IActionResult> DeleteProfile(Guid id)
    {
        var result = await Mediator.Send(new DeleteMappingProfileCommand(id));
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    // ---------- P5.1.3 — header-level defaults ----------

    public record SetDefaultsRequest(ImportDefaults Defaults);

    [HttpPut("sessions/{id}/defaults")]
    public async Task<IActionResult> SetDefaults(Guid id, [FromBody] SetDefaultsRequest req)
    {
        var result = await Mediator.Send(new SetImportDefaultsCommand(id, req.Defaults));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    // ---------- P5.1.4 — column transforms ----------

    public record SetTransformsRequest(ImportTransforms Transforms);

    [HttpPut("sessions/{id}/transforms")]
    public async Task<IActionResult> SetTransforms(Guid id, [FromBody] SetTransformsRequest req)
    {
        var result = await Mediator.Send(new SetImportTransformsCommand(id, req.Transforms));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("sessions/{id}/preview-transformed")]
    public async Task<IActionResult> PreviewTransformed(Guid id, [FromQuery] int take = 20)
    {
        var result = await Mediator.Send(new PreviewTransformedRowsQuery(id, take));
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }

    // ---------- P5.1.6 — dry-run / commit ----------

    [HttpPost("sessions/{id}/dry-run")]
    public async Task<IActionResult> DryRun(Guid id)
    {
        var result = await Mediator.Send(new RunImportCommand(id, Commit: false));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("sessions/{id}/commit")]
    public async Task<IActionResult> Commit(Guid id)
    {
        var result = await Mediator.Send(new RunImportCommand(id, Commit: true));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    // ---------- P5.1.5 — target entity schemas ----------

    [HttpGet("targets")]
    public async Task<IActionResult> ListTargets()
    {
        var result = await Mediator.Send(new GetImportTargetsQuery());
        return Ok(result);
    }

    [HttpGet("targets/{name}")]
    public async Task<IActionResult> GetTarget(string name)
    {
        var result = await Mediator.Send(new GetImportTargetQuery(name));
        if (!result.IsSuccess) return NotFound(result);
        return Ok(result);
    }
}
