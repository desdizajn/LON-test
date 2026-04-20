using LON.API.MasterData;
using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers.MasterData;

[Route("api/MasterData/machines")]
public class MachinesController : BaseController
{
    private readonly ApplicationDbContext _context;

    public MachinesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMachines([FromQuery] Guid? workCenterId = null)
    {
        var query = _context.Machines
            .Include(m => m.WorkCenter)
            .Where(m => m.IsActive)
            .AsQueryable();
        if (workCenterId.HasValue) query = query.Where(m => m.WorkCenterId == workCenterId.Value);

        var machines = await query.ToListAsync();
        return Ok(machines.Select(MasterDataMappings.MapMachine).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMachine(Guid id)
    {
        var machine = await _context.Machines
            .Include(m => m.WorkCenter)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (machine == null) return NotFound();
        return Ok(MasterDataMappings.MapMachine(machine));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMachine([FromBody] MachineRequest request)
    {
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            WorkCenterId = request.WorkCenterId,
            SerialNumber = request.SerialNumber,
            IsActive = request.IsActive
        };

        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();

        machine = await _context.Machines.Include(m => m.WorkCenter).FirstAsync(m => m.Id == machine.Id);
        return Ok(MasterDataMappings.MapMachine(machine));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMachine(Guid id, [FromBody] MachineRequest request)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == id);
        if (machine == null) return NotFound();

        machine.Code = request.Code;
        machine.Name = request.Name;
        machine.WorkCenterId = request.WorkCenterId;
        machine.SerialNumber = request.SerialNumber;
        machine.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        machine = await _context.Machines.Include(m => m.WorkCenter).FirstAsync(m => m.Id == id);
        return Ok(MasterDataMappings.MapMachine(machine));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMachine(Guid id)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == id);
        if (machine == null) return NotFound();

        machine.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
