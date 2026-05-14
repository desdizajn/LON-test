namespace LON.Application.Customs.ClientOrders;

/// <summary>
/// DTOs returned by ClientOrder query handlers + consumed by the FE hub UI.
/// Phase 17 §E1.
/// </summary>
public record ClientOrderFinishedGoodDto(
    Guid Id,
    Guid ItemId,
    string? ItemCode,
    string? ItemName,
    decimal Quantity,
    Guid UoMId,
    string? UoMCode,
    Guid? BOMId,
    decimal? UnitPriceForeign,
    string Currency,
    string? Notes);

public record ClientOrderDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerPartnerId,
    string? CustomerPartnerName,
    Guid LONAuthorizationId,
    string? LONAuthorizationNumber,
    string? CustomerOrderReference,
    DateTime OrderDate,
    DateTime? RequestedShipDate,
    int Status,            // ClientOrderStatus int value
    string StatusName,     // human-readable
    string? Notes,
    string? CancellationReason,
    DateTime CreatedAt,
    string CreatedBy,
    IReadOnlyList<ClientOrderFinishedGoodDto> FinishedGoods);

/// <summary>
/// Hub-card summary (counts only, no nested collections). Used by the
/// /orders list view + the top of /orders/:id hub.
/// </summary>
public record ClientOrderSummaryDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerPartnerId,
    string? CustomerPartnerName,
    Guid LONAuthorizationId,
    string? LONAuthorizationNumber,
    string? CustomerOrderReference,
    DateTime OrderDate,
    DateTime? RequestedShipDate,
    int Status,
    string StatusName,
    int FinishedGoodsCount,
    int DeclarationsCount,
    int ProductionOrdersCount,
    int ShipmentsCount);
