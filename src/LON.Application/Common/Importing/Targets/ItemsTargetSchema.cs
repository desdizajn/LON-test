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
        new("standardCost", "Standard cost", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Row),

        // KW12 color/size structure. When any of these are NOT mapped, the
        // executor auto-parses them from <c>Code</c> using the shape rules:
        //   FG (type != RawMaterial): 5 digits + 3 digits color + rest size
        //   Material: 7 digits + 3 digits color + rest size
        // The parent/base item is auto-created (or undeleted) and linked
        // via <c>Item.ParentItemId</c>, so the variant → base tree falls
        // out of a single Items import.
        new("baseCode", "Base article code", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "Override: skip auto-parsing and use this directly. Default: computed from Code."),
        new("colorCode", "Color code", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "From e.g. column R (materials) or auto-parsed (FG)."),
        new("sizeCode", "Size / dimension", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "From e.g. column S (materials) or auto-parsed (FG)."),
    };
}
