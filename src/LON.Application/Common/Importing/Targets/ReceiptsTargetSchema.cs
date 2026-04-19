namespace LON.Application.Common.Importing.Targets;

/// <summary>
/// Generic importer target that produces <c>CreateReceiptCommand</c>s.
/// Header scope = receipt-level fields (one per file); Row scope = per-line
/// <c>ReceiptLineDto</c> columns.
/// </summary>
public class ReceiptsTargetSchema : IImportTargetSchema
{
    public string TargetName => "Receipts";
    public string DisplayLabel => "Receipts (inventory inbound)";

    public IReadOnlyList<ImportTargetField> Fields { get; } = new List<ImportTargetField>
    {
        new("receiptDate", "Receipt date", ImportTargetFieldType.Date, Required: true, Scope: ImportTargetFieldScope.Header),
        new("warehouseCode", "Warehouse code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Header, LookupEntity: "Warehouses", LookupField: "Code"),
        new("locationCode", "Location code", ImportTargetFieldType.String, Required: false,
            Scope: ImportTargetFieldScope.Either, LookupEntity: "Locations", LookupField: "Code",
            Notes: "If blank, auto-resolve: Receiving location, then first active in warehouse."),
        new("partnerCode", "Partner code", ImportTargetFieldType.String, Required: false,
            Scope: ImportTargetFieldScope.Header, LookupEntity: "Partners", LookupField: "Code"),
        new("purchaseOrderNumber", "PO number", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Header),
        new("referenceNumber", "Reference #", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Header),

        new("itemCode", "Item code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "Items", LookupField: "Code"),
        new("quantity", "Quantity", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row),
        new("uomCode", "UoM code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "UnitsOfMeasure", LookupField: "Code"),
        new("batchNumber", "Batch number", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("mrn", "MRN", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Either,
            Notes: "May be header-level if one MRN applies to the whole file."),
        new("qualityStatus", "Quality status", ImportTargetFieldType.Enum, Required: false,
            Scope: ImportTargetFieldScope.Row, EnumValues: new[] { "OK", "Blocked", "Quarantine" }),
        new("expiryDate", "Expiry date", ImportTargetFieldType.Date, Required: false, Scope: ImportTargetFieldScope.Row),
        new("customsDeclarationNumber", "Customs declaration #", ImportTargetFieldType.String, Required: false,
            Scope: ImportTargetFieldScope.Either, LookupEntity: "CustomsDeclarations", LookupField: "DeclarationNumber",
            Notes: "Ties the receipt line to a specific declaration for LON chain tracking.")
    };
}
