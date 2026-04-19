using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// FakturiU5Z (header) + FakturiU5 (lines) → CustomsDeclarations + CustomsDeclarationLines.
///
/// Mapping notes:
///  - FakturiU5Z rows are the invoice/declaration headers; FakturiU5 rows are individual
///    commodity lines (one per Box 31/32/33 entry on the SAD).
///  - The declaration is keyed in legacy by (OdobrenieRBr, ZaklucokBroj, FakturaU5Broj).
///  - ZaverkaBroj/ZaverkaDatum on the header = customs certification ("заверка"), set when
///    an inspector stamps the declaration. When present, we flag Status=Submitted
///    (closer to legacy semantics than Cleared).
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

        var itemByArtRBr = LoadItemsByArtRBr(lon);
        var authByZakBroj = LoadAuthsByZaklucokBroj(lon);
        Console.WriteLine($"[decls] item map size={itemByArtRBr.Count} auth map size={authByZakBroj.Count}");

        if (_ctx.InwardProcessingProcedureId == null)
        {
            Console.Error.WriteLine("[decls] ABORT: no 'INW-PROC' CustomsProcedure in LON; seed it first.");
            return 3;
        }

        string top = limit > 0 ? $"TOP {limit}" : "";
        var sel = new SqlCommand(
            $"SELECT {top} OdobrenieRBr, ZaklucokBroj, FakturaU5Broj, FakturaU5Datum, " +
            "ZaverkaBroj, ZaverkaDatum, RazdolzenaDaNe, Kurs, Valuta, CarOE, Zabeleska, " +
            "Primac, Ispracac, KoletiBr, TezinaBrutoVk, DatumRokDo, Proizvoditel " +
            "FROM FakturiU5Z ORDER BY FakturaU5Datum, FakturaU5Broj", legacy);

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
                Valuta = AsString(rd["Valuta"]) ?? "EUR",
                CarOE = AsString(rd["CarOE"]),
                Zabeleska = AsString(rd["Zabeleska"]),
                Primac = AsInt(rd["Primac"]),
                Ispracac = AsInt(rd["Ispracac"]),
                KoletiBr = AsDecimal(rd["KoletiBr"]),
                TezinaBruto = AsDecimal(rd["TezinaBrutoVk"]),
                DatumRokDo = AsDate(rd["DatumRokDo"]),
            });
        }
        rd.Close();

        Console.WriteLine($"[decls] loaded {headers.Count} FakturiU5Z headers");

        foreach (var h in headers)
        {
            total++;
            if (string.IsNullOrWhiteSpace(h.FakturaU5Broj)) continue;

            var declId = DeterministicGuid("CustomsDecl",
                $"{_ctx.TenantId}|{h.OdobrenieRBr}|{h.ZaklucokBroj}|{h.FakturaU5Broj}|{h.FakturaU5Datum:yyyyMMdd}");
            authByZakBroj.TryGetValue(h.ZaklucokBroj, out var lonAuthId);
            if (lonAuthId == Guid.Empty) withoutAuth++;

            // Status: Razdolzena=1 → Cleared, ZaverkaBroj present → Submitted, else Registered.
            int status = h.Razdolzena ? 3 /*Cleared*/
                       : !string.IsNullOrWhiteSpace(h.ZaverkaBroj) ? 2 /*Submitted*/
                       : 1 /*Registered*/;

            decimal totalCustomsValue = 0, totalDuty = 0, totalVAT = 0;

            var lines = LoadLines(legacy, h);

            if (!_ctx.DryRun)
            {
                _ctx.Exec(lon,
                    """
                    MERGE CustomsDeclarations AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        DeclarationNumber = @num, MRN = @mrn, DeclarationDate = @date,
                        CustomsProcedureId = @procId, LONAuthorizationId = @authId,
                        DeclarationType = 'IM', ProcedureCode = '4200',
                        Currency = @currency, ExchangeRate = @kurs,
                        TotalCustomsValue = @tcv, TotalDuty = @tdu, TotalVAT = @tvat,
                        TotalOtherCharges = 0, Status = @status, IsCleared = @cleared,
                        DueDate = @due, ClearedDate = @cleared_date,
                        SpecialRemarks = @notes,
                        ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, DeclarationNumber, MRN,
                        DeclarationDate, CustomsProcedureId, LONAuthorizationId, DeclarationType,
                        Currency, TotalInvoiceAmount, ExchangeRate, HasContainer, ProcedureCode,
                        TotalCustomsValue, TotalDuty, TotalVAT, TotalOtherCharges,
                        Status, IsCleared, DueDate, ClearedDate, SpecialRemarks,
                        CreatedAt, CreatedBy, IsDeleted)
                        VALUES (@id, @tenant, @num, @mrn, @date, @procId, @authId, 'IM',
                                @currency, 0, @kurs, 0, '4200',
                                @tcv, @tdu, @tvat, 0, @status, @cleared, @due, @cleared_date,
                                @notes, SYSUTCDATETIME(), 'migration', 0);
                    """,
                    ("@id", declId),
                    ("@tenant", _ctx.TenantId),
                    // legacy FakturaU5Broj is NOT unique across time/auth; compose a stable key
                    ("@num", $"{h.FakturaU5Broj}/{h.FakturaU5Datum:yyMMdd}/{h.OdobrenieRBr}"),
                    ("@mrn", (h.ZaverkaBroj is { } zb && zb.Length > 0 ? zb : $"LEG-{h.FakturaU5Broj}-{h.FakturaU5Datum:yyMMdd}-{h.OdobrenieRBr}")),
                    ("@date", h.FakturaU5Datum),
                    ("@procId", _ctx.InwardProcessingProcedureId!.Value),
                    ("@authId", (object?)(lonAuthId == Guid.Empty ? null : (Guid?)lonAuthId) ?? DBNull.Value),
                    ("@currency", h.Valuta),
                    ("@kurs", h.Kurs),
                    ("@tcv", totalCustomsValue),
                    ("@tdu", totalDuty),
                    ("@tvat", totalVAT),
                    ("@status", status),
                    ("@cleared", status == 3),
                    ("@due", (object?)h.DatumRokDo ?? DBNull.Value),
                    ("@cleared_date", (object?)h.ZaverkaDatum ?? DBNull.Value),
                    ("@notes", (object?)$"[LEGACY OdobrenieRBr={h.OdobrenieRBr} ZaklucokBroj={h.ZaklucokBroj}] {h.Zabeleska}".Trim()));
            }

            // Lines
            int lineNo = 0;
            foreach (var ln in lines)
            {
                lineNo++;
                if (!itemByArtRBr.TryGetValue(ln.ArtRBrMat, out var itemId))
                {
                    // skip lines for items we didn't migrate (archived/missing)
                    continue;
                }

                var lid = DeterministicGuid("CustomsDeclLine", $"{declId}|{lineNo}|{ln.ArtRBrMat}");

                decimal dutyAmt = ln.Davacki;
                decimal cv = ln.Vrednost;
                totalCustomsValue += cv;
                totalDuty += dutyAmt;

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
                        NetWeight, GrossWeight, OtherCharges, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (@id, @tenant, @d, @line, @item, @tar, @country, @q, @uom, @price,
                                @sv, @cv, @duty, 0, 0, 0, @net, @gross, 0, SYSUTCDATETIME(), 'migration', 0);
                    """,
                    ("@id", lid),
                    ("@tenant", _ctx.TenantId),
                    ("@d", declId),
                    ("@line", lineNo),
                    ("@item", itemId),
                    ("@tar", (object?)ln.TarBr ?? DBNull.Value),
                    ("@country", (object?)ln.ZemjaPoteklo ?? DBNull.Value),
                    ("@q", ln.Kol),
                    ("@uom", _ctx.DefaultUoMId),
                    ("@price", ln.Cena),
                    ("@sv", ln.StatVred),
                    ("@cv", ln.Vrednost),
                    ("@duty", dutyAmt),
                    ("@net", ln.Tezina),
                    ("@gross", ln.TezinaBruto));
            }

            // Update totals on declaration now that we know the sum
            if (!_ctx.DryRun && lineNo > 0)
            {
                _ctx.Exec(lon,
                    "UPDATE CustomsDeclarations SET TotalCustomsValue=@tcv, TotalDuty=@tdu WHERE Id=@id",
                    ("@tcv", totalCustomsValue),
                    ("@tdu", totalDuty),
                    ("@id", declId));
            }

            written++;
            if (total % 50 == 0) Console.WriteLine($"[decls] progress total={total} written={written} noauth={withoutAuth}");
        }

        Console.WriteLine($"[decls] done total={total} written={written} headers_without_matched_auth={withoutAuth}");
        return 0;
    }

    private record struct LineRow(int ArtRBrMat, decimal Kol, decimal Cena, string? Valuta,
        decimal Vrednost, decimal StatVred, decimal Davacki, decimal Tezina, decimal TezinaBruto,
        string? TarBr, string? ZemjaPoteklo);

    private static List<LineRow> LoadLines(SqlConnection legacy, HeaderRow h)
    {
        using var cmd = new SqlCommand(
            "SELECT ArtRBrMat, Kol, Cena, Valuta, Vrednost, StatVred, Davacki, Tezina, TezinaBruto, TarBr, ZemjaPoteklo " +
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
                AsInt(r["ArtRBrMat"]),
                AsDecimal(r["Kol"]),
                AsDecimal(r["Cena"]),
                AsString(r["Valuta"]),
                AsDecimal(r["Vrednost"]),
                AsDecimal(r["StatVred"]),
                AsDecimal(r["Davacki"]),
                AsDecimal(r["Tezina"]),
                AsDecimal(r["TezinaBruto"]),
                AsString(r["TarBr"]),
                AsString(r["ZemjaPoteklo"])));
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
    }

    private Dictionary<int, Guid> LoadItemsByArtRBr(SqlConnection lon)
    {
        // We embedded [LEGACY ArtRBr=N] in the description.
        using var cmd = new SqlCommand(
            "SELECT Id, Description FROM Items WHERE TenantId=@t AND Description LIKE '[[]LEGACY ArtRBr=%'",
            lon);
        cmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<int, Guid>();
        while (r.Read())
        {
            var id = r.GetGuid(0);
            var d = r.GetString(1);
            // parse: [LEGACY ArtRBr=N] ...
            int eq = d.IndexOf('=');
            int end = d.IndexOf(']', eq);
            if (eq > 0 && end > eq && int.TryParse(d.AsSpan(eq + 1, end - eq - 1), out var rbr))
                map[rbr] = id;
        }
        return map;
    }

    private Dictionary<string, Guid> LoadAuthsByZaklucokBroj(SqlConnection lon)
    {
        using var cmd = new SqlCommand(
            "SELECT Id, AuthorizationNumber FROM LONAuthorizations WHERE TenantId=@t AND IsDeleted=0",
            lon);
        cmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        while (r.Read())
        {
            var id = r.GetGuid(0);
            var num = r.GetString(1);
            map[num] = id;
        }
        return map;
    }
}
