using LON.Domain.Common;
using LON.Domain.Enums;
using LON.Domain.Entities.Production;

namespace LON.Domain.Entities.MasterData;

public class Item : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ItemType Type { get; set; }
    public bool IsBatchTracked { get; set; }
    public bool IsMRNTracked { get; set; }
    public string? HSCode { get; set; }
    public string? CountryOfOrigin { get; set; }
    public Guid BaseUoMId { get; set; }
    public virtual UnitOfMeasure BaseUoM { get; set; } = null!;
    public decimal StandardCost { get; set; }
    public virtual ICollection<ItemUoMConversion> UoMConversions { get; set; } = new List<ItemUoMConversion>();
    public virtual ICollection<BOM> BOMs { get; set; } = new List<BOM>();
}

public class UnitOfMeasure : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
}

public class ItemUoMConversion : BaseEntity
{
    public Guid ItemId { get; set; }
    public virtual Item Item { get; set; } = null!;
    public Guid FromUoMId { get; set; }
    public virtual UnitOfMeasure FromUoM { get; set; } = null!;
    public Guid ToUoMId { get; set; }
    public virtual UnitOfMeasure ToUoM { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
}

public class Warehouse : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}

public class Location : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public LocationType Type { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Shelf { get; set; }
    public string? Bin { get; set; }
    public decimal? MaxCapacity { get; set; }
    public decimal? CurrentCapacity { get; set; }
    public bool IsActive { get; set; }
}

public class Partner : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PartnerType Type { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; }
}

public class Employee : BaseEntity
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; }
}

public class WorkCenter : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal StandardCostPerHour { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<Machine> Machines { get; set; } = new List<Machine>();
}

public class Machine : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public virtual WorkCenter WorkCenter { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; }
}
