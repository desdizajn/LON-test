using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// FakturiU5Z (header) + FakturiU5 (lines) → CustomsDeclarations + Lines.
///
/// Phase 17 §E.MIGRATE refactor:
///   * Procedure code resolution: VidUIS legacy code → 4200 (default for
///     inward processing import; ALL local TEKSPORT FakturiU5Z rows fall
///     into this bucket). Phase 21 expands the mapping table per real data.
///   * ClientOrderId: stamped via composite (OdobrenieRBr, ZaklucokBroj)
///     lookup so the hub query reaches this declaration without joins.
///   * Status: RazdolzenaDaNe=1 → Cleared; ZaverkaBroj non-empty → Submitted;
///     else Registered.
///   * Totals: server-side recomputation from line aggregation.
/// </summary>
internal sealed class DeclarationMapper
{
    private readonly MigrationContext _ctx;
    public DeclarationMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run(int limit)
    {
        Console.WriteLine("[decls] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var itemByCode = LoadItemsByCode(lon);
        var odMap = OdobrenijaMapper.Lookup(_ctx, legacy, lon);
        Console.WriteLine($"[decls] item map size={itemByCode.Count} auth map size={odMap.Count}");

        if (!_ctx.ProcedureByCode.TryGetValue("4200", out var procDefault))
        {
            Console.Error.WriteLine("[decls] ABORT: '4200' CustomsProcedure not found after Hydrate; check seed.");
            return 3;
        }

        string top = limit > 0 ? $"TOP {limit}" : "";
        var sql = $"""
                  SELECT {top} OdobrenieRBr, ZaklucokBroj, FakturaU5Broj, FakturaU5Datum,
                         ZaverkaBroj, ZaverkaDatum, RazdolzenaDaNe, Kurs, Valuta, CarOE,
                         Zabeleska, Primac, Ispracac, KoletiBr, TezinaBrutoVk, DatumRokDo,
                         Proizvoditel, VidUIS
                    FROM FakturiU5Z
                   WHERE 1=1{_ctx.ZaklucokWhere()}
                   ORDER BY FakturaU5Datum, FakturaU5Broj
                  """;
        using var sel = new SqlCommand(sql, legacy);
        _ctx.AddZaklucokParam(sel);
        using var rd = sel.ExecuteReader();

        int total = 0, written = 0, withoutAuth = 0;
        var headers = new List<HeaderRow>();
        while (rd.Read())
        {
            headers.Add(new HeaderRow
            {
                OdobrenieRBr = AsInt(rd["OdobrenieRBr"]),
                ZaklucokBroj = AsStringOrEmpty(rd["ZaklucokBroj"]),
                FakturaU5Broj = AsStringOrEmpty(rd["FakturaU5Broj"]),
                FakturaU5Datum = AsDateOrNow(rd["FakturaU5Datum"]),
                ZaverkaBroj = AsString(rd["ZaverkaBroj"]),
                ZaverkaDatum = AsDate(rd["ZaverkaDatum"]),
                Razdolzena = AsBool(rd["RazdolzenaDaNe"]),
                Kurs = AsDecimal(rd["Kurs"]),
                Valuta = string.IsNullOrWhiteSpace(AsString(rd["Valuta"])) ? "EUR" : AsString(rd["Valuta"])!,
                CarOE = AsString(rd["CarOE"]),
                Zabeleska = AsString(rd["Zabeleska"]),
                Primac = AsInt(rd["Primac"]),
                Ispracac = AsInt(rd["Ispracac"]),
                KoletiBr = AsDecimal(rd["KoletiBr"]),
                TezinaBruto = AsDecimal(rd["TezinaBrutoVk"]),
                DatumRokDo = AsDate(rd["DatumRokDo"]),
                Proizvoditel = AsInt(rd["Proizvoditel"]),
                VidUIS = AsString(rd["VidUIS"]),
            });
        }
        rd.Close();

        // Phantom headers: FakturiU5 has lines whose (OdobrenieRBr, ZaklucokBroj,
        // FakturaU5Broj) combination isn't represented in FakturiU5Z. Legacy data
        // quirk — most likely an archived header. We synthesize a placeholder
        // header per orphan FakturaU5Broj so the lines still land in LON and the
        // R6 NaimU5 aggregation matches legacy totals.
        var existingKeys = new HashSet<(int, string, string)>(
            headers.Select(h => (h.OdobrenieRBr, h.ZaklucokBroj, h.FakturaU5Broj)));
        var phantomSql = $"""
                         SELECT OdobrenieRBr, ZaklucokBroj, FakturaU5Broj,
                                MIN(FakturaU5Datum) AS Datum, MIN(Valuta) AS Valuta
                           FROM FakturiU5
                          WHERE FakturaU5Broj IS NOT NULL{_ctx.ZaklucokWhere()}
                          GROUP BY OdobrenieRBr, ZaklucokBroj, FakturaU5Broj
                         """;
        using (var phantomCmd = new SqlCommand(phantomSql, legacy))
        {
            _ctx.AddZaklucokParam(phantomCmd);
            using var pr = phantomCmd.ExecuteReader();
            while (pr.Read())
            {
                var k = (AsInt(pr["OdobrenieRBr"]), AsStringOrEmpty(pr["ZaklucokBroj"]),
                         AsStringOrEmpty(pr["FakturaU5Broj"]));
                if (existingKeys.Contains(k)) continue;
                headers.Add(new HeaderRow
                {
                    OdobrenieRBr = k.Item1,
                    ZaklucokBroj = k.Item2,
                    FakturaU5Broj = k.Item3,
                    FakturaU5Datum = AsDateOrNow(pr["Datum"]),
                    Valuta = string.IsNullOrWhiteSpace(AsString(pr["Valuta"])) ? "EUR" : AsString(pr["Valuta"])!,
                    Razdolzena = false,
                    VidUIS = "(phantom-no-header)",
                    Zabeleska = "(synthesised — no FakturiU5Z header for this FakturaU5Broj)",
                });
            }
        }

        Console.WriteLine($"[decls] loaded {headers.Count} headers (incl. phantoms for orphan FakturiU5 lines)");

        foreach (var h in headers)
        {
            total++;
            if (string.IsNullOrWhiteSpace(h.FakturaU5Broj)) continue;

            var declId = DeterministicGuid("CustomsDecl",
                $"{_ctx.TenantId}|{h.OdobrenieRBr}|{h.ZaklucokBroj}|{h.FakturaU5Broj}|{h.FakturaU5Datum:yyyyMMdd}");

            if (!odMap.TryGetValue(h.OdobrenieRBr, out var lonAuthId))
                withoutAuth++;
            var clientOrderId = ClientOrderMapper.ResolveId(_ctx, h.OdobrenieRBr, h.ZaklucokBroj);

            // Status: Razdolzena=1 → Cleared (3), ZaverkaBroj present → Submitted (2), else Registered (1).
            int status = h.Razdolzena ? 3
                       : !string.IsNullOrWhiteSpace(h.ZaverkaBroj) ? 2
                       : 1;

            // Procedure resolution from VidUIS. Local data has IMA4/IMA5/IMC5
            // all of which are legacy aliases for inward processing → 4200.
            // Phase 21 expands this map as we see prod data.
            var procId = procDefault;

            // Declaration type from procedure context (IM for the legacy IM bucket;
            // EX rows aren't present in local TEKSPORT slice's FakturiU5Z).
            string declType = "IM";

            var lines = LoadLines(legacy, h);
            decimal totalCV = 0, totalDuty = 0;

            Guid? customerId = h.Primac > 0
                ? DeterministicGuid("Partner", $"{_ctx.TenantId}|LEG-FIRM-{h.Primac}")
                : _ctx.DefaultSupplierPartnerId;

            if (!_ctx.DryRun)
            {
                _ctx.Exec(lon,
                    """
                    MERGE CustomsDeclarations AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        DeclarationNumber = @num, MRN = @mrn, DeclarationDate = @date,
                        CustomsProcedureId = @procId, LONAuthorizationId = @authId,
                        ClientOrderId = @coId, PartnerId = @partner,
                        DeclarationType = @declType, ProcedureCode = @procCode,
                        Currency = @currency, ExchangeRate = @kurs,
                        TotalCustomsValue = @tcv, TotalDuty = @tdu, TotalVAT = 0,
                        TotalOtherCharges = 0, Status = @status, IsCleared = @cleared,
                        DueDate = @due, ClearedDate = @cleared_date,
                        SpecialRemarks = @notes,
                        ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, DeclarationNumber, MRN,
                        DeclarationDate, CustomsProcedureId, LONAuthorizationId, ClientOrderId,
                        PartnerId, DeclarationType, Currency, TotalInvoiceAmount, ExchangeRate,
                        HasContainer, ProcedureCode, TotalCustomsValue, TotalDuty, TotalVAT,
                        TotalOtherCharges, Status, IsCleared, DueDate, ClearedDate,
                        SpecialRemarks, CreatedAt, CreatedBy, IsDeleted, AverageDutyRate, UseAverageRate)
                        VALUES (@id, @tenant, @num, @mrn, @date, @procId, @authId, @coId,
                                @partner, @declType, @currency, 0, @kurs, 0, @procCode,
                                @tcv, @tdu, 0, 0, @status, @cleared, @due, @cleared_date,
                                @notes, SYSUTCDATETIME(), 'migration', 0, NULL, 0);
                    """,
                    ("@id", declId),
                    ("@tenant", _ctx.TenantId),
                    ("@num", $"{h.FakturaU5Broj}/{h.FakturaU5Datum:yyMMdd}/{h.OdobrenieRBr}"),
                    ("@mrn", (h.ZaverkaBroj is { } zb && zb.Length > 0
                              ? zb : $"LEG-{h.FakturaU5Broj}-{h.FakturaU5Datum:yyMMdd}-{h.OdobrenieRBr}")),
                    ("@date", h.FakturaU5Datum),
                    ("@procId", procId),
                    ("@authId", (object?)(lonAuthId == Guid.Empty ? null : (Guid?)lonAuthId) ?? DBNull.Value),
                    ("@coId", clientOrderId),
                    ("@partner", customerId ?? (object)DBNull.Value),
                    ("@declType", declType),
                    ("@procCode", "4200"),
                    ("@currency", h.Valuta),
                    ("@kurs", h.Kurs),
                    ("@tcv", 0m),
                    ("@tdu", 0m),
                    ("@status", status),
                    ("@cleared", status == 3),
                    ("@due", (object?)h.DatumRokDo ?? DBNull.Value),
                    ("@cleared_date", (object?)h.ZaverkaDatum ?? DBNull.Value),
                    ("@notes", $"[LEGACY VidUIS={h.VidUIS} OdobrenieRBr={h.OdobrenieRBr} ZaklucokBroj={h.ZaklucokBroj}] {h.Zabeleska}".Trim()));
            }

            int lineNo = 0;
            foreach (var ln in lines)
            {
                lineNo++;
                if (!itemByCode.TryGetValue(ln.ArtKatBrMat, out var itemId))
                {
                    Console.WriteLine($"[decls]   skip line {lineNo} item code '{ln.ArtKatBrMat}' not in Items table");
                    continue;
                }

                var lid = DeterministicGuid("CustomsDeclLine", $"{declId}|{lineNo}|{ln.ArtKatBrMat}");
                totalCV += ln.Vrednost;
                totalDuty += ln.Davacki;
                var uomId = ResolveUoM(ln.EdMer);

                if (_ctx.DryRun) continue;

                _ctx.Exec(lon,
                    """
                    MERGE CustomsDeclarationLines AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        CustomsDeclarationId = @d, LineNumber = @line, ItemId = @item,
                        TariffCode = @tar, CountryOfOrigin = @country, Quantity = @q,
                        UoMId = @uom, ItemPrice = @price, StatisticalValue = @sv,
                        CustomsValue = @cv, DutyAmount = @duty, DutyRate = 0, VATRate = 0, VATAmount = 0,
                        NetWeight = @net, GrossWeight = @gross, OtherCharges = 0,
                        ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, CustomsDeclarationId, LineNumber,
                        ItemId, TariffCode, CountryOfOrigin, Quantity, UoMId, ItemPrice,
                        StatisticalValue, CustomsValue, DutyAmount, DutyRate, VATRate, VATAmount,
                        NetWeight, GrossWeight, OtherCharges, CreatedAt, CreatedBy, IsDeleted, RazdolzenaDaNe)
                        VALUES (@id, @tenant, @d, @line, @item, @tar, @country, @q, @uom, @price,
                                @sv, @cv, @duty, 0, 0, 0, @net, @gross, 0, SYSUTCDATETIME(), 'migration', 0, 0);
                    """,
                    ("@id", lid),
                    ("@tenant", _ctx.TenantId),
                    ("@d", declId),
                    ("@line", lineNo),
                    ("@item", itemId),
                    ("@tar", (object?)ln.TarBr ?? DBNull.Value),
                    ("@country", (object?)ln.ZemjaPoteklo ?? DBNull.Value),
                    ("@q", ln.Kol),
                    ("@uom", uomId),
                    ("@price", ln.Cena),
                    ("@sv", ln.StatVred),
                    ("@cv", ln.Vrednost),
                    ("@duty", ln.Davacki),
                    ("@net", ln.Tezina),
                    ("@gross", ln.TezinaBruto));
            }

            if (!_ctx.DryRun && lineNo > 0)
            {
                _ctx.Exec(lon,
                    "UPDATE CustomsDeclarations SET TotalCustomsValue=@tcv, TotalDuty=@tdu WHERE Id=@id",
                    ("@tcv", totalCV),
                    ("@tdu", totalDuty),
                    ("@id", declId));
            }

            written++;
        }

        Console.WriteLine($"[decls] done total={total} written={written} headers_without_matched_auth={withoutAuth}");
        return 0;
    }

    private Guid ResolveUoM(string? edMer)
    {
        if (string.IsNullOrWhiteSpace(edMer)) return _ctx.DefaultUoMId;
        return _ctx.UoMByCode.TryGetValue(edMer.Trim(), out var id) ? id : _ctx.DefaultUoMId;
    }

    private record struct LineRow(string ArtKatBrMat, decimal Kol, decimal Cena, string? Valuta,
        decimal Vrednost, decimal StatVred, decimal Davacki, decimal Tezina, decimal TezinaBruto,
        string? TarBr, string? ZemjaPoteklo, string? EdMer);

    private List<LineRow> LoadLines(SqlConnection legacy, HeaderRow h)
    {
        using var cmd = new SqlCommand(
            "SELECT ArtKatBrMat, Kol, Cena, Valuta, Vrednost, StatVred, Davacki, Tezina, TezinaBruto, TarBr, ZemjaPoteklo, EdMer " +
            "FROM FakturiU5 WHERE OdobrenieRBr=@o AND ZaklucokBroj=@z AND FakturaU5Broj=@f AND FakturaU5Datum=@d",
            legacy);
        cmd.Parameters.AddWithValue("@o", h.OdobrenieRBr);
        cmd.Parameters.AddWithValue("@z", (object?)h.ZaklucokBroj ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@f", h.FakturaU5Broj);
        cmd.Parameters.AddWithValue("@d", h.FakturaU5Datum);
        using var r = cmd.ExecuteReader();
        var list = new List<LineRow>();
        while (r.Read())
            list.Add(new LineRow(
                AsStringOrEmpty(r["ArtKatBrMat"]),
                AsDecimal(r["Kol"]),
                AsDecimal(r["Cena"]),
                AsString(r["Valuta"]),
                AsDecimal(r["Vrednost"]),
                AsDecimal(r["StatVred"]),
                AsDecimal(r["Davacki"]),
                AsDecimal(r["Tezina"]),
                AsDecimal(r["TezinaBruto"]),
                AsString(r["TarBr"]),
                AsString(r["ZemjaPoteklo"]),
                AsString(r["EdMer"])));
        return list;
    }

    private sealed class HeaderRow
    {
        public int OdobrenieRBr;
        public string ZaklucokBroj = "";
        public string FakturaU5Broj = "";
        public DateTime FakturaU5Datum;
        public string? ZaverkaBroj;
        public DateTime? ZaverkaDatum;
        public bool Razdolzena;
        public decimal Kurs;
        public string Valuta = "EUR";
        public string? CarOE;
        public string? Zabeleska;
        public int Primac;
        public int Ispracac;
        public decimal KoletiBr;
        public decimal TezinaBruto;
        public DateTime? DatumRokDo;
        public int Proizvoditel;
        public string? VidUIS;
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
