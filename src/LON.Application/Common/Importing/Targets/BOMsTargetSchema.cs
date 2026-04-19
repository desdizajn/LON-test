namespace LON.Application.Common.Importing.Targets;

/// <summary>
/// BOM import — rows are component lines. Parent FG item is either a
/// header-level default (one BOM per file) or a per-row column when the
/// file mixes multiple BOMs.
/// </summary>
public class BOMsTargetSchema : IImportTargetSchema
{
    public string TargetName => "BOMs";
    public string DisplayLabel => "Bills of Materials (production recipes)";

    public IReadOnlyList<ImportTargetField> Fields { get; } = new List<ImportTargetField>
    {
        new("parentItemCode", "Parent FG code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Either, LookupEntity: "Items", LookupField: "Code",
            Notes: "Finished-good item the BOM produces."),
        new("bomCode", "BOM code", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Either),
        new("baseQuantity", "Base quantity", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Either,
            Notes: "Output units per one run; defaults to 1."),

        new("componentItemCode", "Component item code", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "Items", LookupField: "Code"),
        new("componentQuantity", "Quantity per parent", ImportTargetFieldType.Decimal, Required: true, Scope: ImportTargetFieldScope.Row),
        new("componentUomCode", "Component UoM", ImportTargetFieldType.String, Required: true,
            Scope: ImportTargetFieldScope.Row, LookupEntity: "UnitsOfMeasure", LookupField: "Code"),
        new("scrapPct", "Scrap %", ImportTargetFieldType.Decimal, Required: false, Scope: ImportTargetFieldScope.Row),
        new("position", "Line position", ImportTargetFieldType.Integer, Required: false, Scope: ImportTargetFieldScope.Row,
            Notes: "Optional ordering override; when absent, row order wins.")
    };
}
