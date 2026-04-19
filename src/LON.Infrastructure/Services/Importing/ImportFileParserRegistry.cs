using LON.Application.Common.Importing;
using LON.Domain.Enums;

namespace LON.Infrastructure.Services.Importing;

public class ImportFileParserRegistry : IImportFileParserRegistry
{
    private readonly IReadOnlyDictionary<ImportSourceFormat, IImportFileParser> _byFormat;

    public ImportFileParserRegistry(IEnumerable<IImportFileParser> parsers)
    {
        _byFormat = parsers.ToDictionary(p => p.Format);
    }

    public ImportSourceFormat DetectFormat(string fileName, byte[] firstBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xlsm" or ".xls" => ImportSourceFormat.Xlsx,
            ".csv" => SniffDelimiter(firstBytes) == '\t' ? ImportSourceFormat.Tsv : ImportSourceFormat.Csv,
            ".tsv" or ".tab" => ImportSourceFormat.Tsv,
            ".json" => ImportSourceFormat.Json,
            ".xml" => ImportSourceFormat.Xml,
            _ => throw new NotSupportedException(
                $"Unsupported file extension '{ext}'. Supported: .xlsx, .xls, .csv, .tsv, .json, .xml.")
        };
    }

    public ParsedImportFile Parse(Stream stream, string fileName)
    {
        // Buffer the stream so we can both sniff and hand off to the parser.
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        var format = DetectFormat(fileName, bytes);
        if (!_byFormat.TryGetValue(format, out var parser))
            throw new NotSupportedException($"No parser registered for format '{format}'.");

        using var parseStream = new MemoryStream(bytes, writable: false);
        return parser.Parse(parseStream);
    }

    private static char SniffDelimiter(byte[] firstBytes)
    {
        // Inspect first ~4KB of text to decide between comma and tab.
        // Semicolon-separated files (common in EU exports) are treated as CSV too;
        // the CsvImportParser auto-detects between ',' and ';' row-by-row.
        var len = Math.Min(firstBytes.Length, 4096);
        if (len == 0) return ',';
        var sample = System.Text.Encoding.UTF8.GetString(firstBytes, 0, len);
        var firstLine = sample.Split('\n', 2)[0];
        var tabs = firstLine.Count(c => c == '\t');
        var commas = firstLine.Count(c => c == ',');
        var semis = firstLine.Count(c => c == ';');
        if (tabs > commas && tabs > semis) return '\t';
        return ',';
    }
}
