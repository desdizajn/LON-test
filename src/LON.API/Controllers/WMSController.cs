using LON.Application.WMS.Commands.CreateReceipt;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class WMSController : BaseController
{
    private readonly ApplicationDbContext _context;

    public WMSController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("receipts")]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateReceiptCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var receipts = await _context.Receipts
            .Include(r => r.Lines)
            .OrderByDescending(r => r.ReceiptDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(receipts);
    }

    [HttpGet("receipts/{id}")]
    public async Task<IActionResult> GetReceipt(Guid id)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Lines)
            .ThenInclude(l => l.Item)
            .Include(r => r.Lines)
            .ThenInclude(l => l.UoM)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receipt == null)
            return NotFound();

        return Ok(receipt);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory([FromQuery] Guid? itemId = null, [FromQuery] Guid? locationId = null)
    {
        var query = _context.InventoryBalances
            .Include(i => i.Item)
            .Include(i => i.Location)
            .Include(i => i.UoM)
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(i => i.ItemId == itemId.Value);

        if (locationId.HasValue)
            query = query.Where(i => i.LocationId == locationId.Value);

        var inventory = await query.ToListAsync();
        return Ok(inventory);
    }

    [HttpGet("shipments")]
    public async Task<IActionResult> GetShipments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var shipments = await _context.Shipments
            .Include(s => s.Lines)
            .OrderByDescending(s => s.ShipmentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(shipments);
    }

    [HttpGet("pick-tasks")]
    public async Task<IActionResult> GetPickTasks([FromQuery] PickTaskStatus? status = null)
    {
        var query = _context.PickTasks
            .Include(p => p.Item)
            .Include(p => p.Location)
            .Include(p => p.AssignedToEmployee)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var tasks = await query.OrderBy(p => p.CreatedAt).ToListAsync();
        return Ok(tasks);
    }
}
