using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;
using LON.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LON.API.Controllers;

public class MasterDataController : BaseController
{
    private readonly ApplicationDbContext _context;

    public MasterDataController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] string? search = null)
    {
        var query = _context.Items
            .Include(i => i.BaseUoM)
            .Where(i => !i.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(i => i.Code.Contains(search) || i.Name.Contains(search));

        var items = await query.ToListAsync();
        return Ok(items.Select(MapItem).ToList());
    }

    [HttpGet("items/{id}")]
    public async Task<IActionResult> GetItem(Guid id)
    {
        var item = await _context.Items
            .Include(i => i.BaseUoM)
            .Include(i => i.UoMConversions)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound();

        return Ok(MapItem(item));
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] ItemRequest request)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Type = request.ItemType,
            IsBatchTracked = request.IsBatchRequired,
            IsMRNTracked = request.IsMRNRequired,
            HSCode = request.HSCode,
            CountryOfOrigin = request.CountryOfOrigin,
            BaseUoMId = request.UoMId,
            StandardCost = request.StandardCost ?? 0m,
            IsDeleted = !request.IsActive
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        item = await _context.Items.Include(i => i.BaseUoM).FirstAsync(i => i.Id == item.Id);
        return Ok(MapItem(item));
    }

    [HttpPut("items/{id}")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] ItemRequest request)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id);
        if (item == null)
            return NotFound();

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description ?? string.Empty;
        item.Type = request.ItemType;
        item.IsBatchTracked = request.IsBatchRequired;
        item.IsMRNTracked = request.IsMRNRequired;
        item.HSCode = request.HSCode;
        item.CountryOfOrigin = request.CountryOfOrigin;
        item.BaseUoMId = request.UoMId;
        item.StandardCost = request.StandardCost ?? item.StandardCost;
        item.IsDeleted = !request.IsActive;

        await _context.SaveChangesAsync();

        item = await _context.Items.Include(i => i.BaseUoM).FirstAsync(i => i.Id == item.Id);
        return Ok(MapItem(item));
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id);
        if (item == null)
            return NotFound();

        item.IsDeleted = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> GetWarehouses()
    {
        var warehouses = await _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => w.IsActive)
            .ToListAsync();

        return Ok(warehouses.Select(MapWarehouse).ToList());
    }

    [HttpGet("warehouses/{id}")]
    public async Task<IActionResult> GetWarehouse(Guid id)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.Locations)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (warehouse == null)
            return NotFound();

        return Ok(MapWarehouse(warehouse));
    }

    [HttpPost("warehouses")]
    public async Task<IActionResult> CreateWarehouse([FromBody] WarehouseRequest request)
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Address = request.Address ?? string.Empty,
            IsActive = request.IsActive
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync();

        return Ok(MapWarehouse(warehouse));
    }

    [HttpPut("warehouses/{id}")]
    public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] WarehouseRequest request)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id);
        if (warehouse == null)
            return NotFound();

        warehouse.Code = request.Code;
        warehouse.Name = request.Name;
        warehouse.Address = request.Address ?? string.Empty;
        warehouse.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MapWarehouse(warehouse));
    }

    [HttpDelete("warehouses/{id}")]
    public async Task<IActionResult> DeleteWarehouse(Guid id)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id);
        if (warehouse == null)
            return NotFound();

        warehouse.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("locations")]
    public async Task<IActionResult> GetLocations([FromQuery] Guid? warehouseId = null)
    {
        var query = _context.Locations
            .Include(l => l.Warehouse)
            .Where(l => l.IsActive)
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(l => l.WarehouseId == warehouseId.Value);

        var locations = await query.ToListAsync();
        return Ok(locations.Select(MapLocation).ToList());
    }

    [HttpGet("locations/{id}")]
    public async Task<IActionResult> GetLocation(Guid id)
    {
        var location = await _context.Locations
            .Include(l => l.Warehouse)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (location == null)
            return NotFound();

        return Ok(MapLocation(location));
    }

    [HttpPost("locations")]
    public async Task<IActionResult> CreateLocation([FromBody] LocationRequest request)
    {
        var location = new Location
        {
            Id = Guid.NewGuid(),
            WarehouseId = request.WarehouseId,
            Code = request.Code,
            Name = request.Name,
            Type = request.LocationType,
            Aisle = request.Aisle,
            Rack = request.Rack,
            Shelf = request.Shelf,
            Bin = request.Bin,
            MaxCapacity = request.MaxCapacity,
            IsActive = request.IsActive
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();

        location = await _context.Locations.Include(l => l.Warehouse).FirstAsync(l => l.Id == location.Id);
        return Ok(MapLocation(location));
    }

    [HttpPut("locations/{id}")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] LocationRequest request)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location == null)
            return NotFound();

        location.WarehouseId = request.WarehouseId;
        location.Code = request.Code;
        location.Name = request.Name;
        location.Type = request.LocationType;
        location.Aisle = request.Aisle;
        location.Rack = request.Rack;
        location.Shelf = request.Shelf;
        location.Bin = request.Bin;
        location.MaxCapacity = request.MaxCapacity;
        location.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        location = await _context.Locations.Include(l => l.Warehouse).FirstAsync(l => l.Id == id);
        return Ok(MapLocation(location));
    }

    [HttpDelete("locations/{id}")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location == null)
            return NotFound();

        location.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("partners")]
    public async Task<IActionResult> GetPartners([FromQuery] LON.Domain.Enums.PartnerType? type = null)
    {
        var query = _context.Partners
            .Where(p => p.IsActive)
            .AsQueryable();

        if (type.HasValue)
            query = query.Where(p => p.Type == type.Value);

        var partners = await query.ToListAsync();
        return Ok(partners.Select(MapPartner).ToList());
    }

    [HttpGet("partners/{id}")]
    public async Task<IActionResult> GetPartner(Guid id)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null)
            return NotFound();

        return Ok(MapPartner(partner));
    }

    [HttpPost("partners")]
    public async Task<IActionResult> CreatePartner([FromBody] PartnerRequest request)
    {
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Type = request.PartnerType,
            TaxNumber = request.TaxNumber,
            Address = request.Address,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            Country = request.Country,
            IsActive = request.IsActive
        };

        _context.Partners.Add(partner);
        await _context.SaveChangesAsync();

        return Ok(MapPartner(partner));
    }

    [HttpPut("partners/{id}")]
    public async Task<IActionResult> UpdatePartner(Guid id, [FromBody] PartnerRequest request)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null)
            return NotFound();

        partner.Code = request.Code;
        partner.Name = request.Name;
        partner.Type = request.PartnerType;
        partner.TaxNumber = request.TaxNumber;
        partner.Address = request.Address;
        partner.ContactPerson = request.ContactPerson;
        partner.Email = request.Email;
        partner.Phone = request.Phone;
        partner.Country = request.Country;
        partner.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MapPartner(partner));
    }

    [HttpDelete("partners/{id}")]
    public async Task<IActionResult> DeletePartner(Guid id)
    {
        var partner = await _context.Partners.FirstOrDefaultAsync(p => p.Id == id);
        if (partner == null)
            return NotFound();

        partner.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _context.Employees
            .Where(e => e.IsActive)
            .ToListAsync();

        return Ok(employees);
    }

    [HttpGet("work-centers")]
    [HttpGet("workcenters")]
    public async Task<IActionResult> GetWorkCenters()
    {
        var workCenters = await _context.WorkCenters
            .Include(w => w.Machines)
            .Where(w => w.IsActive)
            .ToListAsync();

        return Ok(workCenters.Select(MapWorkCenter).ToList());
    }

    [HttpGet("workcenters/{id}")]
    public async Task<IActionResult> GetWorkCenter(Guid id)
    {
        var workCenter = await _context.WorkCenters
            .Include(w => w.Machines)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (workCenter == null)
            return NotFound();

        return Ok(MapWorkCenter(workCenter));
    }

    [HttpPost("workcenters")]
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

        return Ok(MapWorkCenter(workCenter));
    }

    [HttpPut("workcenters/{id}")]
    public async Task<IActionResult> UpdateWorkCenter(Guid id, [FromBody] WorkCenterRequest request)
    {
        var workCenter = await _context.WorkCenters.FirstOrDefaultAsync(w => w.Id == id);
        if (workCenter == null)
            return NotFound();

        workCenter.Code = request.Code;
        workCenter.Name = request.Name;
        workCenter.Description = request.Description;
        workCenter.StandardCostPerHour = request.StandardCostPerHour ?? workCenter.StandardCostPerHour;
        workCenter.Capacity = request.Capacity ?? workCenter.Capacity;
        workCenter.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MapWorkCenter(workCenter));
    }

    [HttpDelete("workcenters/{id}")]
    public async Task<IActionResult> DeleteWorkCenter(Guid id)
    {
        var workCenter = await _context.WorkCenters.FirstOrDefaultAsync(w => w.Id == id);
        if (workCenter == null)
            return NotFound();

        workCenter.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("machines")]
    public async Task<IActionResult> GetMachines([FromQuery] Guid? workCenterId = null)
    {
        var query = _context.Machines
            .Include(m => m.WorkCenter)
            .Where(m => m.IsActive)
            .AsQueryable();

        if (workCenterId.HasValue)
            query = query.Where(m => m.WorkCenterId == workCenterId.Value);

        var machines = await query.ToListAsync();
        return Ok(machines.Select(MapMachine).ToList());
    }

    [HttpGet("machines/{id}")]
    public async Task<IActionResult> GetMachine(Guid id)
    {
        var machine = await _context.Machines
            .Include(m => m.WorkCenter)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (machine == null)
            return NotFound();

        return Ok(MapMachine(machine));
    }

    [HttpPost("machines")]
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
        return Ok(MapMachine(machine));
    }

    [HttpPut("machines/{id}")]
    public async Task<IActionResult> UpdateMachine(Guid id, [FromBody] MachineRequest request)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == id);
        if (machine == null)
            return NotFound();

        machine.Code = request.Code;
        machine.Name = request.Name;
        machine.WorkCenterId = request.WorkCenterId;
        machine.SerialNumber = request.SerialNumber;
        machine.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        machine = await _context.Machines.Include(m => m.WorkCenter).FirstAsync(m => m.Id == id);
        return Ok(MapMachine(machine));
    }

    [HttpDelete("machines/{id}")]
    public async Task<IActionResult> DeleteMachine(Guid id)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == id);
        if (machine == null)
            return NotFound();

        machine.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("uom")]
    public async Task<IActionResult> GetUnitsOfMeasure()
    {
        var uoms = await _context.UnitsOfMeasure
            .Where(u => !u.IsDeleted)
            .ToListAsync();
        return Ok(uoms.Select(MapUoM).ToList());
    }

    [HttpGet("uom/{id}")]
    public async Task<IActionResult> GetUnitOfMeasure(Guid id)
    {
        var uom = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id);
        if (uom == null)
            return NotFound();

        return Ok(MapUoM(uom));
    }

    [HttpPost("uom")]
    public async Task<IActionResult> CreateUnitOfMeasure([FromBody] UoMRequest request)
    {
        var uom = new UnitOfMeasure
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Symbol = request.Description,
            IsDeleted = !request.IsActive
        };

        _context.UnitsOfMeasure.Add(uom);
        await _context.SaveChangesAsync();

        return Ok(MapUoM(uom));
    }

    [HttpPut("uom/{id}")]
    public async Task<IActionResult> UpdateUnitOfMeasure(Guid id, [FromBody] UoMRequest request)
    {
        var uom = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id);
        if (uom == null)
            return NotFound();

        uom.Code = request.Code;
        uom.Name = request.Name;
        uom.Symbol = request.Description;
        uom.IsDeleted = !request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(MapUoM(uom));
    }

    [HttpDelete("uom/{id}")]
    public async Task<IActionResult> DeleteUnitOfMeasure(Guid id)
    {
        var uom = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Id == id);
        if (uom == null)
            return NotFound();

        uom.IsDeleted = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("boms")]
    public async Task<IActionResult> GetBOMs([FromQuery] Guid? itemId = null)
    {
        var query = _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(b => b.ItemId == itemId.Value);

        var boms = await query.ToListAsync();
        return Ok(boms.Select(MapBom).ToList());
    }

    [HttpGet("boms/{id}")]
    public async Task<IActionResult> GetBOM(Guid id)
    {
        var bom = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bom == null)
            return NotFound();

        return Ok(MapBom(bom));
    }

    [HttpGet("boms/item/{itemId}")]
    public async Task<IActionResult> GetBOMsByItem(Guid itemId)
    {
        var boms = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .Where(b => b.ItemId == itemId)
            .ToListAsync();

        return Ok(boms.Select(MapBom).ToList());
    }

    [HttpPost("boms")]
    public async Task<IActionResult> CreateBOM([FromBody] BOMRequest request)
    {
        var item = await _context.Items.Include(i => i.BaseUoM).FirstOrDefaultAsync(i => i.Id == request.ItemId);
        if (item == null)
            return BadRequest(new { message = "Invalid item." });

        var version = ParseVersion(request.Version);
        var bom = new BOM
        {
            Id = Guid.NewGuid(),
            Code = $"{item.Code}-BOM-{version}",
            ItemId = request.ItemId,
            Version = version,
            ValidFrom = request.ValidFrom ?? DateTime.UtcNow,
            ValidTo = request.ValidTo,
            IsActive = request.IsActive,
            BaseQuantity = request.Quantity
        };

        bom.Lines = request.Lines.Select(line => new BOMLine
        {
            Id = Guid.NewGuid(),
            BOMId = bom.Id,
            LineNumber = line.SequenceNumber,
            ItemId = line.ComponentItemId,
            Quantity = line.Quantity,
            UoMId = line.UoMId,
            ScrapPercentage = line.ScrapFactor
        }).ToList();

        _context.BOMs.Add(bom);
        await _context.SaveChangesAsync();

        bom = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .FirstAsync(b => b.Id == bom.Id);

        return Ok(MapBom(bom));
    }

    [HttpPut("boms/{id}")]
    public async Task<IActionResult> UpdateBOM(Guid id, [FromBody] BOMRequest request)
    {
        var bom = await _context.BOMs
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bom == null)
            return NotFound();

        bom.ItemId = request.ItemId;
        bom.Version = ParseVersion(request.Version);
        bom.ValidFrom = request.ValidFrom ?? bom.ValidFrom;
        bom.ValidTo = request.ValidTo;
        bom.IsActive = request.IsActive;
        bom.BaseQuantity = request.Quantity;

        _context.BOMLines.RemoveRange(bom.Lines);
        bom.Lines = request.Lines.Select(line => new BOMLine
        {
            Id = Guid.NewGuid(),
            BOMId = bom.Id,
            LineNumber = line.SequenceNumber,
            ItemId = line.ComponentItemId,
            Quantity = line.Quantity,
            UoMId = line.UoMId,
            ScrapPercentage = line.ScrapFactor
        }).ToList();

        await _context.SaveChangesAsync();

        bom = await _context.BOMs
            .Include(b => b.Item)
            .ThenInclude(i => i.BaseUoM)
            .Include(b => b.Lines)
            .ThenInclude(l => l.Item)
            .Include(b => b.Lines)
            .ThenInclude(l => l.UoM)
            .FirstAsync(b => b.Id == id);

        return Ok(MapBom(bom));
    }

    [HttpDelete("boms/{id}")]
    public async Task<IActionResult> DeleteBOM(Guid id)
    {
        var bom = await _context.BOMs.FirstOrDefaultAsync(b => b.Id == id);
        if (bom == null)
            return NotFound();

        bom.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("routings")]
    public async Task<IActionResult> GetRoutings([FromQuery] Guid? itemId = null)
    {
        var query = _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .AsQueryable();

        if (itemId.HasValue)
            query = query.Where(r => r.ItemId == itemId.Value);

        var routings = await query.ToListAsync();
        return Ok(routings.Select(MapRouting).ToList());
    }

    [HttpGet("routings/{id}")]
    public async Task<IActionResult> GetRouting(Guid id)
    {
        var routing = await _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (routing == null)
            return NotFound();

        return Ok(MapRouting(routing));
    }

    [HttpGet("routings/item/{itemId}")]
    public async Task<IActionResult> GetRoutingsByItem(Guid itemId)
    {
        var routings = await _context.Routings
            .Include(r => r.Item)
            .Include(r => r.Operations)
            .ThenInclude(o => o.WorkCenter)
            .Where(r => r.ItemId == itemId)
            .ToListAsync();

        return Ok(routings.Select(MapRouting).ToList());
    }

    [HttpPost("routings")]
    public async Task<IActionResult> CreateRouting([FromBody] RoutingRequest request)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId);
        if (item == null)
            return BadRequest(new { message = "Invalid item." });

        var version = ParseVersion(request.Version);
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

        return Ok(MapRouting(routing));
    }

    [HttpPut("routings/{id}")]
    public async Task<IActionResult> UpdateRouting(Guid id, [FromBody] RoutingRequest request)
    {
        var routing = await _context.Routings
            .Include(r => r.Operations)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (routing == null)
            return NotFound();

        routing.ItemId = request.ItemId;
        routing.Version = ParseVersion(request.Version);
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

        return Ok(MapRouting(routing));
    }

    [HttpDelete("routings/{id}")]
    public async Task<IActionResult> DeleteRouting(Guid id)
    {
        var routing = await _context.Routings.FirstOrDefaultAsync(r => r.Id == id);
        if (routing == null)
            return NotFound();

        routing.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ItemDto MapItem(Item item)
    {
        return new ItemDto(
            item.Id,
            item.Code,
            item.Name,
            item.Description,
            item.Type,
            item.BaseUoMId,
            item.BaseUoM == null ? null : MapUoM(item.BaseUoM),
            item.IsBatchTracked,
            item.IsMRNTracked,
            item.CountryOfOrigin,
            item.HSCode,
            !item.IsDeleted,
            item.CreatedAt,
            item.CreatedBy,
            item.ModifiedAt,
            item.ModifiedBy
        );
    }

    private static PartnerDto MapPartner(Partner partner)
    {
        return new PartnerDto(
            partner.Id,
            partner.Code,
            partner.Name,
            partner.Type,
            partner.TaxNumber,
            null,
            null,
            partner.Address,
            null,
            null,
            partner.Country,
            partner.ContactPerson,
            partner.Email,
            partner.Phone,
            partner.IsActive,
            partner.CreatedAt,
            partner.CreatedBy,
            partner.ModifiedAt,
            partner.ModifiedBy
        );
    }

    private static WarehouseDto MapWarehouse(Warehouse warehouse)
    {
        return new WarehouseDto(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            null,
            warehouse.Address,
            warehouse.IsActive,
            warehouse.CreatedAt,
            warehouse.CreatedBy,
            warehouse.ModifiedAt,
            warehouse.ModifiedBy
        );
    }

    private static LocationDto MapLocation(Location location)
    {
        return new LocationDto(
            location.Id,
            location.WarehouseId,
            location.Warehouse == null ? null : MapWarehouse(location.Warehouse),
            location.Code,
            location.Name,
            location.Type,
            null,
            null,
            location.IsActive,
            location.CreatedAt,
            location.CreatedBy,
            location.ModifiedAt,
            location.ModifiedBy
        );
    }

    private static WorkCenterDto MapWorkCenter(WorkCenter workCenter)
    {
        return new WorkCenterDto(
            workCenter.Id,
            workCenter.Code,
            workCenter.Name,
            workCenter.Description,
            workCenter.IsActive
        );
    }

    private static MachineDto MapMachine(Machine machine)
    {
        return new MachineDto(
            machine.Id,
            machine.Code,
            machine.Name,
            machine.WorkCenterId,
            MapWorkCenter(machine.WorkCenter),
            machine.SerialNumber,
            machine.IsActive
        );
    }

    private static UoMDto MapUoM(UnitOfMeasure uom)
    {
        return new UoMDto(
            uom.Id,
            uom.Code,
            uom.Name,
            uom.Symbol,
            !uom.IsDeleted,
            uom.CreatedAt,
            uom.CreatedBy,
            uom.ModifiedAt,
            uom.ModifiedBy
        );
    }

    private static BomDto MapBom(BOM bom)
    {
        var uom = bom.Item?.BaseUoM;
        return new BomDto(
            bom.Id,
            bom.ItemId,
            bom.Item == null ? null : MapItem(bom.Item),
            bom.Version.ToString(),
            bom.BaseQuantity,
            uom?.Id ?? Guid.Empty,
            uom == null ? null : MapUoM(uom),
            bom.ValidFrom,
            bom.ValidTo,
            null,
            bom.IsActive,
            bom.Lines.Select(line => new BomLineDto(
                line.Id,
                line.BOMId,
                line.ItemId,
                line.Item == null ? null : MapItem(line.Item),
                line.Quantity,
                line.UoMId,
                line.UoM == null ? null : MapUoM(line.UoM),
                line.ScrapPercentage,
                line.LineNumber
            )).ToList(),
            bom.CreatedAt,
            bom.CreatedBy,
            bom.ModifiedAt,
            bom.ModifiedBy
        );
    }

    private static RoutingDto MapRouting(Routing routing)
    {
        return new RoutingDto(
            routing.Id,
            routing.ItemId,
            routing.Item == null ? null : MapItem(routing.Item),
            routing.Version.ToString(),
            null,
            routing.IsActive,
            routing.Operations.Select(op => new RoutingOperationDto(
                op.Id,
                op.RoutingId,
                op.SequenceNumber,
                op.WorkCenterId,
                op.WorkCenter == null ? null : MapWorkCenter(op.WorkCenter),
                op.OperationCode,
                op.StandardTimeMinutes,
                op.SetupTimeMinutes,
                op.Description
            )).ToList(),
            routing.CreatedAt,
            routing.CreatedBy,
            routing.ModifiedAt,
            routing.ModifiedBy
        );
    }

    private static int ParseVersion(string? version)
    {
        return int.TryParse(version, out var parsed) ? parsed : 1;
    }
}

public record ItemRequest(
    string Code,
    string Name,
    string? Description,
    LON.Domain.Enums.ItemType ItemType,
    Guid UoMId,
    bool IsBatchRequired,
    bool IsMRNRequired,
    string? CountryOfOrigin,
    string? HSCode,
    bool IsActive,
    decimal? StandardCost
);

public record PartnerRequest(
    string Code,
    string Name,
    LON.Domain.Enums.PartnerType PartnerType,
    string? TaxNumber,
    string? VatNumber,
    string? EoriNumber,
    string? Address,
    string? City,
    string? PostalCode,
    string? Country,
    string? ContactPerson,
    string? Email,
    string? Phone,
    bool IsActive
);

public record WarehouseRequest(
    string Code,
    string Name,
    string? Description,
    string? Address,
    bool IsActive
);

public record LocationRequest(
    Guid WarehouseId,
    string Code,
    string Name,
    LON.Domain.Enums.LocationType LocationType,
    Guid? ParentLocationId,
    string? Aisle,
    string? Rack,
    string? Shelf,
    string? Bin,
    decimal? MaxCapacity,
    bool IsActive
);

public record WorkCenterRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    decimal? StandardCostPerHour,
    decimal? Capacity
);

public record MachineRequest(
    string Code,
    string Name,
    Guid WorkCenterId,
    string? SerialNumber,
    bool IsActive
);

public record UoMRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive
);

public record BOMRequest(
    Guid ItemId,
    string Version,
    decimal Quantity,
    Guid UoMId,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    string? Notes,
    bool IsActive,
    List<BOMLineRequest> Lines
);

public record BOMLineRequest(
    Guid ComponentItemId,
    decimal Quantity,
    Guid UoMId,
    decimal ScrapFactor,
    int SequenceNumber
);

public record RoutingRequest(
    Guid ItemId,
    string Version,
    string? Description,
    bool IsActive,
    List<RoutingOperationRequest> Operations
);

public record RoutingOperationRequest(
    int OperationNumber,
    Guid WorkCenterId,
    string OperationName,
    decimal StandardTime,
    decimal SetupTime,
    string? Description
);

public record ItemDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    LON.Domain.Enums.ItemType ItemType,
    Guid UoMId,
    UoMDto? UoM,
    bool IsBatchRequired,
    bool IsMRNRequired,
    string? CountryOfOrigin,
    string? HSCode,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record PartnerDto(
    Guid Id,
    string Code,
    string Name,
    LON.Domain.Enums.PartnerType PartnerType,
    string? TaxNumber,
    string? VatNumber,
    string? EoriNumber,
    string? Address,
    string? City,
    string? PostalCode,
    string? Country,
    string? ContactPerson,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Address,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record LocationDto(
    Guid Id,
    Guid WarehouseId,
    WarehouseDto? Warehouse,
    string Code,
    string Name,
    LON.Domain.Enums.LocationType LocationType,
    Guid? ParentLocationId,
    LocationDto? ParentLocation,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record WorkCenterDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive
);

public record MachineDto(
    Guid Id,
    string Code,
    string Name,
    Guid WorkCenterId,
    WorkCenterDto? WorkCenter,
    string? SerialNumber,
    bool IsActive
);

public record UoMDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record BomDto(
    Guid Id,
    Guid ItemId,
    ItemDto? Item,
    string Version,
    decimal Quantity,
    Guid UoMId,
    UoMDto? UoM,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Notes,
    bool IsActive,
    List<BomLineDto> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record BomLineDto(
    Guid Id,
    Guid BomId,
    Guid ComponentItemId,
    ItemDto? ComponentItem,
    decimal Quantity,
    Guid UoMId,
    UoMDto? UoM,
    decimal ScrapFactor,
    int SequenceNumber
);

public record RoutingDto(
    Guid Id,
    Guid ItemId,
    ItemDto? Item,
    string Version,
    string? Description,
    bool IsActive,
    List<RoutingOperationDto> Operations,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record RoutingOperationDto(
    Guid Id,
    Guid RoutingId,
    int OperationNumber,
    Guid WorkCenterId,
    WorkCenterDto? WorkCenter,
    string OperationName,
    decimal StandardTime,
    decimal SetupTime,
    string? Description
);
