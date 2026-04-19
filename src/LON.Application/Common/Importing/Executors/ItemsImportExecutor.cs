using LON.Application.Common.Interfaces;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

public class ItemsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "Items";

    public async Task<(bool Ok, int Created, string? Error)> ExecuteAsync(
        IReadOnlyList<ResolvedImportRow> rows,
        IReadOnlyDictionary<string, object?> headerDefaults,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        // Guardrail — duplicate Code inside the same file is rejected up front
        // so the atomic SaveChanges doesn't explode on the unique index.
        var codesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = row.GetOrDefault<string>("code");
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (!codesSeen.Add(code))
                return (false, 0, $"Row {row.RowIndex}: duplicate code '{code}' within the same file.");
        }

        int created = 0;
        foreach (var row in rows)
        {
            var code = row.GetOrDefault<string>("code")!;
            var existing = await context.Items.AnyAsync(i => i.Code == code, cancellationToken);
            if (existing)
            {
                row.Errors.Add($"Row {row.RowIndex}: Item with code '{code}' already exists.");
                return (false, created, $"Row {row.RowIndex}: Item code '{code}' is already taken.");
            }

            var baseUoMId = row.GetOrDefault<Guid>("baseUoMCode");
            if (baseUoMId == Guid.Empty)
                return (false, created, $"Row {row.RowIndex}: baseUoMCode is required.");

            var typeName = row.GetOrDefault<string>("type") ?? "RawMaterial";
            if (!Enum.TryParse<ItemType>(typeName, ignoreCase: true, out var type))
                return (false, created, $"Row {row.RowIndex}: invalid item type '{typeName}'.");

            var item = new Item
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = row.GetOrDefault<string>("name") ?? code,
                Description = row.GetOrDefault<string>("description") ?? string.Empty,
                Type = type,
                BaseUoMId = baseUoMId,
                HSCode = row.GetOrDefault<string>("hsCode"),
                CountryOfOrigin = row.GetOrDefault<string>("countryOfOrigin"),
                IsBatchTracked = row.GetOrDefault<bool>("isBatchTracked"),
                IsMRNTracked = row.GetOrDefault<bool>("isMRNTracked"),
                StandardCost = row.GetOrDefault<decimal>("standardCost")
            };
            await context.Items.AddAsync(item, cancellationToken);
            created++;
        }

        return (true, created, null);
    }
}
