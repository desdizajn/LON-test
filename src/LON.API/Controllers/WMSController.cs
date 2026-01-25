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

    [HttpPost("shipments")]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentRequest request)
    {
        var shipment = new LON.Domain.Entities.WMS.Shipment
        {
            Id = Guid.NewGuid(),
            ShipmentNumber = GenerateShipmentNumber(),
            ShipmentDate = DateTime.UtcNow,
            CustomerId = request.CustomerId,
            CarrierId = request.CarrierId,
            Status = LON.Domain.Enums.ShipmentStatus.Draft,
            TrackingNumber = request.TrackingNumber,
            SalesOrderNumber = request.SalesOrderNumber,
            CreatedAt = DateTime.UtcNow
        };

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync();

        return Ok(shipment);
    }

    [HttpPost("transfers")]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateTransferRequest request)
    {
        var transfer = new LON.Domain.Entities.WMS.Transfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = GenerateTransferNumber(),
            TransferDate = DateTime.UtcNow,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.Transfers.Add(transfer);
        await _context.SaveChangesAsync();

        return Ok(transfer);
    }

    [HttpGet("transfers")]
    public async Task<IActionResult> GetTransfers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var transfers = await _context.Transfers
            .Include(t => t.FromLocation)
            .Include(t => t.ToLocation)
            .Include(t => t.Lines)
            .OrderByDescending(t => t.TransferDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(transfers);
    }

    [HttpGet("cycle-counts")]
    public async Task<IActionResult> GetCycleCounts([FromQuery] LON.Domain.Enums.CycleCountStatus? status = null)
    {
        var query = _context.CycleCounts
            .Include(c => c.Warehouse)
            .Include(c => c.Lines)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var counts = await query.OrderByDescending(c => c.ScheduledDate).ToListAsync();
        return Ok(counts);
    }

    [HttpPost("cycle-counts")]
    public async Task<IActionResult> CreateCycleCount([FromBody] CreateCycleCountRequest request)
    {
        var cycleCount = new LON.Domain.Entities.WMS.CycleCount
        {
            Id = Guid.NewGuid(),
            CountNumber = GenerateCycleCountNumber(),
            ScheduledDate = request.ScheduledDate,
            WarehouseId = request.WarehouseId,
            Status = LON.Domain.Enums.CycleCountStatus.Planned,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.CycleCounts.Add(cycleCount);
        await _context.SaveChangesAsync();

        return Ok(cycleCount);
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

    private string GenerateShipmentNumber()
    {
        var date = DateTime.UtcNow;
        var count = _context.Shipments.Count(r => r.CreatedAt.Date == date.Date) + 1;
        return $"SHP-{date:yyyyMMdd}-{count:D4}";
    }

    private string GenerateTransferNumber()
    {
        var date = DateTime.UtcNow;
        var count = _context.Transfers.Count(r => r.CreatedAt.Date == date.Date) + 1;
        return $"TRF-{date:yyyyMMdd}-{count:D4}";
    }

    private string GenerateCycleCountNumber()
    {
        var date = DateTime.UtcNow;
        var count = _context.CycleCounts.Count(r => r.CreatedAt.Date == date.Date) + 1;
        return $"CC-{date:yyyyMMdd}-{count:D4}";
    }
}

// Request DTOs
public class CreateShipmentRequest
{
    public Guid? CustomerId { get; set; }
    public Guid? CarrierId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? SalesOrderNumber { get; set; }
}

public class CreateTransferRequest
{
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public string? Notes { get; set; }
}

public class CreateCycleCountRequest
{
    public DateTime ScheduledDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
}
