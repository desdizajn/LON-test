namespace LON.Application.Common.Importing.Targets;

/// <summary>
/// P6.22 — target for textile-style flattened BOM-per-work-order imports
/// (KW12 Matriks sheet). Each row is one material line belonging to one
/// <c>ProductionOrder</c>; rows are grouped by <c>workOrderNumber</c>.
/// The first row of each group provides the order header (item, qty,
/// planned start, customer, etc.); all rows contribute a
/// <see cref="LON.Domain.Entities.Production.ProductionOrderMaterial"/>.
/// </summary>
public class ProductionOrdersTargetSchema : IImportTargetSchema
{
    public string TargetName => "ProductionOrders";
    public string DisplayLabel => "Production orders (textile / flattened BOM)";

    public IReadOnlyList<ImportTargetField> Fields { get; } = new List<ImportTargetField>
    {
        // === Per-row header identity — same value across all rows of the same WO ===
        new("workOrderNumber", "Work order number", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row,
            Notes: "Rows are grouped by this field; one ProductionOrder per distinct value."),
        new("productCode", "Finished good code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "Items", LookupField: "Code"),
        new("orderQuantity", "Order quantity", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row,
            Notes: "Same value on every row of the same WO; first wins."),
        new("plannedStart", "Planned start date", ImportTargetFieldType.Date, Required: false, Scope: ImportTargetFieldScope.Row),
        new("customerOrderNumber", "Customer order #", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "Stored on ProductionOrder.CustomerOrderNumber (S1)."),
        new("customerPartnerCode", "Customer partner code", ImportTargetFieldType.String, Required: false,
            Scope: ImportTargetFieldScope.Either, LookupEntity: "Partners", LookupField: "Code",
            Notes: "Firma/client from the file; populates ProductionOrder.CustomerPartnerId (G6)."),
        new("weekNumber", "Week number", ImportTargetFieldType.Integer, Required: false, Scope: ImportTargetFieldScope.Either),

        // === Header-level (same across the whole file) ===
        new("warehouseCode", "Warehouse code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Header, LookupEntity: "Warehouses", LookupField: "Code"),
        new("productUomCode", "Finished good UoM", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Header, LookupEntity: "UnitsOfMeasure", LookupField: "Code",
            Notes: "Applied as ProductionOrder.UoMId for every imported order."),
        new("status", "Initial status", ImportTargetFieldType.Enum, Required: false,
            Scope: ImportTargetFieldScope.Header,
            EnumValues: new[] { "Draft", "Released" },
            Notes: "Defaults to Draft if omitted."),

        // === Per-row material line ===
        new("materialItemCode", "Material item code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "Items", LookupField: "Code"),
        new("materialQuantity", "Material required qty", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row),
        new("materialUomCode", "Material UoM", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "UnitsOfMeasure", LookupField: "Code"),
        new("materialPreAssignedMRN", "Pre-assigned MRN", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "G3 — material will be issued from this MRN specifically, bypassing FEFO auto-pick."),
        new("materialPreAssignedBatch", "Pre-assigned batch", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("efficiencyFactor", "Efficiency factor", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "KW12 EFF column; 0.8934 = ~11% scrap."),
    };
}
