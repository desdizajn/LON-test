using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/routings")]
public class RoutingsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public RoutingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoutings([FromQuery] Guid? itemId = null)
    {
        var query = _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .AsQueryable();
        if (itemId.HasValue) query = query.Where(r => r.ItemId == itemId.Value);

        var routings = await query.ToListAsync();
        return Ok(routings.Select(MasterDataMappings.MapRouting).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRouting(Guid id)
    {
        var routing = await _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (routing == null) return NotFound();
        return Ok(MasterDataMappings.MapRouting(routing));
    }

    [HttpGet("item/{itemId}")]
    public async Task<IActionResult> GetRoutingsByItem(Guid itemId)
    {
        var routings = await _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .Where(r => r.ItemId == itemId)
            .ToListAsync();
        return Ok(routings.Select(MasterDataMappings.MapRouting).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> CreateRouting([FromBody] RoutingRequest request)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId);
        if (item == null) return BadRequest(new { message = "Invalid item." });

        var version = MasterDataMappings.ParseVersion(request.Version);
        var routing = new Routing
        {
            Id = Guid.NewGuid(),
            Code = $"{item.Code}-RT-{version}",
            ItemId = request.ItemId,
            Version = version,
            IsActive = request.IsActive
        };

        routing.Operations = request.Operations.Select(op => new RoutingOperation
        {
            Id = Guid.NewGuid(),
            RoutingId = routing.Id,
            SequenceNumber = op.OperationNumber,
            OperationCode = op.OperationName,
            Description = op.Description ?? op.OperationName,
            WorkCenterId = op.WorkCenterId,
            StandardTimeMinutes = op.StandardTime,
            SetupTimeMinutes = op.SetupTime
        }).ToList();

        _context.Routings.Add(routing);
        await _context.SaveChangesAsync();

        routing = await _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .FirstAsync(r => r.Id == routing.Id);
        return Ok(MasterDataMappings.MapRouting(routing));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRouting(Guid id, [FromBody] RoutingRequest request)
    {
        var routing = await _context.Routings
            .Include(r => r.Operations)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (routing == null) return NotFound();

        routing.ItemId = request.ItemId;
        routing.Version = MasterDataMappings.ParseVersion(request.Version);
        routing.IsActive = request.IsActive;

        _context.RoutingOperations.RemoveRange(routing.Operations);
        routing.Operations = request.Operations.Select(op => new RoutingOperation
        {
            Id = Guid.NewGuid(),
            RoutingId = routing.Id,
            SequenceNumber = op.OperationNumber,
            OperationCode = op.OperationName,
            Description = op.Description ?? op.OperationName,
            WorkCenterId = op.WorkCenterId,
            StandardTimeMinutes = op.StandardTime,
            SetupTimeMinutes = op.SetupTime
        }).ToList();

        await _context.SaveChangesAsync();

        routing = await _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .FirstAsync(r => r.Id == id);
        return Ok(MasterDataMappings.MapRouting(routing));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRouting(Guid id)
    {
        var routing = await _context.Routings.FirstOrDefaultAsync(r => r.Id == id);
        if (routing == null) return NotFound();

        routing.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
