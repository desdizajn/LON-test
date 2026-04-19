namespace LON.Application.Common.Importing.Targets;

public class PartnersTargetSchema : IImportTargetSchema
{
    public string TargetName => "Partners";
    public string DisplayLabel => "Business partners (suppliers, customers, carriers)";

    public IReadOnlyList<ImportTargetField> Fields { get; } = new List<ImportTargetField>
    {
        new("code", "Partner code", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row),
        new("name", "Name", ImportTargetFieldType.String, Required: true, Scope: ImportTargetFieldScope.Row),
        new("type", "Type", ImportTargetFieldType.Enum, Required: true, Scope: ImportTargetFieldScope.Either,
            EnumValues: new[] { "Supplier", "Customer", "Carrier", "CustomsBroker", "Bank" }),
        new("taxId", "Tax ID / EDB", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("address", "Address", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("country", "Country (ISO-2)", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("email", "Email", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row),
        new("phone", "Phone", ImportTargetFieldType.String, Required: false, Scope: ImportTargetFieldScope.Row)
    };
}
