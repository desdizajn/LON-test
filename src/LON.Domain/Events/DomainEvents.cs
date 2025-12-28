using LON.Domain.Common;

namespace LON.Domain.Events;

public class InventoryMovedEvent : DomainEvent
{
    public Guid ItemId { get; set; }
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string MovementType { get; set; } = string.Empty;
}

public class MaterialIssuedEvent : DomainEvent
{
    public Guid ProductionOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string? BatchNumber { get; set; }
    public string? MRN { get; set; }
    public decimal Quantity { get; set; }
}

public class FGReceivedEvent : DomainEvent
{
    public Guid ProductionOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid LocationId { get; set; }
}

public class GuaranteeDebitedEvent : DomainEvent
{
    public Guid GuaranteeAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? MRN { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
}

public class GuaranteeCreditedEvent : DomainEvent
{
    public Guid GuaranteeAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? MRN { get; set; }
    public Guid? CustomsDeclarationId { get; set; }
}

public class CustomsClearedEvent : DomainEvent
{
    public Guid CustomsDeclarationId { get; set; }
    public string MRN { get; set; } = string.Empty;
    public DateTime ClearedDate { get; set; }
}

public class ProductionOrderCompletedEvent : DomainEvent
{
    public Guid ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public decimal ProducedQuantity { get; set; }
}

public class ShipmentCreatedEvent : DomainEvent
{
    public Guid ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public DateTime ShipmentDate { get; set; }
}

public class ReceiptCreatedEvent : DomainEvent
{
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
}
