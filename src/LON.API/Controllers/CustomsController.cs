using LON.Application.Customs.Commands.BulkUpdateCustomsDeclarationLines;
using LON.Application.Customs.Commands.CertifyDeclaration;
using LON.Application.Customs.Commands.CreateCustomsDeclaration;
using LON.Application.Customs.Commands.CreateExportDeclaration;
using LON.Application.Customs.Commands.CreateReturnDeclaration;
using LON.Application.Customs.Commands.CreateWasteDeclaration;
using LON.Application.Customs.Commands.UpdateCustomsDeclaration;
using LON.Application.Customs.Validation;
using LON.Domain.Entities.Customs;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class CustomsController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly IDeclarationRuleEngine _ruleEngine;

    public CustomsController(ApplicationDbContext context, IDeclarationRuleEngine ruleEngine)
    {
        _context = context;
        _ruleEngine = ruleEngine;
    }

    [HttpPost("declarations")]
    public async Task<IActionResult> CreateDeclaration([FromBody] CreateCustomsDeclarationCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// Updates a Draft declaration. Declarations past Draft status are
    /// rejected with <c>409 Conflict</c> — see
    /// <see cref="UpdateCustomsDeclarationCommandHandler"/> for the full
    /// compliance rationale. Full amendment workflow is deferred.
    /// </summary>
    [HttpPut("declarations/{id:guid}")]
    public async Task<IActionResult> UpdateDeclaration(Guid id, [FromBody] UpdateCustomsDeclarationCommand command)
    {
        if (command.Id != Guid.Empty && command.Id != id)
            return BadRequest(new { errorMessage = "Route id and body id do not match." });

        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        if (result.IsSuccess)
            return Ok(result);
        return Conflict(result);
    }

    /// <summary>
    /// Phase 17 §E0 — bulk-update one whitelisted field across every line of a
    /// Draft declaration. Whitelist enforced server-side (see
    /// <see cref="BulkUpdateCustomsDeclarationLinesCommandHandler"/>).
    /// One <c>AuditLogEntry</c> per affected line.
    /// </summary>
    [HttpPost("declarations/{id:guid}/lines/bulk-update")]
    public async Task<IActionResult> BulkUpdateDeclarationLines(
        Guid id,
        [FromBody] BulkUpdateCustomsDeclarationLinesCommand command)
    {
        if (command.DeclarationId != Guid.Empty && command.DeclarationId != id)
            return BadRequest(new { errorMessage = "Route id and body declarationId do not match." });

        var effective = command with { DeclarationId = id };
        var result = await Mediator.Send(effective);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// P2.6a — Register an EX (re-export) declaration. Discharges LON bond
    /// on the referenced IM MRNs, transitions Imported/InProduction balances
    /// to Exported, credits the guarantee ledger proportionally, and
    /// decrements FG inventory.
    /// </summary>
    [HttpPost("declarations/export")]
    public async Task<IActionResult> CreateExportDeclaration([FromBody] CreateExportDeclarationCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// P2.6b — Register a Return declaration reversing a previous EX.
    /// Un-discharges the MRN (drops DischargedQuantity), restores Exported
    /// balances back to Imported/InProduction, re-debits the guarantee ledger
    /// proportionally, and re-intakes FG inventory.
    /// </summary>
    [HttpPost("declarations/return")]
    public async Task<IActionResult> CreateReturnDeclaration([FromBody] CreateReturnDeclarationCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// P2.6c — Books physical residual LON-state inventory off as Waste.
    /// No guarantee impact in v1 (bond tracks declared quantity, not inflate).
    /// </summary>
    [HttpPost("declarations/waste")]
    public async Task<IActionResult> CreateWasteDeclaration([FromBody] CreateWasteDeclarationCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    /// <summary>
    /// P4.1 — Zaverka (customs certification). Inspector stamps the declaration;
    /// sets ZaverkaNumber/Date + Status=Cleared. Idempotent attempts on an
    /// already-cleared declaration return 400.
    /// </summary>
    [HttpPost("declarations/{id:guid}/certify")]
    public async Task<IActionResult> CertifyDeclaration(Guid id, [FromBody] CertifyDeclarationBody body)
    {
        var cmd = new CertifyDeclarationCommand(id, body.ZaverkaNumber, body.ZaverkaDate);
        var result = await Mediator.Send(cmd);
        if (result.IsSuccess) return Ok(result);
        return BadRequest(result);
    }

    public record CertifyDeclarationBody(string ZaverkaNumber, DateTime ZaverkaDate);

    /// <summary>
    /// P4.2 — Generate PEE060 monthly zadolzuvanje/razdolzuvanje XML for a LON
    /// authorization over a date window. Returns the XML as a file download.
    /// </summary>
    [HttpGet("pee/060")]
    public async Task<IActionResult> GeneratePee060(
        [FromQuery] Guid authorizationId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var result = await Mediator.Send(
            new LON.Application.Customs.Queries.GeneratePee060Xml.GeneratePee060XmlQuery(authorizationId, from, to));
        if (!result.IsSuccess || result.Data == null) return BadRequest(result);
        return File(System.Text.Encoding.UTF8.GetBytes(result.Data.Xml),
            "application/xml", result.Data.FileName);
    }

    /// <summary>
    /// Validate a declaration without persisting it
    /// </summary>
    [HttpPost("declarations/validate")]
    public async Task<IActionResult> ValidateDeclaration([FromBody] CreateCustomsDeclarationCommand command)
    {
        var procedureCode = await _context.CustomsProcedures
            .Where(p => p.Id == command.CustomsProcedureId)
            .Select(p => p.Code)
            .FirstOrDefaultAsync() ?? string.Empty;

        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = command.DeclarationNumber,
            MRN = command.MRN ?? string.Empty,
            DeclarationDate = command.DeclarationDate,
            CustomsProcedureId = command.CustomsProcedureId,
            ProcedureCode = procedureCode,
            PartnerId = command.PartnerId,
            LONAuthorizationId = command.LONAuthorizationId,
            SenderName = command.SenderName,
            SenderAddress = command.SenderAddress,
            SenderCountry = command.SenderCountry,
            CountryOfDispatch = command.CountryOfDispatch,
            CountryOfDestination = command.CountryOfDestination,
            SpecialRemarks = command.SpecialRemarks,
            TotalCustomsValue = command.TotalCustomsValue,
            Currency = command.Currency,
            DueDate = command.DueDate,
            IsCleared = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Validation"
        };

        decimal totalDuty = 0;
        decimal totalVAT = 0;
        int lineNumber = 1;

        foreach (var lineDto in command.Lines)
        {
            var dutyAmount = lineDto.CustomsValue * lineDto.DutyRate / 100;
            var vatAmount = (lineDto.CustomsValue + dutyAmount) * lineDto.VATRate / 100;

            var line = new CustomsDeclarationLine
            {
                Id = Guid.NewGuid(),
                CustomsDeclarationId = declaration.Id,
                LineNumber = lineNumber++,
                ItemId = lineDto.ItemId,
                TariffCode = lineDto.TariffCode,
                Quantity = lineDto.Quantity,
                UoMId = lineDto.UoMId,
                CustomsValue = lineDto.CustomsValue,
                CountryOfOrigin = lineDto.CountryOfOrigin,
                DutyRate = lineDto.DutyRate,
                DutyAmount = dutyAmount,
                VATRate = lineDto.VATRate,
                VATAmount = vatAmount,
                OtherCharges = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Validation"
            };

            totalDuty += dutyAmount;
            totalVAT += vatAmount;
            declaration.Lines.Add(line);
        }

        declaration.TotalDuty = totalDuty;
        declaration.TotalVAT = totalVAT;
        declaration.TotalOtherCharges = 0;

        var validationResult = await _ruleEngine.ValidateAsync(declaration);

        return Ok(new
        {
            validationResult.IsValid,
            validationResult.ValidationTime,
            Errors = validationResult.Errors.Select(e => new { e.Message, e.ReferenceDocument, e.SuggestedValue }),
            Warnings = validationResult.Warnings.Select(w => new { w.Message, w.ReferenceDocument }),
            Summary = validationResult.GetSummary(),
            RulesChecked = validationResult.RuleResults.Count
        });
    }

    /// <summary>
    /// What-if duty calculator. Given a tariff code + customs value + rate +
    /// date (+ optional preferential flag), returns the full duty + VAT
    /// breakdown using the legacy PresmetajDavackiPoNaim formula. Uses
    /// year-indexed TariffCodeRate when available, falls back to base
    /// TariffCode rates with a warning.
    /// </summary>
    [HttpPost("duty-calculator")]
    public async Task<IActionResult> DutyCalculator(
        [FromBody] LON.Application.Customs.Queries.DutyWhatIf.DutyWhatIfQuery query)
    {
        var result = await Mediator.Send(query);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Lookup tariff code information
    /// </summary>
    [HttpGet("tariff-lookup/{tariffCode}")]
    public async Task<IActionResult> TariffLookup(string tariffCode)
    {
        var tariff = await _context.TariffCodes
            .Where(t => t.IsActive && (t.TariffNumber == tariffCode || t.TARBR == tariffCode))
            .Select(t => new
            {
                t.Id,
                t.TariffNumber,
                t.TARBR,
                t.Description,
                t.CustomsRate,
                t.VATRate,
                t.UnitMeasure,
                t.TAROZ1,
                t.TAROZ2,
                t.TAROZ3
            })
            .FirstOrDefaultAsync();

        if (tariff == null)
            return NotFound(new { Message = $"Tariff code '{tariffCode}' not found." });

        return Ok(tariff);
    }

    [HttpGet("declarations")]
    public async Task<IActionResult> GetDeclarations([FromQuery] bool? isCleared = null)
    {
        var query = _context.CustomsDeclarations
            .Include(d => d.CustomsProcedure)
            .Include(d => d.Partner)
            .Include(d => d.Lines)
            .AsQueryable();

        if (isCleared.HasValue)
            query = query.Where(d => d.IsCleared == isCleared.Value);

        var declarations = await query.OrderByDescending(d => d.DeclarationDate).ToListAsync();
        return Ok(declarations);
    }

    [HttpGet("declarations/{id}")]
    public async Task<IActionResult> GetDeclaration(Guid id)
    {
        var declaration = await _context.CustomsDeclarations
            .Include(d => d.CustomsProcedure)
            .Include(d => d.Partner)
            .Include(d => d.Lines)
            .ThenInclude(l => l.Item)
            .Include(d => d.Documents)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (declaration == null)
            return NotFound();

        return Ok(declaration);
    }

    /// <summary>
    /// P15.4 — NaimU5 rollup. Groups the declaration's lines by
    /// (TariffCode, UoM, CountryOfOrigin) and returns one aggregate row
    /// per naimenovanie with summed qty/value/duty/VAT. Used by PEE XML
    /// + customs register printouts + the declaration-detail UI tab.
    /// </summary>
    [HttpGet("declarations/{id:guid}/naim")]
    public async Task<IActionResult> GetDeclarationNaim(Guid id)
    {
        var rows = await Mediator.Send(
            new LON.Application.Customs.Queries.GetDeclarationNaim.GetDeclarationNaimQuery(id));
        return Ok(rows);
    }

    // ---------- P15.11 Legacy reports ----------

    /// <summary>
    /// P15.11 — legacy rptRazdolzuvanje. Per-LON-authorization release
    /// summary for a date window: debit (IM), credit (EX/Return/Waste),
    /// net outstanding per MRN.
    /// </summary>
    [HttpGet("reports/razdolzuvanje")]
    public async Task<IActionResult> RazdolzuvanjeReport(
        [FromQuery] Guid authorizationId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var report = await Mediator.Send(
            new LON.Application.Customs.Queries.LegacyReports.RazdolzuvanjeReportQuery(authorizationId, from, to));
        return Ok(report);
    }

    /// <summary>
    /// P15.11 — legacy rptG20-G30Mesecno. Monthly customs register grouped
    /// by (month, procedure code) for a year.
    /// </summary>
    [HttpGet("reports/monthly-register")]
    public async Task<IActionResult> MonthlyCustomsRegister([FromQuery] int year)
    {
        var rows = await Mediator.Send(
            new LON.Application.Customs.Queries.LegacyReports.MonthlyCustomsRegisterQuery(year));
        return Ok(rows);
    }

    /// <summary>
    /// P15.11 — legacy rptOtpad. Waste register for a date window.
    /// </summary>
    [HttpGet("reports/waste-register")]
    public async Task<IActionResult> WasteRegister(
        [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var rows = await Mediator.Send(
            new LON.Application.Customs.Queries.LegacyReports.WasteRegisterQuery(from, to));
        return Ok(rows);
    }

    // ---------- P15.12 / P15.13 / P15.14 / P15.15 PEE XML family ----------

    /// <summary>
    /// P15.12+ — generate one of the PEE envelopes (PEE010 / PEE020 / PEE040
    /// / PEE050) for a given declaration. Returns file name + XML body;
    /// frontend offers it as a download the operator uploads to the customs
    /// portal (legacy Notepad-save path).
    /// </summary>
    [HttpGet("declarations/{id:guid}/pee/{envelope}")]
    public async Task<IActionResult> GeneratePeeXml(Guid id, string envelope)
    {
        var result = await Mediator.Send(
            new LON.Application.Customs.Queries.GeneratePeeXml.GeneratePeeXmlQuery(id, envelope));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// P15.13.1 — upload a PEE020 response XML from the customs portal. The
    /// handler parses it, matches the declaration by MRN (or DeclarationNumber
    /// fallback), and stamps ZaverkaNumber + ZaverkaDate via the shared
    /// CertifyDeclaration pipeline. One-click automated clearance replaces
    /// the legacy manual Notepad-copy-paste flow.
    /// </summary>
    [HttpPost("pee/020/parse")]
    public async Task<IActionResult> ParsePee020([FromBody] Pee020Upload request)
    {
        var result = await Mediator.Send(
            new LON.Application.Customs.Commands.ParsePee020.ParsePee020Command
            {
                XmlContent = request.XmlContent ?? string.Empty
            });
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    public class Pee020Upload
    {
        public string? XmlContent { get; set; }
    }

    [HttpGet("procedures")]
    public async Task<IActionResult> GetProcedures()
    {
        var procedures = await _context.CustomsProcedures
            .Include(p => p.RequiredDocuments)
            .Where(p => p.IsActive)
            .ToListAsync();

        return Ok(procedures);
    }

    /// <summary>
    /// List LON authorizations (Odobrenija) available to the current tenant.
    /// Used by the declaration form to populate the LONAuthorizationId picker
    /// for procedure codes 4200 / 5100.
    /// </summary>
    [HttpGet("lon-authorizations")]
    public async Task<IActionResult> GetLONAuthorizations([FromQuery] bool activeOnly = true)
    {
        var query = _context.LONAuthorizations
            .Include(a => a.Partner)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(a => a.Status == "Active" &&
                                     (!a.ExpiryDate.HasValue || a.ExpiryDate >= DateTime.UtcNow.Date));

        var list = await query
            .OrderByDescending(a => a.IssueDate)
            .Select(a => new
            {
                a.Id,
                a.AuthorizationNumber,
                a.AuthorizationType,
                a.SystemType,
                a.OperationType,
                a.IssueDate,
                a.ExpiryDate,
                a.GuaranteeAmount,
                a.GuaranteeReference,
                a.CompetentCustomsOffice,
                a.Status,
                PartnerName = a.Partner.Name
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("mrn-registry")]
    public async Task<IActionResult> GetMRNRegistry([FromQuery] string? mrn = null, [FromQuery] bool? isActive = null)
    {
        var query = _context.MRNRegistries
            .Include(m => m.CustomsDeclaration)
            .AsQueryable();

        if (!string.IsNullOrEmpty(mrn))
            query = query.Where(m => m.MRN.Contains(mrn));

        if (isActive.HasValue)
            query = query.Where(m => m.IsActive == isActive.Value);

        var registry = await query.ToListAsync();
        return Ok(registry);
    }

    [HttpGet("mrn-registry/{mrn}")]
    public async Task<IActionResult> GetMRNByNumber(string mrn)
    {
        var mrnRecord = await _context.MRNRegistries
            .Include(m => m.CustomsDeclaration)
            .ThenInclude(d => d!.Lines)
            .FirstOrDefaultAsync(m => m.MRN == mrn);

        if (mrnRecord == null)
            return NotFound();

        return Ok(mrnRecord);
    }
}
