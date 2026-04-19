using System.Text;
using LON.Application.Common.Importing;
using LON.Domain.Enums;

namespace LON.Infrastructure.Services.Importing;

/// <summary>
/// Minimalistic CSV parser — handles comma and semicolon delimiters with
/// auto-detection on the first data line, quoted fields (RFC 4180-ish:
/// double-quote escapes), and trimmed whitespace around unquoted fields.
/// No external dep; sufficient for invoice exports, master-data spreadsheets,
/// and legacy-ELON dumps.
/// </summary>
public class CsvImportParser : IImportFileParser
{
    public virtual ImportSourceFormat Format => ImportSourceFormat.Csv;
    protected virtual char? ForceDelimiter => null;

    public ParsedImportFile Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, DetectEncoding(stream), leaveOpen: true);
        var text = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedImportFile(Format, Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());

        var delimiter = ForceDelimiter ?? DetectDelimiter(text);
        var lines = SplitLogicalLines(text);
        if (lines.Count == 0)
            return new ParsedImportFile(Format, Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());

        var headers = ParseLine(lines[0], delimiter).Select(s => s?.Trim() ?? string.Empty).ToList();
        var rows = new List<IReadOnlyList<string?>>();
        for (int i = 1; i < lines.Count; i++)
        {
            var cells = ParseLine(lines[i], delimiter);
            while (cells.Count < headers.Count) cells.Add(null);
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(cells.Take(headers.Count).ToList());
        }

        return new ParsedImportFile(Format, headers, rows);
    }

    private static Encoding DetectEncoding(Stream stream)
    {
        // Respect BOM if present; default to UTF-8 otherwise (legacy ELON exports
        // that use CP1251 will show mojibake — tenants are told to re-save as UTF-8).
        if (!stream.CanSeek) return Encoding.UTF8;
        stream.Position = 0;
        var bom = new byte[3];
        var read = stream.Read(bom, 0, 3);
        stream.Position = 0;
        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(true);
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
        return new UTF8Encoding(false);
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        var commas = firstLine.Count(c => c == ',');
        var semis = firstLine.Count(c => c == ';');
        var tabs = firstLine.Count(c => c == '\t');
        if (tabs > commas && tabs > semis) return '\t';
        if (semis > commas) return ';';
        return ',';
    }

    private static List<string> SplitLogicalLines(string text)
    {
        // Respect quoted newlines — scan char-by-char and track quote state.
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                current.Append(ch);
            }
            else if ((ch == '\n' || ch == '\r') && !inQuotes)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static List<string?> ParseLine(string line, char delimiter)
    {
        var cells = new List<string?>();
        var cell = new StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"'); // escaped quote
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }
            }
            else
            {
                if (ch == '"' && cell.Length == 0)
                {
                    inQuotes = true;
                }
                else if (ch == delimiter)
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(ch);
                }
            }
        }
        cells.Add(cell.ToString());
        return cells;
    }
}

/// <summary>Tab-separated variant — same parser, delimiter forced to '\t'.</summary>
public class TsvImportParser : CsvImportParser
{
    public override ImportSourceFormat Format => ImportSourceFormat.Tsv;
    protected override char? ForceDelimiter => '\t';
}
