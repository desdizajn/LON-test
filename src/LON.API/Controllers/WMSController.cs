using LON.Application.WMS.Commands.BulkReceiptFromDeclaration;
using LON.Application.WMS.Commands.BulkShipmentFromFG;
using LON.Application.WMS.Commands.CreateReceipt;
using LON.Application.WMS.Commands.MassLocationTransfer;
using LON.Application.WMS.Commands.MoveBatchAcrossStages;
using LON.Application.WMS.Commands.Podelba;
using LON.Application.WMS.Commands.PodelbaToProducer;
using LON.Application.WMS.Commands.ReportSkart;
using LON.Application.WMS.Queries.GenerateIspratnica;
using LON.Application.WMS.Queries.GetSkart;
using LON.Application.WMS.Queries.MassLocationTransferPreview;
using LON.Application.WMS.Queries.MozniMinusi;
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

    /// <summary>
    /// P5.2.3 — bulk receipt from an existing customs declaration. Explodes
    /// every CustomsDeclarationLine into a ReceiptLine and stamps the shared
    /// MRN/PartnerId on every line so the standard Receipt pipeline handles
    /// MRN consumption + inflate-for-waste + LON state.
    /// </summary>
    [HttpPost("receipts/bulk-from-declaration")]
    public async Task<IActionResult> BulkReceiptFromDeclaration(
        [FromBody] BulkReceiptFromDeclarationCommand command)
    {
        var result = await Mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? clientOrderId = null)
    {
        var query = _context.Receipts
            .Include(r => r.Lines)
            .AsQueryable();

        // Phase 17 §E4 — hub Receipts tab filters via the ClientOrder linkage
        // on the receipt's lines' CustomsDeclaration. A receipt belongs to the
        // ClientOrder if at least one of its lines references a declaration
        // tied to that order. EF translates this to a single SQL IN-clause.
        if (clientOrderId.HasValue && clientOrderId.Value != Guid.Empty)
        {
            var declIds = _context.CustomsDeclarations
                .Where(d => d.ClientOrderId == clientOrderId.Value)
                .Select(d => d.Id);
            query = query.Where(r =>
                r.Lines.Any(l => l.CustomsDeclarationId.HasValue && declIds.Contains(l.CustomsDeclarationId.Value)));
        }

        var receipts = await query
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
    public async Task<IActionResult> GetInventory(
        [FromQuery] Guid? itemId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? clientOrderId = null,
        [FromQuery] bool? unassignedOnly = null,
        [FromQuery] Guid? assignedProducerId = null)
    {
        var query = _context.InventoryBalances
            .Include(i => i.Item)
            .Include(i => i.Location)
            .Include(i => i.UoM)
            .Include(i => i.AssignedProducer)
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(i => i.ItemId == itemId.Value);

        if (locationId.HasValue)
            query = query.Where(i => i.LocationId == locationId.Value);

        if (warehouseId.HasValue)
            query = query.Where(i => i.Location.WarehouseId == warehouseId.Value);

        if (unassignedOnly == true)
            query = query.Where(i => i.AssignedProducerId == null);

        if (assignedProducerId.HasValue)
            query = query.Where(i => i.AssignedProducerId == assignedProducerId.Value);

        // Phase 17 §E6 — Podelba dialog scopes its picker to materials referenced
        // by any ProductionOrderMaterial on a ProductionOrder linked to this
        // ClientOrder. Single SQL IN-clause; falls open (returns 0 rows) when
        // no POs/materials exist yet, so the UI can warn the user.
        if (clientOrderId.HasValue && clientOrderId.Value != Guid.Empty)
        {
            var poIds = _context.ProductionOrders
                .Where(p => p.ClientOrderId == clientOrderId.Value)
                .Select(p => p.Id);
            var itemIds = _context.ProductionOrderMaterials
                .Where(m => poIds.Contains(m.ProductionOrderId))
                .Select(m => m.ItemId)
                .Distinct();
            query = query.Where(i => itemIds.Contains(i.ItemId));
        }

        var inventory = await query.ToListAsync();
        return Ok(inventory);
    }

    /// <summary>
    /// P4.3 — MozniMinusi (negative-stock reconciliation report).
    /// Returns (Item, Batch, MRN) groups with net-negative net movement totals
    /// plus any InventoryBalance rows with Quantity &lt; 0.
    /// </summary>
    [HttpGet("inventory/mozni-minusi")]
    public async Task<IActionResult> GetMozniMinusi()
    {
        var result = await Mediator.Send(new MozniMinusiQuery());
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P5.2.2 — one-click move of every InventoryBalance carrying the given
    /// batch number to a target stage (LocationType). See command docs.
    /// </summary>
    [HttpPost("inventory/move-batch")]
    public async Task<IActionResult> MoveBatchAcrossStages([FromBody] MoveBatchAcrossStagesCommand command)
    {
        var result = await Mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P5.2.7 — preview which InventoryBalance rows would be touched by a
    /// MassLocationTransfer. The UI invokes this before Commit so the user
    /// sees a count + total qty + per-row list before confirming.
    /// </summary>
    [HttpPost("inventory/mass-transfer/preview")]
    public async Task<IActionResult> MassTransferPreview([FromBody] MassLocationTransferPreviewQuery query)
    {
        var result = await Mediator.Send(query);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P5.2.7 — filter inventory by arbitrary predicate (item/batch/MRN/
    /// source-warehouse/source-location/quality/lon-state) and transfer
    /// every match to a single target location in one atomic call.
    /// </summary>
    [HttpPost("inventory/mass-transfer")]
    public async Task<IActionResult> MassTransfer([FromBody] MassLocationTransferCommand command)
    {
        var result = await Mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// P14.7 — bulk move N selected InventoryBalance rows to one target
    /// location atomically. Selection-based companion to mass-transfer.
    /// </summary>
    [HttpPost("inventory/bulk-move-balances")]
    public async Task<IActionResult> BulkMoveBalances(
        [FromBody] LON.Application.WMS.Commands.BulkMoveBalances.BulkMoveBalancesCommand command)
    {
        var result = await Mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
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

    /// <summary>
    /// P5.2.4 — one-click shipment from an FG selection predicate. Creates a
    /// Shipment + per-balance lines, drains stock, optionally chains an EX
    /// declaration when the selection collapses to a single source MRN.
    /// </summary>
    [HttpPost("shipments/bulk-from-fg")]
    public async Task<IActionResult> BulkShipmentFromFG([FromBody] BulkShipmentFromFGCommand command)
    {
        var result = await Mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(result);
        return Ok(result);
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

    // ---------- P15.3 Skart (defective-on-intake) ----------

    /// <summary>
    /// P15.3 — report defective-on-intake qty against a ReceiptLine. Splits
    /// the OK balance into OK + Blocked siblings at the same location and
    /// records a Skart audit row.
    /// </summary>
    [HttpPost("skart")]
    public async Task<IActionResult> ReportSkart([FromBody] ReportSkartCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// P15.3 — list Skart reports, newest first. Optional <c>openOnly</c>,
    /// <c>itemId</c>, <c>mrn</c> filters for the register page.
    /// </summary>
    [HttpGet("skart")]
    public async Task<IActionResult> GetSkart(
        [FromQuery] bool openOnly = false,
        [FromQuery] Guid? itemId = null,
        [FromQuery] string? mrn = null)
    {
        var rows = await Mediator.Send(new GetSkartQuery(openOnly, itemId, mrn));
        return Ok(rows);
    }

    /// <summary>
    /// P15.3 — close out a Skart with the settled resolution
    /// (ReturnedToSupplier / Destroyed / AcceptedAtDiscount). Does not
    /// move inventory; subsequent shipment/adjustment handles the physical side.
    /// </summary>
    [HttpPost("skart/{id}/resolve")]
    public async Task<IActionResult> ResolveSkart(Guid id, [FromBody] ResolveSkartRequest request)
    {
        var result = await Mediator.Send(new ResolveSkartCommand
        {
            SkartId = id,
            Resolution = request.Resolution,
            ResolutionNote = request.ResolutionNote
        });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ---------- P15.8 Podelba (sub-contract producer distribution) ----------

    /// <summary>
    /// P15.8 — Distribute one tenant-held InventoryBalance across N producers.
    /// Atomically drains the source to zero and creates/increments per-producer
    /// siblings with AssignedProducerId set. Σ allocations must equal source qty.
    /// </summary>
    [HttpPost("podelba")]
    public async Task<IActionResult> Podelba([FromBody] PodelbaCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Phase 17 §E6 — assign N balances to ONE producer in one atomic call.
    /// Dual of <see cref="Podelba"/>: that flow splits one balance across many
    /// producers (full distribution required); this routes many balances to one
    /// producer with partial quantities allowed. Sources keep their remainder.
    /// </summary>
    [HttpPost("inventory/podelba-to-producer")]
    public async Task<IActionResult> PodelbaToProducer([FromBody] PodelbaToProducerCommand command)
    {
        var result = await Mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// P15.9.1 — render Ispratnica (shipment document). Returns JSON with
    /// structured fields + a ready-to-print HTML payload. Frontend can drop
    /// the HTML into an iframe and call window.print(), or render from the
    /// structured fields directly.
    /// </summary>
    [HttpGet("shipments/{id:guid}/ispratnica")]
    public async Task<IActionResult> GenerateIspratnica(Guid id)
    {
        var result = await Mediator.Send(new GenerateIspratnicaQuery(id));
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// P15.9.1 — same content as JSON endpoint but returns just the HTML
    /// as `text/html` so a browser can open it directly (right-click
    /// "Open in new tab" then Ctrl+P).
    /// </summary>
    [HttpGet("shipments/{id:guid}/ispratnica.html")]
    public async Task<IActionResult> GenerateIspratnicaHtml(Guid id)
    {
        var result = await Mediator.Send(new GenerateIspratnicaQuery(id));
        if (!result.IsSuccess) return BadRequest(result);
        return Content(result.Data!.Html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// P15.8 — aggregated view "how much of what is sitting at which producer".
    /// </summary>
    [HttpGet("inventory-by-producer")]
    public async Task<IActionResult> InventoryByProducer([FromQuery] Guid? producerId = null)
    {
        var q = _context.InventoryBalances
            .Include(b => b.Item)
            .Include(b => b.AssignedProducer)
            .Where(b => !b.IsDeleted && b.Quantity > 0 && b.AssignedProducerId != null);
        if (producerId.HasValue) q = q.Where(b => b.AssignedProducerId == producerId.Value);
        var rows = await q
            .Select(b => new
            {
                b.Id,
                ItemCode = b.Item.Code,
                ItemName = b.Item.Name,
                b.BatchNumber,
                b.MRN,
                b.Quantity,
                b.QualityStatus,
                b.LonProcessState,
                ProducerId = b.AssignedProducerId,
                ProducerCode = b.AssignedProducer != null ? b.AssignedProducer.Code : null,
                ProducerName = b.AssignedProducer != null ? b.AssignedProducer.Name : null
            })
            .OrderBy(b => b.ProducerCode).ThenBy(b => b.ItemCode)
            .ToListAsync();
        return Ok(rows);
    }
}

/// <summary>P15.3 — body of POST /skart/{id}/resolve.</summary>
public class ResolveSkartRequest
{
    public SkartResolution Resolution { get; set; }
    public string? ResolutionNote { get; set; }
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
