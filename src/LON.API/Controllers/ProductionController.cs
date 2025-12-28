using LON.Application.Production.Commands.CreateProductionOrder;
using LON.Domain.Enums;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class ProductionController : BaseController
{
    private readonly ApplicationDbContext _context;

    public ProductionController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateProductionOrder([FromBody] CreateProductionOrderCommand command)
    {
        var result = await Mediator.Send(command);
        if (result.IsSuccess)
            return Ok(result);
        return BadRequest(result);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetProductionOrders([FromQuery] ProductionOrderStatus? status = null)
    {
        var query = _context.ProductionOrders
            .Include(p => p.Item)
            .Include(p => p.UoM)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var orders = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return Ok(orders);
    }

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> GetProductionOrder(Guid id)
    {
        var order = await _context.ProductionOrders
            .Include(p => p.Item)
            .Include(p => p.UoM)
            .Include(p => p.BOM)
            .ThenInclude(b => b!.Lines)
            .Include(p => p.Materials)
            .ThenInclude(m => m.Item)
            .Include(p => p.Operations)
            .ThenInclude(o => o.WorkCenter)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (order == null)
            return NotFound();

        return Ok(order);
    }

    [HttpGet("orders/{id}/material-issues")]
    public async Task<IActionResult> GetMaterialIssues(Guid id)
    {
        var issues = await _context.MaterialIssues
            .Include(m => m.Item)
            .Include(m => m.UoM)
            .Include(m => m.IssuedByEmployee)
            .Where(m => m.ProductionOrderId == id)
            .ToListAsync();

        return Ok(issues);
    }

    [HttpGet("orders/{id}/receipts")]
    public async Task<IActionResult> GetProductionReceipts(Guid id)
    {
        var receipts = await _context.ProductionReceipts
            .Include(r => r.Item)
            .Include(r => r.UoM)
            .Include(r => r.Location)
            .Include(r => r.ReceivedByEmployee)
            .Where(r => r.ProductionOrderId == id)
            .ToListAsync();

        return Ok(receipts);
    }

    [HttpGet("boms")]
    public async Task<IActionResult> GetBOMs([FromQuery] Guid? itemId = null)
    {
        var query = _context.BOMs
            .Include(b => b.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(b => b.ItemId == itemId.Value);

        var boms = await query.ToListAsync();
        return Ok(boms);
    }
}
