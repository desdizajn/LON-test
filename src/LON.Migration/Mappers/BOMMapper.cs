using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — Normativi → BOM + BOMLine per MAPPING.md §5.2.
///
/// Legacy `Normativi` has one row per BOM-line per FG instance (319k rows
/// total). LON normalises to:
///   * One BOM per (FG Item, ClientOrder)
///   * N BOMLines per BOM
///
/// Dedupe rule: group by (OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr,
/// ArtKatBrMat). First occurrence wins; later identicals collapse. We
/// log the collapsed-count for the reconciliation report (§10 R5).
/// </summary>
internal sealed class BOMMapper
{
    private readonly MigrationContext _ctx;
    public BOMMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[boms] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var itemByCode = LoadItemsByCode(lon);

        // Distinct FG headers first.
        var fgSql = $"""
                     SELECT DISTINCT OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr, ArtKatBr
                       FROM GotoviProizvodi
                      WHERE 1=1{_ctx.ZaklucokWhere()}
                     """;
        var fgList = new List<(int Od, string Zb, int Gp, string ArtCode)>();
        using (var fgCmd = new SqlCommand(fgSql, legacy))
        {
            _ctx.AddZaklucokParam(fgCmd);
            using var r = fgCmd.ExecuteReader();
            while (r.Read())
                fgList.Add((AsInt(r["OdobrenieRBr"]), AsStringOrEmpty(r["ZaklucokBroj"]),
                            AsInt(r["GotovProizvodRBr"]), AsString(r["ArtKatBr"]) ?? ""));
        }
        Console.WriteLine($"[boms] {fgList.Count} FG headers to enumerate");

        int totalLines = 0, written = 0, collapsed = 0, missingItem = 0;

        foreach (var fg in fgList)
        {
            if (!itemByCode.TryGetValue(fg.ArtCode, out var fgItemId)) { missingItem++; continue; }

            var bomId = DeterministicGuid("BOM", $"{_ctx.TenantId}|{fg.Od}|{fg.Zb}|{fg.Gp}|{fg.ArtCode}");
            var bomCode = $"BOM-O{fg.Od}-Z{fg.Zb}-GP{fg.Gp}";

            if (!_ctx.DryRun)
            {
                // Resolve a free Version for this ItemId. Legacy ELON ties one
                // BOM to one finished good per (Odobrenie, Zaklucok, GP_RBr) —
                // when the same Article is the FG of multiple FG-rows (e.g.
                // size variants of the same jacket OR the same article reused
                // across different Zaklucoci), all of them collide at v=1 on
                // the LON unique index (ItemId, Version).
                // Strategy: keep the existing version if this BOM Id is
                // already in the table; else pick MAX(Version)+1 for the
                // ItemId. Idempotent across re-runs.
                int version;
                using (var verCmd = new SqlCommand(
                    "SELECT TOP 1 Version FROM BOMs WHERE Id = @id", lon))
                {
                    verCmd.Parameters.AddWithValue("@id", bomId);
                    var existing = verCmd.ExecuteScalar();
                    if (existing != null && existing != DBNull.Value)
                        version = Convert.ToInt32(existing);
                    else
                    {
                        using var maxCmd = new SqlCommand(
                            "SELECT ISNULL(MAX(Version), 0) FROM BOMs WHERE ItemId = @item AND TenantId = @t", lon);
                        maxCmd.Parameters.AddWithValue("@item", fgItemId);
                        maxCmd.Parameters.AddWithValue("@t", _ctx.TenantId);
                        version = Convert.ToInt32(maxCmd.ExecuteScalar()) + 1;
                    }
                }

                _ctx.Exec(lon,
                    """
                    MERGE BOMs AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        Code = @code, ItemId = @item, Version = @ver, ValidFrom = @from,
                        IsActive = 1, BaseQuantity = 1, ModifiedAt = SYSUTCDATETIME(),
                        ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, Code, ItemId, Version,
                        ValidFrom, IsActive, BaseQuantity, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (@id, @tenant, @code, @item, @ver, @from, 1, 1,
                                SYSUTCDATETIME(), 'migration', 0);
                    """,
                    ("@id", bomId),
                    ("@tenant", _ctx.TenantId),
                    ("@code", bomCode),
                    ("@item", fgItemId),
                    ("@ver", version),
                    ("@from", DateTime.UtcNow.Date));
                written++;

                // Idempotency: re-runs may have a different effective lineIdx
                // ordering (e.g. when the dedupe key resolves differently after
                // upstream fixes). Drop existing BOM lines so the unique
                // (BOMId, LineNumber) index doesn't conflict with the fresh
                // insertion sequence.
                _ctx.Exec(lon, "DELETE FROM BOMLines WHERE BOMId = @b", ("@b", bomId));
            }

            // Now the BOM lines.
            using var linesCmd = new SqlCommand(
                "SELECT NormativRBr, ArtKatBrMat, Normativ, EdMerMat AS EdMer FROM Normativi " +
                "WHERE OdobrenieRBr=@o AND ZaklucokBroj=@z AND GotovProizvodRBr=@gp ORDER BY NormativRBr",
                legacy);
            linesCmd.Parameters.AddWithValue("@o", fg.Od);
            linesCmd.Parameters.AddWithValue("@z", fg.Zb);
            linesCmd.Parameters.AddWithValue("@gp", fg.Gp);
            using var lr = linesCmd.ExecuteReader();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int lineIdx = 0;
            while (lr.Read())
            {
                totalLines++;
                var rbr = AsInt(lr["NormativRBr"]);
                var matCode = AsString(lr["ArtKatBrMat"]) ?? "";
                var normativ = AsDecimal(lr["Normativ"]);
                var ed = AsString(lr["EdMer"]);

                if (string.IsNullOrWhiteSpace(matCode)) continue;
                if (!itemByCode.TryGetValue(matCode, out var matItemId)) { missingItem++; continue; }

                // Dedupe key: same (material, normativ, uom) → collapse.
                var key = $"{matCode}|{normativ}|{ed}";
                if (!seen.Add(key)) { collapsed++; continue; }

                lineIdx++;
                var lineId = DeterministicGuid("BOMLine", $"{bomId}|{matCode}|{rbr}");
                var uomId = _ctx.UoMByCode.TryGetValue(ed ?? "", out var u) ? u : _ctx.DefaultUoMId;

                if (_ctx.DryRun) continue;

                _ctx.Exec(lon,
                    """
                    MERGE BOMLines AS T
                    USING (SELECT @id AS Id) S ON T.Id = S.Id
                    WHEN MATCHED THEN UPDATE SET
                        BOMId = @b, LineNumber = @line, ItemId = @item, Quantity = @q,
                        UoMId = @uom, ScrapPercentage = 0, ModifiedAt = SYSUTCDATETIME(),
                        ModifiedBy = 'migration', IsDeleted = 0
                    WHEN NOT MATCHED THEN INSERT (Id, TenantId, BOMId, LineNumber, ItemId,
                        Quantity, UoMId, ScrapPercentage, CreatedAt, CreatedBy, IsDeleted)
                        VALUES (@id, @tenant, @b, @line, @item, @q, @uom, 0,
                                SYSUTCDATETIME(), 'migration', 0);
                    """,
                    ("@id", lineId),
                    ("@tenant", _ctx.TenantId),
                    ("@b", bomId),
                    ("@line", lineIdx),
                    ("@item", matItemId),
                    ("@q", normativ),
                    ("@uom", uomId));
            }
        }

        Console.WriteLine($"[boms] done BOMs={fgList.Count} totalLines={totalLines} written={written} collapsedDuplicates={collapsed} missingItem={missingItem}");
        return 0;
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
