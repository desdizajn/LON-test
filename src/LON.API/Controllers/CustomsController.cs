using LON.Application.Customs.Commands.CreateCustomsDeclaration;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class CustomsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public CustomsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("declarations")]
    public async Task<IActionResult> CreateDeclaration([FromBody] CreateCustomsDeclarationCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
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
