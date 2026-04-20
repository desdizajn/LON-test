using LON.API.MasterData;
using LON.Application.MasterData.Commands.BackfillItemBaseVariants;
using LON.Application.MasterData.Queries.GetItemImportAttributes;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

/// <summary>
/// P6.10 — Items split from the old monolithic MasterDataController.
/// Routes stay <c>/api/MasterData/items</c> to preserve the public URL contract
/// (frontend + integration tests are unchanged). The 2 P6.30/P6.31 endpoints
/// that already went through MediatR remain on MediatR.
/// </summary>
[Route("api/MasterData/items")]
public class ItemsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public ItemsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems([FromQuery] string? search = null)
    {
        var query = _context.Items
            .Include(i => i.BaseUoM)
            .Where(i => !i.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(i => i.Code.Contains(search) || i.Name.Contains(search));

        var items = await query.ToListAsync();
        return Ok(items.Select(MasterDataMappings.MapItem).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var item = await _context.Items
            .Include(i => i.BaseUoM)
            .Include(i => i.UoMConversions)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null) return NotFound();
        return Ok(MasterDataMappings.MapItem(item));
    }

    [HttpPost]
    public async Task<IActionResult> CreateItem([FromBody] ItemRequest request)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Type = request.ItemType,
            IsBatchTracked = request.IsBatchRequired,
            IsMRNTracked = request.IsMRNRequired,
            HSCode = request.HSCode,
            CountryOfOrigin = request.CountryOfOrigin,
            BaseUoMId = request.UoMId,
            StandardCost = request.StandardCost ?? 0m,
            IsDeleted = !request.IsActive
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        item = await _context.Items.Include(i => i.BaseUoM).FirstAsync(i => i.Id == item.Id);
        return Ok(MasterDataMappings.MapItem(item));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] ItemRequest request)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id);
        if (item == null) return NotFound();

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description ?? string.Empty;
        item.Type = request.ItemType;
        item.IsBatchTracked = request.IsBatchRequired;
        item.IsMRNTracked = request.IsMRNRequired;
        item.HSCode = request.HSCode;
        item.CountryOfOrigin = request.CountryOfOrigin;
        item.BaseUoMId = request.UoMId;
        item.StandardCost = request.StandardCost ?? item.StandardCost;
        item.IsDeleted = !request.IsActive;

        await _context.SaveChangesAsync();

        item = await _context.Items.Include(i => i.BaseUoM).FirstAsync(i => i.Id == item.Id);
        return Ok(MasterDataMappings.MapItem(item));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id);
        if (item == null) return NotFound();

        item.IsDeleted = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// P6.30 — one-shot backfill of BaseCode/ColorCode/SizeCode/ParentItemId
    /// on legacy items migrated from ELON before KW12 variant model existed.
    /// Admin-only; idempotent (second run is a no-op).
    /// </summary>
    [HttpPost("backfill-base-variants")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> BackfillBaseVariants([FromQuery] bool dryRun = false)
    {
        var result = await Mediator.Send(new BackfillItemBaseVariantsCommand(dryRun));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P6.31 — for a given Item, returns the distinct (TariffCode, CountryOfOrigin,
    /// IsPreferentialOrigin, Supplier, DutyRate, VATRate) tuples across all
    /// active-MRN CustomsDeclarationLines plus the aggregate stock per tuple.
    /// </summary>
    [HttpGet("{id}/import-attributes")]
    public async Task<IActionResult> GetItemImportAttributes(Guid id)
    {
        var result = await Mediator.Send(new GetItemImportAttributesQuery(id));
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }
}
