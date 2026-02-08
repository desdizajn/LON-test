using LON.Application.Customs.Commands.CreateCustomsDeclaration;
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
    /// Validate a declaration without persisting it
    /// </summary>
    [HttpPost("declarations/validate")]
    public async Task<IActionResult> ValidateDeclaration([FromBody] CreateCustomsDeclarationCommand command)
    {
        var declaration = new CustomsDeclaration
        {
            Id = Guid.NewGuid(),
            DeclarationNumber = command.DeclarationNumber,
            MRN = command.MRN,
            DeclarationDate = command.DeclarationDate,
            CustomsProcedureId = command.CustomsProcedureId,
            PartnerId = command.PartnerId,
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

    [HttpGet("procedures")]
    public async Task<IActionResult> GetProcedures()
    {
        var procedures = await _context.CustomsProcedures
            .Include(p => p.RequiredDocuments)
            .Where(p => p.IsActive)
            .ToListAsync();

        return Ok(procedures);
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
