// Shared request + response DTOs for the MasterData controllers, split out of
// the old monolithic MasterDataController (P6.10). Living in LON.API.MasterData
// keeps the controllers tidy without changing the public URL contract or
// the OpenAPI schema names that the frontend consumes.

namespace LON.API.MasterData;

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
    decimal? StandardCost,
    string? PartnerSKU = null,
    // P15.6 waste slots (legacy ArtKatBrMatOtpad/1/2 + ArtKatBrMatZaguba)
    Guid? PrimaryWasteItemId = null,
    decimal? PrimaryWastePercentage = null,
    Guid? SecondaryWasteItemId = null,
    decimal? SecondaryWastePercentage = null,
    Guid? TertiaryWasteItemId = null,
    decimal? TertiaryWastePercentage = null,
    Guid? ZagubaItemId = null,
    decimal? ZagubaPercentage = null,
    string? WasteTariffCode = null,
    bool IsWasteCatalog = false
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
    // G8 — nullable so an omitted field defaults to active instead of silently
    // soft-deleting. Positional `bool` previously defaulted to false.
    bool? IsActive = true
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
    string? UpdatedBy,
    // KW12 color/size decomposition + parent link (null on base items).
    string? BaseCode,
    string? ColorCode,
    string? SizeCode,
    Guid? ParentItemId
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
