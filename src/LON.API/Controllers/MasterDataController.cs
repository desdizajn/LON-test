using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class MasterDataController : BaseController
{
    private readonly ApplicationDbContext _context;

    public MasterDataController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] string? search = null)
    {
        var query = _context.Items
            .Include(i => i.BaseUoM)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(i => i.Code.Contains(search) || i.Name.Contains(search));

        var items = await query.ToListAsync();
        return Ok(items);
    }

    [HttpGet("items/{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var item = await _context.Items
            .Include(i => i.BaseUoM)
            .Include(i => i.UoMConversions)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses()
    {
        var warehouses = await _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => w.IsActive)
            .ToListAsync();

        return Ok(warehouses);
    }

    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations([FromQuery] Guid? warehouseId = null)
    {
        var query = _context.Locations
            .Include(l => l.Warehouse)
            .Where(l => l.IsActive)
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(l => l.WarehouseId == warehouseId.Value);

        var locations = await query.ToListAsync();
        return Ok(locations);
    }

    [HttpGet("partners")]
    public async Task<IActionResult> GetPartners([FromQuery] LON.Domain.Enums.PartnerType? type = null)
    {
        var query = _context.Partners
            .Where(p => p.IsActive)
            .AsQueryable();

        if (type.HasValue)
            query = query.Where(p => p.Type == type.Value);

        var partners = await query.ToListAsync();
        return Ok(partners);
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _context.Employees
            .Where(e => e.IsActive)
            .ToListAsync();

        return Ok(employees);
    }

    [HttpGet("work-centers")]
    public async Task<IActionResult> GetWorkCenters()
    {
        var workCenters = await _context.WorkCenters
            .Include(w => w.Machines)
            .Where(w => w.IsActive)
            .ToListAsync();

        return Ok(workCenters);
    }

    [HttpGet("uom")]
    public async Task<IActionResult> GetUnitsOfMeasure()
    {
        var uoms = await _context.UnitsOfMeasure.ToListAsync();
        return Ok(uoms);
    }
}
