using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/locations")]
public class LocationsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public LocationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetLocations([FromQuery] Guid? warehouseId = null)
    {
        var query = _context.Locations
            .Include(l => l.Warehouse)
            .Where(l => l.IsActive)
            .AsQueryable();
        if (warehouseId.HasValue) query = query.Where(l => l.WarehouseId == warehouseId.Value);

        var locations = await query.ToListAsync();
        return Ok(locations.Select(MasterDataMappings.MapLocation).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLocation(Guid id)
    {
        var location = await _context.Locations
            .Include(l => l.Warehouse)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound();
        return Ok(MasterDataMappings.MapLocation(location));
    }

    [HttpPost]
    public async Task<IActionResult> CreateLocation([FromBody] LocationRequest request)
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            WarehouseId = request.WarehouseId,
            Code = request.Code,
            Name = request.Name,
            Type = request.LocationType,
            Aisle = request.Aisle,
            Rack = request.Rack,
            Shelf = request.Shelf,
            Bin = request.Bin,
            MaxCapacity = request.MaxCapacity,
            IsActive = request.IsActive
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();

        location = await _context.Locations.Include(l => l.Warehouse).FirstAsync(l => l.Id == location.Id);
        return Ok(MasterDataMappings.MapLocation(location));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] LocationRequest request)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound();

        location.WarehouseId = request.WarehouseId;
        location.Code = request.Code;
        location.Name = request.Name;
        location.Type = request.LocationType;
        location.Aisle = request.Aisle;
        location.Rack = request.Rack;
        location.Shelf = request.Shelf;
        location.Bin = request.Bin;
        location.MaxCapacity = request.MaxCapacity;
        location.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        location = await _context.Locations.Include(l => l.Warehouse).FirstAsync(l => l.Id == id);
        return Ok(MasterDataMappings.MapLocation(location));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location == null) return NotFound();

        location.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
