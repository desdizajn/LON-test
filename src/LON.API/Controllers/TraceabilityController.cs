using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class TraceabilityController : BaseController
{
    private readonly ApplicationDbContext _context;

    public TraceabilityController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("trace-forward")]
    public async Task<IActionResult> TraceForward([FromQuery] string? batchNumber = null, [FromQuery] string? mrn = null)
    {
        if (string.IsNullOrEmpty(batchNumber) && string.IsNullOrEmpty(mrn))
            return BadRequest("Either batchNumber or mrn must be provided");

        var query = _context.TraceLinks
            .Include(t => t.Item)
            .AsQueryable();

        if (!string.IsNullOrEmpty(batchNumber))
            query = query.Where(t => t.SourceBatchNumber == batchNumber);

        if (!string.IsNullOrEmpty(mrn))
            query = query.Where(t => t.SourceMRN == mrn);

        var links = await query.ToListAsync();
        return Ok(links);
    }

    [HttpGet("trace-backward")]
    public async Task<IActionResult> TraceBackward([FromQuery] string? batchNumber = null, [FromQuery] string? mrn = null)
    {
        if (string.IsNullOrEmpty(batchNumber) && string.IsNullOrEmpty(mrn))
            return BadRequest("Either batchNumber or mrn must be provided");

        var query = _context.TraceLinks
            .Include(t => t.Item)
            .AsQueryable();

        if (!string.IsNullOrEmpty(batchNumber))
            query = query.Where(t => t.TargetBatchNumber == batchNumber);

        if (!string.IsNullOrEmpty(mrn))
            query = query.Where(t => t.TargetMRN == mrn);

        var links = await query.ToListAsync();
        return Ok(links);
    }

    [HttpGet("genealogy/{batchNumber}")]
    public async Task<IActionResult> GetBatchGenealogy(string batchNumber)
    {
        var genealogy = await _context.BatchGenealogies
            .Include(g => g.Item)
            .FirstOrDefaultAsync(g => g.BatchNumber == batchNumber);

        if (genealogy == null)
            return NotFound();

        return Ok(genealogy);
    }

    [HttpGet("trace-full")]
    public async Task<IActionResult> TraceFullPath([FromQuery] string batchNumber)
    {
        var visited = new HashSet<string>();
        var result = new List<object>();

        await TraceRecursive(batchNumber, visited, result, true);

        return Ok(new
        {
            BatchNumber = batchNumber,
            TracePath = result
        });
    }

    private async Task TraceRecursive(string batchNumber, HashSet<string> visited, List<object> result, bool forward)
    {
        if (visited.Contains(batchNumber))
            return;

        visited.Add(batchNumber);

        var links = forward
            ? await _context.TraceLinks
                .Include(t => t.Item)
                .Where(t => t.SourceBatchNumber == batchNumber)
                .ToListAsync()
            : await _context.TraceLinks
                .Include(t => t.Item)
                .Where(t => t.TargetBatchNumber == batchNumber)
                .ToListAsync();

        foreach (var link in links)
        {
            result.Add(new
            {
                link.SourceType,
                link.SourceId,
                link.SourceBatchNumber,
                link.SourceMRN,
                link.TargetType,
                link.TargetId,
                link.TargetBatchNumber,
                link.TargetMRN,
                ItemCode = link.Item.Code,
                ItemName = link.Item.Name,
                link.Quantity
            });

            var nextBatch = forward ? link.TargetBatchNumber : link.SourceBatchNumber;
            if (!string.IsNullOrEmpty(nextBatch))
            {
                await TraceRecursive(nextBatch, visited, result, forward);
            }
        }
    }
}
