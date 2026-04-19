using System.Text.Json;
using LON.Application.Common.Importing;
using LON.Domain.Enums;

namespace LON.Infrastructure.Services.Importing;

/// <summary>
/// Parses JSON as a flat table:
///   { "data": [ {col1: v, col2: v}, ... ] }   — preferred, "data" key is wrapper
///   [ {col1: v, col2: v}, ... ]               — root array, also supported
/// Headers = union of keys across first 50 objects (preserves insertion order).
/// Nested objects / arrays are stringified via JsonSerializer so the grid
/// stays flat; the user can then transform or ignore the column.
/// </summary>
public class JsonImportParser : IImportFileParser
{
    public ImportSourceFormat Format => ImportSourceFormat.Json;

    public ParsedImportFile Parse(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement;

        JsonElement array;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            array = data;
        }
        else
        {
            throw new InvalidOperationException(
                "JSON must be an array of objects or an object with a 'data' array property.");
        }

        var items = array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .ToList();
        if (items.Count == 0)
            return new ParsedImportFile(Format, Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());

        var headers = new List<string>();
        var seen = new HashSet<string>();
        foreach (var item in items.Take(50))
        {
            foreach (var prop in item.EnumerateObject())
            {
                if (seen.Add(prop.Name)) headers.Add(prop.Name);
            }
        }

        var rows = new List<IReadOnlyList<string?>>();
        foreach (var item in items)
        {
            var row = new List<string?>(headers.Count);
            foreach (var header in headers)
            {
                if (item.TryGetProperty(header, out var value))
                {
                    row.Add(Stringify(value));
                }
                else
                {
                    row.Add(null);
                }
            }
            rows.Add(row);
        }

        return new ParsedImportFile(Format, headers, rows);
    }

    private static string? Stringify(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText()
    };
}
