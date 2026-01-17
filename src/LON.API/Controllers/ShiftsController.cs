using LON.Domain.Entities.MasterData;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class ShiftsController : BaseController
{
    private readonly ApplicationDbContext _context;

    public ShiftsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetShifts()
    {
        var shifts = await _context.Shifts.ToListAsync();
        return Ok(shifts.Select(MapShift).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetShift(Guid id)
    {
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null)
        {
            return NotFound();
        }

        return Ok(MapShift(shift));
    }

    [HttpPost]
    public async Task<IActionResult> CreateShift([FromBody] CreateShiftRequest request)
    {
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            Code = GenerateCode(request.Name),
            Name = request.Name,
            StartTime = TimeSpan.Parse(request.StartTime),
            EndTime = TimeSpan.Parse(request.EndTime),
            Description = request.Description,
            IsActive = true
        };

        await _context.Shifts.AddAsync(shift);
        await _context.SaveChangesAsync();

        return Ok(MapShift(shift));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateShift(Guid id, [FromBody] UpdateShiftRequest request)
    {
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null)
        {
            return NotFound();
        }

        shift.Name = request.Name;
        shift.StartTime = TimeSpan.Parse(request.StartTime);
        shift.EndTime = TimeSpan.Parse(request.EndTime);
        shift.Description = request.Description;
        shift.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(MapShift(shift));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteShift(Guid id)
    {
        var shift = await _context.Shifts.FirstOrDefaultAsync(s => s.Id == id);
        if (shift == null)
        {
            return NotFound();
        }

        shift.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static ShiftDto MapShift(Shift shift)
    {
        return new ShiftDto(
            shift.Id,
            shift.Name,
            shift.StartTime.ToString(@"hh\:mm"),
            shift.EndTime.ToString(@"hh\:mm"),
            shift.Description,
            shift.IsActive
        );
    }

    private static string GenerateCode(string name)
    {
        var code = new string(name
            .Trim()
            .ToUpperInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(code) ? "SHIFT" : code;
    }
}

public record ShiftDto(
    Guid Id,
    string Name,
    string StartTime,
    string EndTime,
    string? Description,
    bool IsActive
);

public record CreateShiftRequest(
    string Name,
    string StartTime,
    string EndTime,
    string? Description
);

public record UpdateShiftRequest(
    string Name,
    string StartTime,
    string EndTime,
    string? Description,
    bool IsActive
);
