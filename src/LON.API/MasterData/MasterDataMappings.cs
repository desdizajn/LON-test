using LON.Domain.Entities.MasterData;
using LON.Domain.Entities.Production;

namespace LON.API.MasterData;

// Shared mapping helpers extracted from the old MasterDataController (P6.10).
// Every per-domain controller imports these so the DTO shapes stay identical
// to the pre-split OpenAPI schema.
public static class MasterDataMappings
{
    public static ItemDto MapItem(Item item) => new(
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
        item.ModifiedBy,
        item.BaseCode,
        item.ColorCode,
        item.SizeCode,
        item.ParentItemId
    );

    public static PartnerDto MapPartner(Partner partner) => new(
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

    public static WarehouseDto MapWarehouse(Warehouse warehouse) => new(
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

    public static LocationDto MapLocation(Location location) => new(
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

    public static WorkCenterDto MapWorkCenter(WorkCenter workCenter) => new(
        workCenter.Id,
        workCenter.Code,
        workCenter.Name,
        workCenter.Description,
        workCenter.IsActive
    );

    public static MachineDto MapMachine(Machine machine) => new(
        machine.Id,
        machine.Code,
        machine.Name,
        machine.WorkCenterId,
        MapWorkCenter(machine.WorkCenter),
        machine.SerialNumber,
        machine.IsActive
    );

    public static UoMDto MapUoM(UnitOfMeasure uom) => new(
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

    public static BomDto MapBom(BOM bom)
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

    public static RoutingDto MapRouting(Routing routing) => new(
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

    public static int ParseVersion(string? version)
        => int.TryParse(version, out var parsed) ? parsed : 1;
}
