using LON.Domain.Enums;

namespace LON.Application.Common.Importing;

/// <summary>Result of parsing a single uploaded tabular file.</summary>
public sealed record ParsedImportFile(
    ImportSourceFormat Format,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>Parser for ONE concrete format. Registry picks the right one by extension.</summary>
public interface IImportFileParser
{
    ImportSourceFormat Format { get; }
    ParsedImportFile Parse(Stream stream);
}

/// <summary>
/// Dispatches to the right <see cref="IImportFileParser"/> based on file extension
/// (with content sniffing for CSV vs TSV when ambiguous).
/// </summary>
public interface IImportFileParserRegistry
{
    /// <summary>Reads the whole stream to memory once, detects format, delegates to parser.</summary>
    ParsedImportFile Parse(Stream stream, string fileName);

    /// <summary>Returns format for a given file name; throws <see cref="NotSupportedException"/> if unknown.</summary>
    ImportSourceFormat DetectFormat(string fileName, byte[] firstBytes);
}
