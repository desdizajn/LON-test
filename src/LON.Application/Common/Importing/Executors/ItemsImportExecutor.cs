using System.Text.RegularExpressions;
using LON.Application.Common.Interfaces;
using LON.Domain.Entities.MasterData;
using LON.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing.Executors;

public class ItemsImportExecutor : IImportTargetExecutor
{
    public string TargetName => "Items";

    /// <summary>
    /// KW12 code shape: FG `\d{5}\d{3}.*`, material `\d{7}\d{3}.*`. Parse into
    /// (base, color, size). When the code doesn't match the shape rule, the
    /// item is treated as a bare base item (no parent-variant relationship).
    /// </summary>
    internal static (string? Base, string? Color, string? Size) DecomposeCode(string code, ItemType type)
    {
        if (string.IsNullOrWhiteSpace(code)) return (null, null, null);
        var trimmed = code.Trim();

        // FG — first 5 chars must be digits; after that 3 digits of color; rest is size.
        if (type != ItemType.RawMaterial)
        {
            var m = Regex.Match(trimmed, @"^(\d{5})(\d{3})(.*)$");
            if (m.Success)
                return (m.Groups[1].Value, m.Groups[2].Value, NullIfEmpty(m.Groups[3].Value));
            return (trimmed, null, null); // doesn't match — treat as base
        }

        // Material — 7 digits + optional 3-digit color + optional size.
        var mm = Regex.Match(trimmed, @"^(\d{7})(\d{3})(.*)$");
        if (mm.Success)
            return (mm.Groups[1].Value, mm.Groups[2].Value, NullIfEmpty(mm.Groups[3].Value));
        if (Regex.IsMatch(trimmed, @"^\d{7}$"))
            return (trimmed, null, null);
        return (trimmed, null, null);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

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
        var tenantId = context.CurrentTenantId ?? Guid.Empty;
        // Cache base-Items created/resolved in this session so repeated
        // variants of the same base (e.g. 182485422XL-1/-2/-3 all → 18248)
        // share the parent. Keys by (BaseCode), value = Item (tracked).
        var baseCache = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var code = row.GetOrDefault<string>("code")!;
            var baseUoMId = row.GetOrDefault<Guid>("baseUoMCode");
            if (baseUoMId == Guid.Empty)
                return (false, created, $"Row {row.RowIndex}: baseUoMCode is required.");
            var typeName = row.GetOrDefault<string>("type") ?? "RawMaterial";
            if (!Enum.TryParse<ItemType>(typeName, ignoreCase: true, out var type))
                return (false, created, $"Row {row.RowIndex}: invalid item type '{typeName}'.");

            // Structured fields — explicit override if present, otherwise parse.
            // Bugfix 2026-04-19: when color/size were supplied but base wasn't,
            // the previous code fell back to `baseCode = code`, which left
            // every material-with-column-R-color row with BaseCode == Code
            // (e.g. `5000051401` kept its full code as base instead of the
            // 7-char `5000051`). Now we always parse first and let explicit
            // values override each field independently; if only color/size
            // are explicit, base is derived by stripping the known suffix.
            var explicitBase = row.GetOrDefault<string>("baseCode");
            var explicitColor = row.GetOrDefault<string>("colorCode");
            var explicitSize = row.GetOrDefault<string>("sizeCode");
            var (parsedBase, parsedColor, parsedSize) = DecomposeCode(code, type);
            var colorCode = NullIfEmpty(explicitColor) ?? parsedColor;
            var sizeCode = NullIfEmpty(explicitSize) ?? parsedSize;
            string? baseCode;
            if (!string.IsNullOrWhiteSpace(explicitBase))
            {
                baseCode = explicitBase;
            }
            else if (!string.IsNullOrWhiteSpace(explicitColor) || !string.IsNullOrWhiteSpace(explicitSize))
            {
                // Explicit color/size from the source (e.g. Matriks columns
                // R/S). Derive base by stripping those character counts from
                // the END of the full code.
                var suffixLen = (colorCode?.Length ?? 0) + (sizeCode?.Length ?? 0);
                baseCode = code.Length > suffixLen ? code.Substring(0, code.Length - suffixLen) : code;
            }
            else
            {
                baseCode = parsedBase;
            }
            var isVariant = !string.Equals(baseCode, code, StringComparison.OrdinalIgnoreCase)
                            && (!string.IsNullOrWhiteSpace(colorCode) || !string.IsNullOrWhiteSpace(sizeCode));

            // Resolve/create the BASE item first (only for variants).
            Guid? parentItemId = null;
            if (isVariant && !string.IsNullOrWhiteSpace(baseCode))
            {
                if (!baseCache.TryGetValue(baseCode!, out var baseItem))
                {
                    baseItem = tenantId == Guid.Empty
                        ? null
                        : await context.Items
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(
                                i => i.TenantId == tenantId && i.Code == baseCode,
                                cancellationToken);
                    if (baseItem is null)
                    {
                        baseItem = new Item
                        {
                            Id = Guid.NewGuid(),
                            Code = baseCode!,
                            Name = row.GetOrDefault<string>("name") ?? baseCode!,
                            Description = string.Empty,
                            Type = type,
                            BaseUoMId = baseUoMId,
                            BaseCode = baseCode,
                            ColorCode = null,
                            SizeCode = null,
                            ParentItemId = null
                        };
                        await context.Items.AddAsync(baseItem, cancellationToken);
                        created++;
                    }
                    else if (baseItem.IsDeleted)
                    {
                        // Undelete and refresh shape fields.
                        baseItem.IsDeleted = false;
                        baseItem.Type = type;
                        baseItem.BaseUoMId = baseUoMId;
                        baseItem.BaseCode = baseCode;
                    }
                    baseCache[baseCode!] = baseItem!;
                }
                parentItemId = baseItem!.Id;
            }

            // G7 — UPSERT semantics. Look past the global query filter so we
            // can tell "active duplicate" apart from "soft-deleted ghost".
            var existing = tenantId == Guid.Empty
                ? null
                : await context.Items
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Code == code, cancellationToken);
            if (existing is not null && !existing.IsDeleted)
            {
                // Active legacy item — only patch the NEW shape fields so
                // legacy ELON rows (which have no BaseCode/ColorCode/SizeCode)
                // gain the structure needed for variant reporting. Core
                // attributes (name, type, cost) stay as-is.
                existing.BaseCode ??= baseCode;
                existing.ColorCode ??= colorCode;
                existing.SizeCode ??= sizeCode;
                existing.ParentItemId ??= parentItemId;
                created++;
                continue;
            }
            if (existing is not null && existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.Name = row.GetOrDefault<string>("name") ?? existing.Name;
                existing.Description = row.GetOrDefault<string>("description") ?? existing.Description;
                existing.Type = type;
                existing.BaseUoMId = baseUoMId;
                existing.HSCode = row.GetOrDefault<string>("hsCode") ?? existing.HSCode;
                existing.CountryOfOrigin = row.GetOrDefault<string>("countryOfOrigin") ?? existing.CountryOfOrigin;
                existing.IsBatchTracked = row.GetOrDefault<bool>("isBatchTracked");
                existing.IsMRNTracked = row.GetOrDefault<bool>("isMRNTracked");
                existing.StandardCost = row.GetOrDefault<decimal>("standardCost");
                existing.BaseCode = baseCode;
                existing.ColorCode = colorCode;
                existing.SizeCode = sizeCode;
                existing.ParentItemId = parentItemId;
                created++;
                continue;
            }

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
                StandardCost = row.GetOrDefault<decimal>("standardCost"),
                BaseCode = baseCode,
                ColorCode = colorCode,
                SizeCode = sizeCode,
                ParentItemId = parentItemId
            };
            await context.Items.AddAsync(item, cancellationToken);
            created++;
        }

        return (true, created, null);
    }
}
