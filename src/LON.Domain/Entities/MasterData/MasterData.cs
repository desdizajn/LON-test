using LON.Domain.Common;
using LON.Domain.Enums;
using LON.Domain.Entities.Production;

namespace LON.Domain.Entities.MasterData;

public class Item : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

    /// <summary>
    /// KW12 — the base article code with color/size suffix stripped.
    /// Finished goods: first 5 digits of <see cref="Code"/> (e.g. "18248").
    /// Materials: first 7 digits (e.g. "1000010"). Null on already-base items.
    /// Reports that aggregate by article (no color/size) group on this column.
    /// </summary>
    public string? BaseCode { get; set; }

    /// <summary>
    /// 3-char color code when present (e.g. "542" Gold, "010" Black).
    /// Null on base items and on materials whose catalog entry has no color.
    /// </summary>
    public string? ColorCode { get; set; }

    /// <summary>
    /// Remaining suffix after base + color, e.g. "2XL-3" on FG or "5" on the
    /// 5000038530953-style dimensional material. Null when no size suffix.
    /// </summary>
    public string? SizeCode { get; set; }

    /// <summary>
    /// Link from a variant to its base (color/size-less) Item. Null when this
    /// IS the base. Allows UI to render a collapsible base → variants tree
    /// and to answer "how many of article 18248 across all colors/sizes".
    /// </summary>
    public Guid? ParentItemId { get; set; }
    public virtual Item? Parent { get; set; }
    public virtual ICollection<Item> Variants { get; set; } = new List<Item>();

    /// <summary>
    /// P15.1 — legacy <c>tblArtikli.ArtKatBrStara</c>. The partner's own SKU
    /// (customer or supplier code) for this item; used during bulk invoice
    /// import to disambiguate an incoming line that references the partner's
    /// code instead of our internal <see cref="Code"/>. Null when the item
    /// has no external crosswalk. Nonunique: two different internal items
    /// CAN share the same partner SKU when the partner reused the code, so
    /// the import path falls back to an interactive disambiguation UI when
    /// multiple matches exist (legacy <c>frmArtKatBrStara</c>).
    /// </summary>
    public string? PartnerSKU { get; set; }

    // ===== P15.6 — waste slots (legacy tblArtikli ArtKatBrMatOtpad/1/2 + ArtKatBrMatZaguba) =====
    //
    // Each material carries up to FOUR waste outcomes:
    //   Primary / Secondary / Tertiary — recoverable by-products that leave
    //   under their own tariff (sold as scrap, downgraded, or otherwise
    //   accounted for in inventory).
    //   Zaguba — non-recoverable loss (mass that evaporates, burns off,
    //   or is otherwise unreturnable; still must be declared to customs
    //   but has no physical catalog entry leaving the building).
    //
    // Percentages are of the CONSUMED input material. Example: a fabric
    // with PrimaryWastePercentage=5 means "when you consume 100 kg of this
    // material, 5 kg becomes PrimaryWasteItem (e.g. fabric-scrap) and
    // the remaining 95 kg enters the finished product".
    //
    // The BOM and ProductionOrderMaterial carry their own overrides; Item
    // values are DEFAULTS used when a new BOM line is created for this
    // material and no explicit override is supplied.

    /// <summary>Waste slot 0 — saleable by-product catalog item.</summary>
    public Guid? PrimaryWasteItemId { get; set; }
    public virtual Item? PrimaryWasteItem { get; set; }
    public decimal? PrimaryWastePercentage { get; set; }

    /// <summary>Waste slot 1 — secondary by-product (separate tariff / channel).</summary>
    public Guid? SecondaryWasteItemId { get; set; }
    public virtual Item? SecondaryWasteItem { get; set; }
    public decimal? SecondaryWastePercentage { get; set; }

    /// <summary>Waste slot 2 — tertiary by-product.</summary>
    public Guid? TertiaryWasteItemId { get; set; }
    public virtual Item? TertiaryWasteItem { get; set; }
    public decimal? TertiaryWastePercentage { get; set; }

    /// <summary>Zaguba — non-recoverable loss (dust, vapor, mass unreturnable).</summary>
    public Guid? ZagubaItemId { get; set; }
    public virtual Item? ZagubaItem { get; set; }
    public decimal? ZagubaPercentage { get; set; }

    /// <summary>
    /// Legacy <c>ArtOtpadTarBr</c> — tariff code of the waste material itself
    /// when the item IS a waste catalog entry. Distinct from <see cref="HSCode"/>
    /// (which is this item's tariff as a raw material). Used by waste declarations
    /// so the outbound waste line gets the correct tariff without a separate lookup.
    /// </summary>
    public string? WasteTariffCode { get; set; }

    /// <summary>
    /// Legacy <c>ArtOtpadZao</c>. True = this item IS a waste-catalog entry
    /// (i.e. it's what another item's waste slot points at), NOT a product
    /// that produces waste. Drives UI filtering (waste pickers) and excludes
    /// these rows from ordinary production BOMs.
    /// </summary>
    public bool IsWasteCatalog { get; set; }

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

public class Warehouse : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}

public class Location : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

public class Partner : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
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

public class Employee : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public DateTime? HireDate { get; set; }
    public Guid? ShiftId { get; set; }
    public virtual Shift? Shift { get; set; }
    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }
    public bool IsActive { get; set; }
}

public class User : BaseEntity, ITenantScoped, IAuditable
{
    public Guid TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public virtual Employee? Employee { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;
}

public class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty; // MasterData, Production, Customs, WMS, etc.
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;
    public Guid PermissionId { get; set; }
    public virtual Permission Permission { get; set; } = null!;
}

public class Shift : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class WorkCenter : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal StandardCostPerHour { get; set; }
    public decimal Capacity { get; set; }
    public bool IsActive { get; set; }
    public virtual ICollection<Machine> Machines { get; set; } = new List<Machine>();
}

public class Machine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid WorkCenterId { get; set; }
    public virtual WorkCenter WorkCenter { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; }
}
