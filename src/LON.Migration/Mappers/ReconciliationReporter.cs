using Microsoft.Data.SqlClient;
using System.Text;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — six reconciliation queries per MAPPING.md §10.
/// Emits PASS/FAIL lines suitable for CI grep, plus the legacy
/// migration_reconciliation.html artefact for human review.
/// </summary>
internal sealed class ReconciliationReporter
{
    private const decimal QtyTolerancePct = 0.0001m;  // R1: 0.01%
    private const decimal EurTolerance = 0.01m;       // R3, R6

    private readonly MigrationContext _ctx;
    public ReconciliationReporter(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[reconcile] starting" +
            (string.IsNullOrEmpty(_ctx.ZaklucokFilter) ? "" : $" (scope: Zaklucok={_ctx.ZaklucokFilter})"));
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var rows = new List<ReconRow>();
        rows.Add(R1_InventoryByProces(legacy, lon));
        rows.Add(R2_GuaranteePerAuth(legacy, lon));
        rows.Add(R3_DeclarationTotals(legacy, lon));
        rows.Add(R4_ClientOrderCount(legacy, lon));
        rows.Add(R5_BomLineCount(legacy, lon));
        rows.Add(R6_NaimAggregate(legacy, lon));

        int passes = 0, fails = 0;
        foreach (var r in rows)
        {
            var verdict = r.Pass ? "PASS" : "FAIL";
            Console.WriteLine($"[reconcile] {verdict}  {r.Id}  {r.Label}");
            foreach (var detail in r.Details)
                Console.WriteLine($"[reconcile]        {detail}");
            if (r.Pass) passes++; else fails++;
        }

        WriteHtml(rows);
        Console.WriteLine($"[reconcile] done passes={passes} fails={fails}");
        return fails > 0 ? 5 : 0;
    }

    // ───────── R1 — Inventory by Proces / MovementType ─────────

    private ReconRow R1_InventoryByProces(SqlConnection legacy, SqlConnection lon)
    {
        var details = new List<string>();
        bool ok = true;

        // Legacy: per-Proces counts + qty
        var leg = new Dictionary<int, (long Count, decimal Qty)>();
        var legSql = $"""
                     SELECT Proces, COUNT(*) AS Cnt, SUM(CAST(Kol AS decimal(18,4))) AS Qty
                       FROM LagerMaterijali
                      WHERE ArtKatBrMat IS NOT NULL{_ctx.ZaklucokWhere()}
                      GROUP BY Proces
                     """;
        using (var c = new SqlCommand(legSql, legacy))
        {
            _ctx.AddZaklucokParam(c);
            using var r = c.ExecuteReader();
            while (r.Read())
                leg[Convert.ToInt32(r["Proces"])] = (Convert.ToInt64(r["Cnt"]), Convert.ToDecimal(r["Qty"]));
        }

        // LON: per MovementType counts + qty
        var lonByType = new Dictionary<int, (long Count, decimal Qty)>();
        string lzwhere = string.IsNullOrEmpty(_ctx.ZaklucokFilter) ? string.Empty : " AND BatchNumber = @zb";
        using (var c = new SqlCommand(
            $"SELECT [Type], COUNT(*) AS Cnt, SUM(Quantity) AS Qty FROM InventoryMovements WHERE TenantId=@t AND IsDeleted=0{lzwhere} GROUP BY [Type]", lon))
        {
            c.Parameters.AddWithValue("@t", _ctx.TenantId);
            _ctx.AddZaklucokParam(c);
            using var r = c.ExecuteReader();
            while (r.Read())
                lonByType[Convert.ToInt32(r["Type"])] = (Convert.ToInt64(r["Cnt"]), Convert.ToDecimal(r["Qty"]));
        }

        // Mapping legacy Proces → LON Type (must mirror InventoryMapper).
        var procToType = new Dictionary<int, int>
        {
            { 1, 1 }, // Receipt
            { 6, 4 }, // Adjustment
            { 7, 6 }, // ProductionIssue
            { 8, 8 }, // Return
            { 9, 7 }, // Shipment
        };

        foreach (var (proces, target) in procToType)
        {
            leg.TryGetValue(proces, out var l);
            lonByType.TryGetValue(target, out var n);
            if (l.Count == 0 && n.Count == 0) continue;
            details.Add($"Proces={proces} → Type={target}: legacy cnt={l.Count} qty={l.Qty:F4} | lon cnt={n.Count} qty={n.Qty:F4}");
            if (l.Count != n.Count) ok = false;
            if (Math.Abs(l.Qty - n.Qty) > Math.Max(0.01m, Math.Abs(l.Qty) * QtyTolerancePct)) ok = false;
        }

        return new ReconRow("R1", "Inventory by Proces ↔ MovementType", ok, details);
    }

    // ───────── R2 — Guarantee per LONAuthorization ─────────

    private ReconRow R2_GuaranteePerAuth(SqlConnection legacy, SqlConnection lon)
    {
        var details = new List<string>();
        bool ok = true;

        // Legacy: Odobrenija.GarancijaIznos (filtered to the parent auth if Zaklucok scope set)
        var sql = "SELECT OdobrenieRBr, GarancijaIznos FROM Odobrenija WHERE GarancijaIznos > 0";
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            sql += " AND OdobrenieRBr IN (SELECT DISTINCT OdobrenieRBr FROM Zaklucoci WHERE ZaklucokBroj=@zb)";
        var legAuths = new List<(int Rbr, decimal Amount)>();
        using (var c = new SqlCommand(sql, legacy))
        {
            _ctx.AddZaklucokParam(c);
            using var r = c.ExecuteReader();
            while (r.Read())
                legAuths.Add((Convert.ToInt32(r["OdobrenieRBr"]), Convert.ToDecimal(r["GarancijaIznos"])));
        }
        if (legAuths.Count == 0)
        {
            details.Add("(no Odobrenija with GarancijaIznos > 0 in scope)");
            return new ReconRow("R2", "Guarantee per Authorization", true, details);
        }

        foreach (var (rbr, amt) in legAuths)
        {
            // `[` is a wildcard in T-SQL LIKE — escape it so we match the
            // literal `[LEGACY OdobrenieRBr=N]` marker stamped by
            // OdobrenijaMapper, not "any string containing any of those chars".
            // Include soft-deleted rows — legacy Arhivirano=1 sets LON
            // IsDeleted, but reconciliation compares against the full audit
            // trail regardless of archive state.
            using var c = new SqlCommand(
                "SELECT GuaranteeAmount FROM LONAuthorizations WHERE TenantId=@t AND Notes LIKE @marker ESCAPE '!'", lon);
            c.Parameters.AddWithValue("@t", _ctx.TenantId);
            c.Parameters.AddWithValue("@marker", $"%![LEGACY OdobrenieRBr={rbr}]%");
            var got = c.ExecuteScalar();
            decimal mig = (got == null || got is DBNull) ? 0m : Convert.ToDecimal(got);
            details.Add($"OdobrenieRBr={rbr}: legacy={amt:F2} mig={mig:F2}");
            if (Math.Abs(amt - mig) > EurTolerance) ok = false;
        }
        return new ReconRow("R2", "Guarantee per Authorization", ok, details);
    }

    // ───────── R3 — Declaration totals (10-sample spot-check) ─────────

    private ReconRow R3_DeclarationTotals(SqlConnection legacy, SqlConnection lon)
    {
        var details = new List<string>();
        bool ok = true;

        // Pick a sample of FakturiU5Z keys (scoped if filter present).
        var sampleSql = $"""
                        SELECT TOP 10 OdobrenieRBr, ZaklucokBroj, FakturaU5Broj, FakturaU5Datum
                          FROM FakturiU5Z
                         WHERE 1=1{_ctx.ZaklucokWhere()}
                         ORDER BY NEWID()
                        """;
        var samples = new List<(int Od, string Zb, string F, DateTime D)>();
        using (var c = new SqlCommand(sampleSql, legacy))
        {
            _ctx.AddZaklucokParam(c);
            using var r = c.ExecuteReader();
            while (r.Read())
                samples.Add((
                    Convert.ToInt32(r["OdobrenieRBr"]),
                    Convert.ToString(r["ZaklucokBroj"]) ?? "",
                    Convert.ToString(r["FakturaU5Broj"]) ?? "",
                    Convert.ToDateTime(r["FakturaU5Datum"])));
        }

        if (samples.Count == 0)
        {
            details.Add("(no declarations in scope)");
            return new ReconRow("R3", "Declaration totals (spot-check)", true, details);
        }

        foreach (var s in samples)
        {
            decimal legCV = 0, legDuty = 0;
            using (var c = new SqlCommand(
                "SELECT SUM(Vrednost) AS V, SUM(Davacki) AS D FROM FakturiU5 WHERE OdobrenieRBr=@o AND ZaklucokBroj=@z AND FakturaU5Broj=@f AND FakturaU5Datum=@d", legacy))
            {
                c.Parameters.AddWithValue("@o", s.Od);
                c.Parameters.AddWithValue("@z", s.Zb);
                c.Parameters.AddWithValue("@f", s.F);
                c.Parameters.AddWithValue("@d", s.D);
                using var r = c.ExecuteReader();
                if (r.Read())
                {
                    legCV = r["V"] is DBNull ? 0m : Convert.ToDecimal(r["V"]);
                    legDuty = r["D"] is DBNull ? 0m : Convert.ToDecimal(r["D"]);
                }
            }

            decimal lonCV = 0, lonDuty = 0;
            var declNum = $"{s.F}/{s.D:yyMMdd}/{s.Od}";
            using (var c = new SqlCommand(
                "SELECT TotalCustomsValue, TotalDuty FROM CustomsDeclarations WHERE TenantId=@t AND DeclarationNumber=@n AND IsDeleted=0", lon))
            {
                c.Parameters.AddWithValue("@t", _ctx.TenantId);
                c.Parameters.AddWithValue("@n", declNum);
                using var r = c.ExecuteReader();
                if (r.Read())
                {
                    lonCV = Convert.ToDecimal(r["TotalCustomsValue"]);
                    lonDuty = Convert.ToDecimal(r["TotalDuty"]);
                }
            }

            details.Add($"O{s.Od}-Z{s.Zb}-F{s.F}: legacy CV={legCV:F2} Duty={legDuty:F2} | lon CV={lonCV:F2} Duty={lonDuty:F2}");
            if (Math.Abs(legCV - lonCV) > EurTolerance) ok = false;
            if (Math.Abs(legDuty - lonDuty) > EurTolerance) ok = false;
        }

        return new ReconRow("R3", "Declaration totals (spot-check)", ok, details);
    }

    // ───────── R4 — ClientOrder count ─────────

    private ReconRow R4_ClientOrderCount(SqlConnection legacy, SqlConnection lon)
    {
        long legacyCount;
        using (var c = new SqlCommand(
            $"SELECT COUNT(*) FROM Zaklucoci WHERE ZaklucokBroj IS NOT NULL AND ZaklucokBroj <> '00000'{_ctx.ZaklucokWhere()}",
            legacy))
        {
            _ctx.AddZaklucokParam(c);
            legacyCount = Convert.ToInt64(c.ExecuteScalar() ?? 0L);
        }

        // LON: count by tenant; if scope, filter by notes marker. ESCAPE '!'
        // because the marker `[LEGACY ... ZaklucokBroj=N]` contains `[` which
        // T-SQL treats as a wildcard.
        string lonSql = "SELECT COUNT(*) FROM ClientOrders WHERE TenantId=@t AND IsDeleted=0";
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            lonSql += " AND Notes LIKE @marker ESCAPE '!'";
        using var c2 = new SqlCommand(lonSql, lon);
        c2.Parameters.AddWithValue("@t", _ctx.TenantId);
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            c2.Parameters.AddWithValue("@marker", $"%ZaklucokBroj={_ctx.ZaklucokFilter}!]%");
        long lonCount = Convert.ToInt64(c2.ExecuteScalar() ?? 0L);
        bool ok = legacyCount == lonCount;
        return new ReconRow("R4", "ClientOrder count", ok,
            new List<string> { $"legacy={legacyCount} lon={lonCount}" });
    }

    // ───────── R5 — BOMLine count (LON ≤ legacy after dedupe) ─────────

    private ReconRow R5_BomLineCount(SqlConnection legacy, SqlConnection lon)
    {
        long legacyCount;
        using (var c = new SqlCommand($"SELECT COUNT(*) FROM Normativi WHERE 1=1{_ctx.ZaklucokWhere()}", legacy))
        {
            _ctx.AddZaklucokParam(c);
            legacyCount = Convert.ToInt64(c.ExecuteScalar() ?? 0L);
        }

        // LON BOM lines belong to BOMs we created — filter by Code prefix to scope to our migrator.
        string lonSql = """
                        SELECT COUNT(*) FROM BOMLines bl
                          JOIN BOMs b ON b.Id = bl.BOMId
                         WHERE bl.TenantId=@t AND bl.IsDeleted=0 AND b.Code LIKE 'BOM-O%'
                        """;
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            lonSql += " AND b.Code LIKE @prefix";
        using var c2 = new SqlCommand(lonSql, lon);
        c2.Parameters.AddWithValue("@t", _ctx.TenantId);
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            c2.Parameters.AddWithValue("@prefix", $"BOM-O%-Z{_ctx.ZaklucokFilter}-%");
        long lonCount = Convert.ToInt64(c2.ExecuteScalar() ?? 0L);
        bool ok = lonCount <= legacyCount && lonCount > 0;
        long collapsed = legacyCount - lonCount;
        return new ReconRow("R5", "BOMLine count (≤ legacy after dedupe)", ok,
            new List<string> { $"legacy Normativi={legacyCount} | lon BOMLines={lonCount} | collapsed={collapsed}" });
    }

    // ───────── R6 — NaimU5 aggregate re-derived from LON ─────────

    private ReconRow R6_NaimAggregate(SqlConnection legacy, SqlConnection lon)
    {
        var details = new List<string>();
        bool ok = true;
        // Legacy: Σ Davacki grouped by (TarBr, ZemjaPoteklo, EdMer) across the
        // scope. Legacy EdMer (e.g. "MTR", "PRS") is normalised to LON UoM
        // codes via MigrationContext.UoMByCode aliases so the comparison
        // groups align on the LON side's canonical code.
        var legSql = $"""
                     SELECT TarBr, ZemjaPoteklo, EdMer,
                            SUM(CAST(Kol AS decimal(18,4))) AS Q,
                            SUM(CAST(Davacki AS decimal(18,4))) AS D
                       FROM FakturiU5
                      WHERE 1=1{_ctx.ZaklucokWhere()}
                      GROUP BY TarBr, ZemjaPoteklo, EdMer
                     """;
        var leg = new Dictionary<string, (decimal Q, decimal D)>();
        // Build reverse map: legacy UoM code → LON canonical UoM code via
        // alias resolution. Walk UoMByCode entries — every code that maps
        // to a Guid also already exists; legacy aliases share their Guid
        // with the canonical code, so reverse-resolve via Guid.
        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<Guid, string>();
        foreach (var (code, id) in _ctx.UoMByCode)
        {
            if (!seen.TryGetValue(id, out var canon))
            {
                canon = code; // first occurrence wins
                seen[id] = canon;
            }
            canonical[code] = canon;
        }
        string Norm(string? uom) => uom is null ? "?" : canonical.TryGetValue(uom.Trim(), out var c) ? c : uom.Trim();

        using (var c = new SqlCommand(legSql, legacy))
        {
            _ctx.AddZaklucokParam(c);
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                var k = $"{r["TarBr"]}|{r["ZemjaPoteklo"]}|{Norm(r["EdMer"]?.ToString())}";
                leg.TryGetValue(k, out var prev);
                leg[k] = (prev.Q + Convert.ToDecimal(r["Q"]), prev.D + Convert.ToDecimal(r["D"]));
            }
        }

        // LON: same aggregation from CustomsDeclarationLines + UoMs join.
        // Filter by SpecialRemarks (where the IM marker lives — Notes is
        // null on migrated declarations).
        string lonSql = """
                        SELECT cdl.TariffCode, cdl.CountryOfOrigin, uom.Code AS UoMCode,
                               SUM(cdl.Quantity) AS Q, SUM(cdl.DutyAmount) AS D
                          FROM CustomsDeclarationLines cdl
                          JOIN CustomsDeclarations cd ON cd.Id = cdl.CustomsDeclarationId
                          LEFT JOIN UnitsOfMeasure uom ON uom.Id = cdl.UoMId
                         WHERE cdl.TenantId=@t AND cdl.IsDeleted=0
                           AND cd.DeclarationType = 'IM'
                        """;
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            lonSql += " AND cd.SpecialRemarks LIKE @marker ESCAPE '!'";
        lonSql += " GROUP BY cdl.TariffCode, cdl.CountryOfOrigin, uom.Code";

        var migr = new Dictionary<string, (decimal Q, decimal D)>();
        using (var c = new SqlCommand(lonSql, lon))
        {
            c.Parameters.AddWithValue("@t", _ctx.TenantId);
            if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
                c.Parameters.AddWithValue("@marker", $"%ZaklucokBroj={_ctx.ZaklucokFilter}!]%");
            using var r = c.ExecuteReader();
            while (r.Read())
            {
                var k = $"{r["TariffCode"]}|{r["CountryOfOrigin"]}|{r["UoMCode"]}";
                migr[k] = (Convert.ToDecimal(r["Q"]), Convert.ToDecimal(r["D"]));
            }
        }

        var keys = leg.Keys.Union(migr.Keys).ToList();
        foreach (var k in keys)
        {
            leg.TryGetValue(k, out var l);
            migr.TryGetValue(k, out var m);
            if (Math.Abs(l.Q - m.Q) > Math.Max(0.01m, Math.Abs(l.Q) * QtyTolerancePct)) ok = false;
            if (Math.Abs(l.D - m.D) > EurTolerance) ok = false;
            details.Add($"{k}: legacy Q={l.Q:F4} D={l.D:F2} | mig Q={m.Q:F4} D={m.D:F2}");
        }
        if (keys.Count == 0) details.Add("(no NaimU5 groups in scope)");
        return new ReconRow("R6", "NaimU5 re-aggregation", ok, details);
    }

    private void WriteHtml(List<ReconRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!doctype html><html><head><meta charset="utf-8">
            <title>LON ↔ ELON reconciliation</title>
            <style>
              body{font-family:system-ui,sans-serif;max-width:1024px;margin:32px auto;color:#222}
              h1{margin-bottom:4px} .scope{color:#666}
              table{border-collapse:collapse;width:100%;margin-top:16px}
              th,td{border:1px solid #ddd;padding:6px 10px;text-align:left;vertical-align:top}
              th{background:#f2f4f7}
              tr.pass td:first-child{color:#0a7c36;font-weight:600}
              tr.fail td:first-child{color:#b00020;font-weight:600}
              ul{margin:4px 0;padding-left:18px;font-size:13px;color:#444}
            </style></head><body>
            """);
        sb.Append($"<h1>Reconciliation — {_ctx.TenantCode}</h1>");
        sb.Append($"<div class='scope'>Scope: {(string.IsNullOrEmpty(_ctx.ZaklucokFilter) ? "(all)" : "Zaklucok=" + _ctx.ZaklucokFilter)} · {DateTime.Now:yyyy-MM-dd HH:mm}</div>");
        sb.Append("<table><thead><tr><th>Verdict</th><th>ID</th><th>Check</th><th>Detail</th></tr></thead><tbody>");
        foreach (var r in rows)
        {
            sb.Append($"<tr class='{(r.Pass ? "pass" : "fail")}'>");
            sb.Append($"<td>{(r.Pass ? "PASS" : "FAIL")}</td>");
            sb.Append($"<td>{r.Id}</td>");
            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(r.Label)}</td>");
            sb.Append("<td><ul>");
            foreach (var d in r.Details) sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(d)}</li>");
            sb.Append("</ul></td></tr>");
        }
        sb.Append("</tbody></table></body></html>");

        var path = Path.Combine(AppContext.BaseDirectory, "migration_reconciliation.html");
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"[reconcile] wrote {path}");
    }

    private sealed record ReconRow(string Id, string Label, bool Pass, List<string> Details);
}
