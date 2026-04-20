namespace LON.Application.Common.Importing;

/// <summary>
/// P6.34 — opt-in extension over <see cref="IImportFileParser"/>: return every
/// worksheet in a workbook instead of only the first. Used by the KW12 preset
/// orchestrator to turn a single xlsx into 3 ImportSessions (Matriks,
/// Faktura, Transport). The basic parser contract stays single-sheet so every
/// other caller keeps its assumptions intact.
/// </summary>
public interface IXlsxMultiSheetParser
{
    IReadOnlyDictionary<string, ParsedImportFile> ParseAllSheets(Stream stream);
}
