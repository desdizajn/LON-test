using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/warehouses")]
public class WarehousesController : BaseController
{
    private readonly ApplicationDbContext _context;

    public WarehousesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetWarehouses()
    {
        var warehouses = await _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => w.IsActive)
            .ToListAsync();
        return Ok(warehouses.Select(MasterDataMappings.MapWarehouse).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWarehouse(Guid id)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.Locations)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (warehouse == null) return NotFound();
        return Ok(MasterDataMappings.MapWarehouse(warehouse));
    }

    [HttpPost]
    public async Task<IActionResult> CreateWarehouse([FromBody] WarehouseRequest request)
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Address = request.Address ?? string.Empty,
            IsActive = request.IsActive
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapWarehouse(warehouse));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] WarehouseRequest request)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id);
        if (warehouse == null) return NotFound();

        warehouse.Code = request.Code;
        warehouse.Name = request.Name;
        warehouse.Address = request.Address ?? string.Empty;
        warehouse.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapWarehouse(warehouse));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWarehouse(Guid id)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id);
        if (warehouse == null) return NotFound();

        warehouse.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
