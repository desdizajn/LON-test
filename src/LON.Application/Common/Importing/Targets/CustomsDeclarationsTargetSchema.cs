namespace LON.Application.Common.Importing.Targets;

/// <summary>
/// Import source for customs declarations (IM 4200, IM 5100, EX 3151 etc).
/// One header = one declaration; rows = declaration lines. Only covers
/// enough fields to reconstruct a CreateImportDeclarationCommand /
/// CreateExportDeclarationCommand; advanced fields (document attachments,
/// specific SAD boxes) stay on the dedicated declaration UI.
/// </summary>
public class CustomsDeclarationsTargetSchema : IImportTargetSchema
{
    public string TargetName => "CustomsDeclarations";
    public string DisplayLabel => "Customs declarations (IM / EX)";

    public IReadOnlyList<ImportTargetField> Fields { get; } = new List<ImportTargetField>
    {
        new("declarationNumber", "Declaration number / MRN", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Header),
        new("declarationDate", "Declaration date", ImportTargetFieldType.Date, Required: true, Scope: ImportTargetFieldScope.Header),
        new("declarationType", "IM / EX", ImportTargetFieldType.Enum, Required: true, Scope: ImportTargetFieldScope.Header,
            EnumValues: new[] { "IM", "EX" }),
        new("procedureCode", "Procedure code (Box 37)", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Header),
        new("previousProcedureCode", "Previous procedure", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Header),
        new("partnerCode", "Partner code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Header, LookupEntity: "Partners", LookupField: "Code"),
        new("currencyCode", "Currency (ISO-3)", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Header),
        new("exchangeRate", "Exchange rate", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Header),
        new("lonAuthorizationCode", "LON authorization #", ImportTargetFieldType.String, Required: false,
            Scope: ImportTargetFieldScope.Header, LookupEntity: "LONAuthorizations", LookupField: "AuthorizationNumber"),

        new("itemCode", "Item code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "Items", LookupField: "Code"),
        new("tariffCode", "Tariff code (CN8)", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row),
        new("originCountry", "Origin country (ISO-2)", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row),
        new("quantity", "Quantity", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row),
        new("uomCode", "UoM code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "UnitsOfMeasure", LookupField: "Code"),
        new("netWeight", "Net weight (kg)", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row),
        new("grossWeight", "Gross weight (kg)", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Row),
        new("invoiceValue", "Invoice value", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row),
        new("vatRate", "VAT %", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Row)
    };
}
