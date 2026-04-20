using System.Text.Json;
using LON.Application.Common.Commands;
using LON.Application.Common.Importing;
using LON.Application.Common.Interfaces;
using LON.Application.Common.Models;
using LON.Domain.Entities.Importing;
using LON.Domain.Enums;

namespace LON.Application.Importing.Commands.CreateKw12ImportBundle;

/// <summary>
/// P6.34 — one-shot orchestrator. The user drops a KW12 workbook and gets
/// back a bundle of pre-configured <see cref="ImportSession"/> IDs in the
/// order they must be executed:
///
///   1. Items  (from the Matriks sheet — creates/updates item catalog)
///   2. CustomsDeclarations  (from the Faktura sheet — registers the import)
///   3. ProductionOrders / Receipts  (from Matriks/Transport — downstream stock)
///
/// Each session lands with its TargetEntity pre-set so the wizard can skip
/// the "pick a target" step and jump straight to mapping review with
/// auto-matched columns. Header-level defaults (warehouseCode, procedureCode,
/// lonAuthorizationId, partnerCode) are documented in the bundle result for
/// the caller to PUT via the standard defaults endpoint — we don't prescribe
/// them in the bundle because they're TEKSPORT-specific.
/// </summary>
public sealed record CreateKw12ImportBundleCommand(
    byte[] FileBytes,
    string FileName) : ICommand<Result<Kw12ImportBundleResult>>;

public sealed record Kw12ImportBundleResult(
    Guid? ItemsSessionId,
    Guid? CustomsDeclarationsSessionId,
    Guid? ProductionOrdersSessionId,
    Guid? ReceiptsSessionId,
    List<string> SheetsFound,
    List<string> SheetsSkipped,
    List<string> SuggestedDefaults);

public sealed class CreateKw12ImportBundleCommandHandler
    : ICommandHandler<CreateKw12ImportBundleCommand, Result<Kw12ImportBundleResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IXlsxMultiSheetParser _parser;

    // Sheet → (TargetEntity, display label) mapping. Matches the KW12 workbook
    // layout TEKSPORT sends: 3 tabs "Matriks", "Faktura", "Transport".
    // Case-insensitive; also tolerates Cyrillic variants the user types.
    private static readonly (string[] Aliases, string Target)[] SheetTargets =
    {
        (new[] { "Matriks", "Матрикс" },  "Items"),
        (new[] { "Faktura", "Фактура", "Invoice" }, "CustomsDeclarations"),
        (new[] { "Transport", "Транспорт" }, "Receipts"),
    };

    public CreateKw12ImportBundleCommandHandler(
        IApplicationDbContext context,
        IXlsxMultiSheetParser parser)
    {
        _context = context;
        _parser = parser;
    }

    public async Task<Result<Kw12ImportBundleResult>> Handle(
        CreateKw12ImportBundleCommand request, CancellationToken ct)
    {
        if (request.FileBytes is null || request.FileBytes.Length == 0)
            return Result<Kw12ImportBundleResult>.Failure("Uploaded file is empty.");
        if (string.IsNullOrWhiteSpace(request.FileName))
            return Result<Kw12ImportBundleResult>.Failure("File name is required.");
        if (!request.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            && !request.FileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase))
            return Result<Kw12ImportBundleResult>.Failure("KW12 preset expects a .xlsx or .xlsm workbook.");

        IReadOnlyDictionary<string, ParsedImportFile> sheets;
        try
        {
            using var ms = new MemoryStream(request.FileBytes, writable: false);
            sheets = _parser.ParseAllSheets(ms);
        }
        catch (Exception ex)
        {
            return Result<Kw12ImportBundleResult>.Failure($"Failed to parse workbook: {ex.Message}");
        }

        if (sheets.Count == 0)
            return Result<Kw12ImportBundleResult>.Failure("Workbook has no worksheets.");

        Guid? itemsId = null;
        Guid? customsId = null;
        Guid? receiptsId = null;
        Guid? productionOrdersId = null;
        var found = new List<string>();
        var skipped = new List<string>();

        foreach (var sheetKvp in sheets)
        {
            var match = SheetTargets.FirstOrDefault(
                st => st.Aliases.Any(a => a.Equals(sheetKvp.Key, StringComparison.OrdinalIgnoreCase)));
            if (match.Target is null)
            {
                skipped.Add(sheetKvp.Key);
                continue;
            }
            if (sheetKvp.Value.Headers.Count == 0)
            {
                skipped.Add($"{sheetKvp.Key} (no headers)");
                continue;
            }

            var session = new ImportSession
            {
                Id = Guid.NewGuid(),
                OriginalFileName = $"{request.FileName}#{sheetKvp.Key}",
                SourceFormat = ImportSourceFormat.Xlsx,
                FileSizeBytes = request.FileBytes.Length,
                Status = ImportSessionStatus.Uploaded,
                HeadersJson = JsonSerializer.Serialize(sheetKvp.Value.Headers),
                RowsJson = JsonSerializer.Serialize(sheetKvp.Value.Rows),
                RowCount = sheetKvp.Value.Rows.Count,
                TargetEntity = match.Target,
                PartnerContextId = null
            };
            await _context.ImportSessions.AddAsync(session, ct);

            switch (match.Target)
            {
                case "Items":                itemsId = session.Id; break;
                case "CustomsDeclarations":  customsId = session.Id; break;
                case "Receipts":             receiptsId = session.Id; break;
                case "ProductionOrders":     productionOrdersId = session.Id; break;
            }
            found.Add($"{sheetKvp.Key} → {match.Target} ({sheetKvp.Value.Rows.Count} rows)");
        }

        if (found.Count == 0)
            return Result<Kw12ImportBundleResult>.Failure(
                "None of the sheets matched the KW12 shape (Matriks / Faktura / Transport).");

        await _context.SaveChangesAsync(ct);

        return Result<Kw12ImportBundleResult>.Success(new Kw12ImportBundleResult(
            ItemsSessionId: itemsId,
            CustomsDeclarationsSessionId: customsId,
            ProductionOrdersSessionId: productionOrdersId,
            ReceiptsSessionId: receiptsId,
            SheetsFound: found,
            SheetsSkipped: skipped,
            SuggestedDefaults: new List<string>
            {
                "Items: type=RawMaterial, baseUoMCode=<your main UoM>",
                "CustomsDeclarations: procedureCode=4200, currency=EUR, lonAuthorizationId=<your 26/.../0001>",
                "Receipts: warehouseCode=222, partnerCode=<your supplier>"
            }));
    }
}
