namespace LON.Application.Common.Importing.Targets;

/// <summary>
/// Items catalog target. One row = one <c>Item</c>. Each item row is
/// independent; no header-level defaults needed beyond optional metadata.
/// </summary>
public class ItemsTargetSchema : IImportTargetSchema
{
    public string TargetName => "Items";
    public string DisplayLabel => "Items catalog";

    public IReadOnlyList<ImportTargetField> Fields { get; } = new List<ImportTargetField>
    {
        new("code", "Item code", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row),
        new("name", "Name", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row),
        new("description", "Description", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("type", "Type", ImportTargetFieldType.Enum, Required: true, Scope: ImportTargetFieldScope.Either,
            EnumValues: new[] { "RawMaterial", "SemiFinished", "FinishedGood", "Packaging" }),
        new("baseUoMCode", "Base UoM code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Either, LookupEntity: "UnitsOfMeasure", LookupField: "Code"),
        new("hsCode", "HS code", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("countryOfOrigin", "Country (ISO-2)", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("isBatchTracked", "Batch tracked", ImportTargetFieldType.Boolean, Required: false, Scope: ImportTargetFieldScope.Either),
        new("isMRNTracked", "MRN tracked", ImportTargetFieldType.Boolean, Required: false, Scope: ImportTargetFieldScope.Either),
        new("standardCost", "Standard cost", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Row)
    };
}
