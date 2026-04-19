using ClosedXML.Excel;
using LON.Application.Common.Importing;
using LON.Domain.Enums;

namespace LON.Infrastructure.Services.Importing;

/// <summary>
/// Parses the FIRST worksheet of an .xlsx/.xls/.xlsm workbook. Row 1 is
/// treated as header; everything below is data. Blank trailing rows/columns
/// are skipped via ClosedXML's <see cref="IXLWorksheet.RangeUsed"/>.
/// </summary>
public class XlsxImportParser : IImportFileParser
{
    public ImportSourceFormat Format => ImportSourceFormat.Xlsx;

    public ParsedImportFile Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook has no worksheets.");

        var used = sheet.RangeUsed();
        if (used is null)
            return new ParsedImportFile(Format, Array.Empty<string>(), Array.Empty<IReadOnlyList<string?>>());

        var firstRow = used.FirstRow();
        var headers = firstRow.Cells()
            .Select(c => c.GetString().Trim())
            .ToList();

        var rows = new List<IReadOnlyList<string?>>();
        var dataRows = used.RowsUsed().Skip(1);
        foreach (var row in dataRows)
        {
            var values = new List<string?>();
            for (int c = 1; c <= headers.Count; c++)
            {
                var cell = row.Cell(c);
                values.Add(cell.IsEmpty() ? null : cell.GetString());
            }
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(values);
        }

        return new ParsedImportFile(Format, headers, rows);
    }
}
