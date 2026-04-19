namespace LON.Application.Common.Importing;

public class ImportTargetRegistry : IImportTargetRegistry
{
    public IReadOnlyList<IImportTargetSchema> All { get; }

    public ImportTargetRegistry(IEnumerable<IImportTargetSchema> schemas)
    {
        All = schemas
            .OrderBy(s => s.TargetName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IImportTargetSchema? Find(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return null;
        return All.FirstOrDefault(s => string.Equals(s.TargetName, targetName, StringComparison.OrdinalIgnoreCase));
    }
}
