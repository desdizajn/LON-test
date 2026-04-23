using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/boms")]
public class BomsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public BomsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetBOMs([FromQuery] Guid? itemId = null)
    {
        var query = _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .AsQueryable();
        if (itemId.HasValue) query = query.Where(b => b.ItemId == itemId.Value);

        var boms = await query.ToListAsync();
        return Ok(boms.Select(MasterDataMappings.MapBom).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBOM(Guid id)
    {
        var bom = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return NotFound();
        return Ok(MasterDataMappings.MapBom(bom));
    }

    [HttpGet("item/{itemId}")]
    public async Task<IActionResult> GetBOMsByItem(Guid itemId)
    {
        var boms = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .Where(b => b.ItemId == itemId)
            .ToListAsync();
        return Ok(boms.Select(MasterDataMappings.MapBom).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> CreateBOM([FromBody] BOMRequest request)
    {
        var item = await _context.Items.Include(i => i.BaseUoM).FirstOrDefaultAsync(i => i.Id == request.ItemId);
        if (item == null) return BadRequest(new { message = "Invalid item." });

        var version = MasterDataMappings.ParseVersion(request.Version);
        var bom = new BOM
        {
            Id = Guid.NewGuid(),
            Code = $"{item.Code}-BOM-{version}",
            ItemId = request.ItemId,
            Version = version,
            ValidFrom = request.ValidFrom ?? DateTime.UtcNow,
            ValidTo = request.ValidTo,
            IsActive = request.IsActive,
            BaseQuantity = request.Quantity
        };

        bom.Lines = request.Lines.Select(line => new BOMLine
        {
            Id = Guid.NewGuid(),
            BOMId = bom.Id,
            LineNumber = line.SequenceNumber,
            ItemId = line.ComponentItemId,
            Quantity = line.Quantity,
            UoMId = line.UoMId,
            ScrapPercentage = line.ScrapFactor,
            PrimaryWasteItemId = line.PrimaryWasteItemId,
            PrimaryWastePercentage = line.PrimaryWastePercentage,
            SecondaryWasteItemId = line.SecondaryWasteItemId,
            SecondaryWastePercentage = line.SecondaryWastePercentage,
            TertiaryWasteItemId = line.TertiaryWasteItemId,
            TertiaryWastePercentage = line.TertiaryWastePercentage,
            ZagubaItemId = line.ZagubaItemId,
            ZagubaPercentage = line.ZagubaPercentage
        }).ToList();

        _context.BOMs.Add(bom);
        await _context.SaveChangesAsync();

        bom = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .FirstAsync(b => b.Id == bom.Id);
        return Ok(MasterDataMappings.MapBom(bom));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBOM(Guid id, [FromBody] BOMRequest request)
    {
        var bom = await _context.BOMs
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return NotFound();

        bom.ItemId = request.ItemId;
        bom.Version = MasterDataMappings.ParseVersion(request.Version);
        bom.ValidFrom = request.ValidFrom ?? bom.ValidFrom;
        bom.ValidTo = request.ValidTo;
        bom.IsActive = request.IsActive;
        bom.BaseQuantity = request.Quantity;

        _context.BOMLines.RemoveRange(bom.Lines);
        bom.Lines = request.Lines.Select(line => new BOMLine
        {
            Id = Guid.NewGuid(),
            BOMId = bom.Id,
            LineNumber = line.SequenceNumber,
            ItemId = line.ComponentItemId,
            Quantity = line.Quantity,
            UoMId = line.UoMId,
            ScrapPercentage = line.ScrapFactor,
            PrimaryWasteItemId = line.PrimaryWasteItemId,
            PrimaryWastePercentage = line.PrimaryWastePercentage,
            SecondaryWasteItemId = line.SecondaryWasteItemId,
            SecondaryWastePercentage = line.SecondaryWastePercentage,
            TertiaryWasteItemId = line.TertiaryWasteItemId,
            TertiaryWastePercentage = line.TertiaryWastePercentage,
            ZagubaItemId = line.ZagubaItemId,
            ZagubaPercentage = line.ZagubaPercentage
        }).ToList();

        await _context.SaveChangesAsync();

        bom = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .FirstAsync(b => b.Id == id);
        return Ok(MasterDataMappings.MapBom(bom));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBOM(Guid id)
    {
        var bom = await _context.BOMs.FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null) return NotFound();

        bom.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
