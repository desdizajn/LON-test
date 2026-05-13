using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — Ispratnici (waste destruction certificate) +
/// Proces=9 LagerMaterijali aggregation → CustomsDeclaration(type=Waste) +
/// CustomsDeclarationLine rows per MAPPING.md §6.2 + §11.1.
///
/// In LON, waste is modelled as a CustomsDeclaration with
/// <c>DeclarationType="Waste"</c> (consumed by GetRazdolzuvanjeForClientOrder
/// — §E9 — for credit aggregation). Each line carries the matching IM MRN
/// in <c>PreviousMRN</c> so the Razdolzuvanje query can fold orphan credits
/// in via that join (§E9 §11.1).
///
/// One CustomsDeclaration per (Zaklucok, Ispratnica) header; one line per
/// material item code.
/// </summary>
internal sealed class WasteDeclarationMapper
{
    private readonly MigrationContext _ctx;
    public WasteDeclarationMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[wastes] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var itemByCode = LoadItemsByCode(lon);
        var odMap = OdobrenijaMapper.Lookup(_ctx, legacy, lon);
        if (!_ctx.ProcedureByCode.TryGetValue("WASTE", out var wasteProcId))
        {
            // Fallback to 3151 (export) if WASTE wasn't auto-seeded.
            _ctx.ProcedureByCode.TryGetValue("3151", out wasteProcId);
        }

        // 1) Aggregate Proces=9 LagerMaterijali by (Zaklucok, Ispratnica=DokRBr, Item).
        var sql = $"""
                  SELECT OdobrenieRBr, ZaklucokBroj, DokRBr, ArtKatBrMat,
                         SUM(CAST(Kol AS decimal(18,4))) AS Qty,
                         MIN(EdMerMat) AS EdMer, MIN(FakturaU5Broj) AS SourceFaktura,
                         MIN(LagerDatum) AS Datum
                    FROM LagerMaterijali
                   WHERE Proces = 9 AND ArtKatBrMat IS NOT NULL{_ctx.ZaklucokWhere()}
                   GROUP BY OdobrenieRBr, ZaklucokBroj, DokRBr, ArtKatBrMat
                   ORDER BY OdobrenieRBr, ZaklucokBroj, DokRBr
                  """;
        using var sel = new SqlCommand(sql, legacy);
        _ctx.AddZaklucokParam(sel);
        using var rd = sel.ExecuteReader();

        // Group rows by (Od, Zb, Dok) to emit one declaration with N lines.
        var byHeader = new Dictionary<(int Od, string Zb, int Dok),
            List<(string Code, decimal Qty, string? Ed, string? SourceFaktura, DateTime When)>>();
        while (rd.Read())
        {
            var od = AsInt(rd["OdobrenieRBr"]);
            var zb = AsStringOrEmpty(rd["ZaklucokBroj"]);
            var dok = AsInt(rd["DokRBr"]);
            var code = AsString(rd["ArtKatBrMat"]) ?? "";
            var qty = AsDecimal(rd["Qty"]);
            var ed = AsString(rd["EdMer"]);
            var src = AsString(rd["SourceFaktura"]);
            var when = AsDate(rd["Datum"]) ?? DateTime.UtcNow.Date;
            if (string.IsNullOrWhiteSpace(code) || qty <= 0m) continue;
            var key = (od, zb, dok);
            if (!byHeader.TryGetValue(key, out var list))
                byHeader[key] = list = new();
            list.Add((code, qty, ed, src, when));
        }
        rd.Close();

        Console.WriteLine($"[wastes] {byHeader.Count} waste headers to write");

        // 2) Pull Ispratnica metadata (number + flags).
        var ispRBrs = byHeader.Keys.Select(k => k.Dok).Distinct().ToList();
        var ispMeta = LoadIspratnicaMetadata(legacy, ispRBrs);

        int headersWritten = 0, linesWritten = 0, missingItem = 0;
        foreach (var (key, lines) in byHeader)
        {
            var (od, zb, dok) = key;
            if (!odMap.TryGetValue(od, out var lonAuthId)) lonAuthId = Guid.Empty;
            var clientOrderId = ClientOrderMapper.ResolveId(_ctx, od, zb);
            var declId = DeterministicGuid("WasteDecl", $"{_ctx.TenantId}|{od}|{zb}|{dok}");
            var (ispBroj, ispDate) = ispMeta.TryGetValue(dok, out var m) ? m : (null, lines[0].When);
            var declNumber = !string.IsNullOrWhiteSpace(ispBroj)
                ? $"WASTE-{ispBroj}/{ispDate:yyMMdd}"
                : $"WASTE-LEG-{dok}/{ispDate:yyMMdd}";
            var mrn = $"WASTE-{dok}-{ispDate:yyMMdd}";

            if (!_ctx.DryRun && wasteProcId != Guid.Empty)
            {
                _ctx.Exec(lon,
                    """
                    MERGE CustomsDeclarations AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        DeclarationNumber = @num, MRN = @mrn, DeclarationDate = @date,
                        CustomsProcedureId = @proc, LONAuthorizationId = @auth,
                        ClientOrderId = @co, DeclarationType = 'Waste', ProcedureCode = 'WASTE',
                        Currency = 'EUR', TotalCustomsValue = 0, TotalDuty = 0, TotalVAT = 0,
                        TotalOtherCharges = 0, Status = 3, IsCleared = 1,
                        SpecialRemarks = @notes,
                        ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, DeclarationNumber, MRN,
                        DeclarationDate, CustomsProcedureId, LONAuthorizationId, ClientOrderId,
                        DeclarationType, Currency, TotalInvoiceAmount, ExchangeRate, HasContainer,
                        ProcedureCode, TotalCustomsValue, TotalDuty, TotalVAT, TotalOtherCharges,
                        Status, IsCleared, ClearedDate, SpecialRemarks,
                        CreatedAt, CreatedBy, IsDeleted, AverageDutyRate, UseAverageRate)
                        VALUES (@id, @tenant, @num, @mrn, @date, @proc,
                                @auth, @co, 'Waste', 'EUR', 0, 1, 0, 'WASTE',
                                0, 0, 0, 0, 3, 1, @date, @notes,
                                SYSUTCDATETIME(), 'migration', 0, NULL, 0);
                    """,
                    ("@id", declId),
                    ("@tenant", _ctx.TenantId),
                    ("@num", declNumber),
                    ("@mrn", mrn),
                    ("@date", ispDate),
                    ("@proc", wasteProcId),
                    ("@auth", lonAuthId == Guid.Empty ? (object)DBNull.Value : lonAuthId),
                    ("@co", clientOrderId),
                    ("@notes", $"[LEGACY IspratnicaRBr={dok} IspratnicaBroj={ispBroj}]"));
                headersWritten++;
            }

            int lineIdx = 0;
            foreach (var l in lines)
            {
                lineIdx++;
                if (!itemByCode.TryGetValue(l.Code, out var itemId)) { missingItem++; continue; }
                var uomId = _ctx.UoMByCode.TryGetValue(l.Ed ?? "", out var u) ? u : _ctx.DefaultUoMId;
                var prevMrn = string.IsNullOrEmpty(l.SourceFaktura) ? null : $"LEG-{l.SourceFaktura}";
                var lineId = DeterministicGuid("WasteDeclLine", $"{declId}|{lineIdx}|{l.Code}");

                if (_ctx.DryRun) { linesWritten++; continue; }

                _ctx.Exec(lon,
                    """
                    MERGE CustomsDeclarationLines AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        CustomsDeclarationId = @d, LineNumber = @line, ItemId = @item,
                        Quantity = @q, UoMId = @uom, CustomsValue = 0, DutyAmount = 0,
                        DutyRate = 0, VATRate = 0, VATAmount = 0, OtherCharges = 0,
                        PreviousMRN = @prev, NetWeight = 0, GrossWeight = 0,
                        ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, CustomsDeclarationId, LineNumber,
                        ItemId, Quantity, UoMId, ItemPrice, StatisticalValue, CustomsValue, DutyAmount,
                        DutyRate, VATRate, VATAmount, NetWeight, GrossWeight, OtherCharges,
                        PreviousMRN, CreatedAt, CreatedBy, IsDeleted, RazdolzenaDaNe)
                        VALUES (@id, @tenant, @d, @line, @item, @q, @uom, 0, 0, 0, 0,
                                0, 0, 0, 0, 0, 0, @prev,
                                SYSUTCDATETIME(), 'migration', 0, 0);
                    """,
                    ("@id", lineId),
                    ("@tenant", _ctx.TenantId),
                    ("@d", declId),
                    ("@line", lineIdx),
                    ("@item", itemId),
                    ("@q", l.Qty),
                    ("@uom", uomId),
                    ("@prev", (object?)prevMrn ?? DBNull.Value));
                linesWritten++;
            }
        }

        Console.WriteLine($"[wastes] done headers={headersWritten} lines={linesWritten} missingItem={missingItem}");
        return 0;
    }

    private Dictionary<int, (string? Broj, DateTime Date)> LoadIspratnicaMetadata(SqlConnection legacy, List<int> rbrs)
    {
        var map = new Dictionary<int, (string? Broj, DateTime Date)>();
        if (rbrs.Count == 0) return map;
        var inClause = string.Join(',', rbrs);
        using var cmd = new SqlCommand(
            $"SELECT IspratnicaRBr, IspratnicaBroj, IspratnicaDatum FROM Ispratnici WHERE IspratnicaRBr IN ({inClause})", legacy);
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var rbr = AsInt(rd["IspratnicaRBr"]);
            var broj = AsString(rd["IspratnicaBroj"]);
            var date = AsDate(rd["IspratnicaDatum"]) ?? DateTime.UtcNow.Date;
            map[rbr] = (broj, date);
        }
        return map;
    }

    private Dictionary<string, Guid> LoadItemsByCode(SqlConnection lon)
    {
        using var cmd = new SqlCommand(
            "SELECT Id, Code FROM Items WHERE TenantId=@t", lon);
        cmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        while (r.Read()) map[r.GetString(1)] = r.GetGuid(0);
        return map;
    }
}
