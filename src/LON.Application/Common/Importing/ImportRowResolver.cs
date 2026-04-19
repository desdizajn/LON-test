using System.Globalization;
using LON.Application.Common.Interfaces;
using LON.Application.Importing.DTOs;
using LON.Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;

namespace LON.Application.Common.Importing;

/// <summary>
/// Resolves every raw row of an <see cref="LON.Domain.Entities.Importing.ImportSession"/>
/// into a typed <see cref="ResolvedImportRow"/> using the session's mapping,
/// transforms, and the target's schema.
///
/// Pipeline per row:
///   1. For each mapped column, take the cell, apply in-memory transforms.
///   2. Fall back to header defaults for fields not covered by mapping.
///   3. Resolve LOOKUP transforms against the DB (Items.Code → Item.Id, etc).
///   4. Coerce each field to the schema's declared type.
///   5. Validate required fields.
/// </summary>
public class ImportRowResolver
{
    /// <summary>
    /// Entity name (used in LOOKUP:&lt;Entity&gt;.&lt;Field&gt;) → async resolver that
    /// maps a string value to a Guid Id (or null if not found). Registered
    /// here so the resolver stays in the Application layer while still
    /// using the injected <see cref="IApplicationDbContext"/>.
    /// </summary>
    private static readonly Dictionary<string, Func<IApplicationDbContext, string, string, CancellationToken, Task<Guid?>>>
        LookupResolvers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Items"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "Code", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.Items.Where(i => i.Code == value).Select(i => (Guid?)i.Id).FirstOrDefaultAsync(ct);
            },
            ["UnitsOfMeasure"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "Code", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.UnitsOfMeasure.Where(u => u.Code == value).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            },
            ["Warehouses"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "Code", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.Warehouses.Where(w => w.Code == value).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);
            },
            ["Locations"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "Code", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.Locations.Where(l => l.Code == value).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
            },
            ["Partners"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "Code", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.Partners.Where(p => p.Code == value).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            },
            ["CustomsDeclarations"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "DeclarationNumber", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.CustomsDeclarations.Where(d => d.DeclarationNumber == value).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
            },
            ["LONAuthorizations"] = async (ctx, field, value, ct) =>
            {
                if (!string.Equals(field, "AuthorizationNumber", StringComparison.OrdinalIgnoreCase))
                    return null;
                return await ctx.LONAuthorizations.Where(a => a.AuthorizationNumber == value)
                    .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
            }
        };

    public async Task<List<ResolvedImportRow>> ResolveAsync(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        ImportMapping mapping,
        ImportDefaults defaults,
        ImportTransforms transforms,
        IImportTargetSchema schema,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        // 1) Pre-compute, per mapped target field, the source column index +
        //    in-memory transforms + optional LOOKUP.
        var plan = BuildPlan(headers, mapping, transforms);

        var result = new List<ResolvedImportRow>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var resolved = new ResolvedImportRow { RowIndex = i + 1 };
            // Row cells -> typed fields via plan.
            foreach (var step in plan)
            {
                var raw = step.ColumnIndex < row.Count ? row[step.ColumnIndex] : null;
                var after = ImportTransformRunner.Apply(raw, step.InMemoryRules);
                if (!string.IsNullOrWhiteSpace(after) && step.LookupEntity is not null && step.LookupField is not null)
                {
                    var id = await ResolveLookupAsync(context, step.LookupEntity, step.LookupField, after!, cancellationToken);
                    if (id is null)
                    {
                        resolved.Errors.Add($"Row {i + 1}: {step.TargetField} — no {step.LookupEntity} where {step.LookupField}='{after}'.");
                        continue;
                    }
                    resolved.Fields[step.TargetField] = id.Value;
                    continue;
                }
                var coerced = Coerce(after, step.TargetField, schema, resolved);
                resolved.Fields[step.TargetField] = coerced;
            }

            // 2) Merge header defaults for fields not provided by row.
            foreach (var (key, rawValue) in defaults.Values)
            {
                if (resolved.Fields.ContainsKey(key)) continue;
                if (string.IsNullOrWhiteSpace(rawValue)) continue;
                var field = schema.Fields.FirstOrDefault(f => string.Equals(f.Name, key, StringComparison.OrdinalIgnoreCase));
                if (field is null) continue;
                if (!string.IsNullOrWhiteSpace(field.LookupEntity) && !string.IsNullOrWhiteSpace(field.LookupField))
                {
                    var id = await ResolveLookupAsync(context, field.LookupEntity!, field.LookupField!, rawValue!, cancellationToken);
                    if (id is null)
                    {
                        resolved.Errors.Add($"Row {i + 1}: default {key} — no {field.LookupEntity} where {field.LookupField}='{rawValue}'.");
                        continue;
                    }
                    resolved.Fields[field.Name] = id.Value;
                    continue;
                }
                var coerced = Coerce(rawValue, field.Name, schema, resolved);
                resolved.Fields[field.Name] = coerced;
            }

            // 3) Required-field check.
            foreach (var field in schema.Fields.Where(f => f.Required))
            {
                if (!resolved.Fields.TryGetValue(field.Name, out var v) || v is null
                    || (v is string s && string.IsNullOrWhiteSpace(s)))
                {
                    resolved.Errors.Add($"Row {i + 1}: required field '{field.Name}' is missing.");
                }
            }

            result.Add(resolved);
        }
        return result;
    }

    private static async Task<Guid?> ResolveLookupAsync(
        IApplicationDbContext ctx, string entity, string field, string value, CancellationToken ct)
    {
        if (!LookupResolvers.TryGetValue(entity, out var fn)) return null;
        return await fn(ctx, field, value, ct);
    }

    private static object? Coerce(string? input, string fieldName, IImportTargetSchema schema, ResolvedImportRow row)
    {
        var field = schema.Fields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return input;
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        try
        {
            return field.Type switch
            {
                ImportTargetFieldType.String => trimmed,
                ImportTargetFieldType.Integer => int.Parse(trimmed, CultureInfo.InvariantCulture),
                ImportTargetFieldType.Decimal => decimal.Parse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture),
                ImportTargetFieldType.Boolean => ParseBool(trimmed),
                ImportTargetFieldType.Date or ImportTargetFieldType.DateTime => ParseDate(trimmed),
                ImportTargetFieldType.Guid => Guid.Parse(trimmed),
                ImportTargetFieldType.Enum => ValidateEnum(trimmed, field, row),
                _ => trimmed
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            row.Errors.Add($"Row {row.RowIndex}: {field.Name} — '{input}' is not a valid {field.Type}.");
            return null;
        }
    }

    private static object ValidateEnum(string value, ImportTargetField field, ResolvedImportRow row)
    {
        if (field.EnumValues is null) return value;
        if (field.EnumValues.Any(e => string.Equals(e, value, StringComparison.OrdinalIgnoreCase)))
            return field.EnumValues.First(e => string.Equals(e, value, StringComparison.OrdinalIgnoreCase));
        row.Errors.Add($"Row {row.RowIndex}: {field.Name} — '{value}' is not one of {string.Join(", ", field.EnumValues)}.");
        return value;
    }

    private static bool ParseBool(string s) => s.ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "y" or "t" => true,
        "false" or "0" or "no" or "n" or "f" => false,
        _ => throw new FormatException($"'{s}' is not a boolean.")
    };

    private static DateTime ParseDate(string s)
    {
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        if (DateTime.TryParse(s, out dt)) return dt;
        throw new FormatException($"'{s}' is not a valid date.");
    }

    private sealed record ColumnPlan(
        int ColumnIndex,
        string TargetField,
        List<string> InMemoryRules,
        string? LookupEntity,
        string? LookupField);

    private static List<ColumnPlan> BuildPlan(
        IReadOnlyList<string> headers,
        ImportMapping mapping,
        ImportTransforms transforms)
    {
        var plan = new List<ColumnPlan>();
        var transformsByHeader = transforms.Columns
            .GroupBy(c => c.SourceHeader, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.SelectMany(c => c.Rules).ToList(), StringComparer.OrdinalIgnoreCase);

        var headersList = headers as IList<string> ?? headers.ToList();
        foreach (var col in mapping.Columns)
        {
            if (col.Ignore || string.IsNullOrWhiteSpace(col.TargetField)) continue;
            int idx = -1;
            for (int i = 0; i < headersList.Count; i++)
            {
                if (string.Equals(headersList[i], col.SourceHeader, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0) continue;

            var rules = transformsByHeader.TryGetValue(col.SourceHeader, out var rs) ? rs : new List<string>();
            var inMemRules = rules.Where(ImportTransformRunner.IsInMemoryRule).ToList();
            var lookupRule = rules.FirstOrDefault(r => r.StartsWith("LOOKUP:", StringComparison.OrdinalIgnoreCase));
            string? lookupEntity = null, lookupField = null;
            if (lookupRule is not null)
            {
                var body = lookupRule.Substring("LOOKUP:".Length);
                var parts = body.Split('.', 2);
                if (parts.Length == 2)
                {
                    lookupEntity = parts[0].Trim();
                    lookupField = parts[1].Trim();
                }
            }
            plan.Add(new ColumnPlan(idx, col.TargetField!, inMemRules, lookupEntity, lookupField));
        }
        return plan;
    }
}
