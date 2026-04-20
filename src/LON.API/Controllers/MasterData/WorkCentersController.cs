using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

// Both route prefixes preserved from the old monolith for backwards compat.
[Route("api/MasterData/workcenters")]
[Route("api/MasterData/work-centers")]
public class WorkCentersController : BaseController
{
    private readonly ApplicationDbContext _context;

    public WorkCentersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkCenters()
    {
        var workCenters = await _context.WorkCenters
            .Include(w => w.Machines)
            .Where(w => w.IsActive)
            .ToListAsync();
        return Ok(workCenters.Select(MasterDataMappings.MapWorkCenter).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkCenter(Guid id)
    {
        var workCenter = await _context.WorkCenters
            .Include(w => w.Machines)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (workCenter == null) return NotFound();
        return Ok(MasterDataMappings.MapWorkCenter(workCenter));
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkCenter([FromBody] WorkCenterRequest request)
    {
        var workCenter = new WorkCenter
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            StandardCostPerHour = request.StandardCostPerHour ?? 0m,
            Capacity = request.Capacity ?? 0m,
            IsActive = request.IsActive
        };

        _context.WorkCenters.Add(workCenter);
        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapWorkCenter(workCenter));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWorkCenter(Guid id, [FromBody] WorkCenterRequest request)
    {
        var workCenter = await _context.WorkCenters.FirstOrDefaultAsync(w => w.Id == id);
        if (workCenter == null) return NotFound();

        workCenter.Code = request.Code;
        workCenter.Name = request.Name;
        workCenter.Description = request.Description;
        workCenter.StandardCostPerHour = request.StandardCostPerHour ?? workCenter.StandardCostPerHour;
        workCenter.Capacity = request.Capacity ?? workCenter.Capacity;
        workCenter.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MasterDataMappings.MapWorkCenter(workCenter));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkCenter(Guid id)
    {
        var workCenter = await _context.WorkCenters.FirstOrDefaultAsync(w => w.Id == id);
        if (workCenter == null) return NotFound();

        workCenter.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
