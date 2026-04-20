using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/uom")]
public class UoMsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public UoMsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnitsOfMeasure()
    {
        var uoms = await _context.UnitsOfMeasure.Where(u => !u.IsDeleted).ToListAsync();
        return Ok(uoms.Select(MasterDataMappings.MapUoM).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUnitOfMeasure(Guid id)
    {
        var uom = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id);
        if (uom == null) return NotFound();
        return Ok(MasterDataMappings.MapUoM(uom));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUnitOfMeasure([FromBody] UoMRequest request)
    {
        var uom = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Symbol = request.Description,
            IsDeleted = request.IsActive == false
        };

        _context.UnitsOfMeasure.Add(uom);
        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapUoM(uom));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUnitOfMeasure(Guid id, [FromBody] UoMRequest request)
    {
        var uom = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id);
        if (uom == null) return NotFound();

        uom.Code = request.Code;
        uom.Name = request.Name;
        uom.Symbol = request.Description;
        uom.IsDeleted = request.IsActive == false;

        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapUoM(uom));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUnitOfMeasure(Guid id)
    {
        var uom = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id);
        if (uom == null) return NotFound();

        uom.IsDeleted = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
