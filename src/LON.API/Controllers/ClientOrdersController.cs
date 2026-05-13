using LON.Application.Customs.ClientOrders;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

/// <summary>
/// Phase 17 §E1 — ClientOrder CRUD endpoints. Hub UI (§E2) consumes these.
/// </summary>
public class ClientOrdersController : BaseController
{
    private readonly ApplicationDbContext _context;

    public ClientOrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? status,
        [FromQuery] Guid? customerPartnerId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool includeCancelled = false)
    {
        var result = await Mediator.Send(new GetClientOrdersQuery
        {
            Status = status,
            CustomerPartnerId = customerPartnerId,
            FromDate = fromDate,
            ToDate = toDate,
            IncludeCancelled = includeCancelled,
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetClientOrderByIdQuery(id));
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientOrderCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientOrderCommand command)
    {
        if (command.Id != Guid.Empty && command.Id != id)
            return BadRequest(new { errorMessage = "Route id and body id do not match." });
        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelClientOrderCommand command)
    {
        var effective = command with { Id = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E5 — add a <see cref="ClientOrderFinishedGood"/> row to an
    /// existing ClientOrder. Used by the hub's „Внеси готови производи (BOM)" action.
    /// </summary>
    [HttpPost("{id:guid}/finished-goods")]
    public async Task<IActionResult> AddFinishedGood(Guid id, [FromBody] AddClientOrderFinishedGoodCommand command)
    {
        var effective = command with { ClientOrderId = id };
        var result = await Mediator.Send(effective);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E8 — list of FGs declared on this ClientOrder, joined to the
    /// current InventoryBalance positive-qty rows at any OK / Quarantine
    /// location. Powers the EX-declaration wizard's FG picker so the user
    /// only sees what's actually shippable.
    ///
    /// One row per (FG item, batch, MRN, location) inventory bucket — the
    /// wizard collapses by item when computing default shipment qty.
    /// </summary>
    [HttpGet("{id:guid}/available-fgs")]
    public async Task<IActionResult> GetAvailableFinishedGoods(Guid id)
    {
        var fgItemIds = await _context.ClientOrderFinishedGoods
            .Where(g => g.ClientOrderId == id)
            .Select(g => g.ItemId)
            .Distinct()
            .ToListAsync();
        if (fgItemIds.Count == 0)
            return Ok(Array.Empty<object>());

        var balances = await _context.InventoryBalances
            .Include(b => b.Item)
            .Include(b => b.Location).ThenInclude(l => l.Warehouse)
            .Include(b => b.UoM)
            .Where(b => fgItemIds.Contains(b.ItemId)
                        && b.Quantity > 0m
                        && b.QualityStatus != LON.Domain.Enums.QualityStatus.Blocked)
            .ToListAsync();

        var rows = balances.Select(b => new
        {
            balanceId = b.Id,
            itemId = b.ItemId,
            itemCode = b.Item.Code,
            itemName = b.Item.Name,
            batchNumber = b.BatchNumber,
            mrn = b.MRN,
            quantity = b.Quantity,
            qualityStatus = (int)b.QualityStatus,
            uoMId = b.UoMId,
            uoMCode = b.UoM != null ? b.UoM.Code : null,
            locationId = b.LocationId,
            locationCode = b.Location != null ? b.Location.Code : null,
            warehouseCode = b.Location != null && b.Location.Warehouse != null ? b.Location.Warehouse.Code : null,
        }).ToList();

        return Ok(rows);
    }
}
