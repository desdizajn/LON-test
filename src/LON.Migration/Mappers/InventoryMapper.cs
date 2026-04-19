using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// LagerMaterijali → InventoryBalances (aggregated current stock).
///
/// Legacy LagerMaterijali is an append-only movement log: every receipt/issue writes a row
/// with PlusMinus=+1 or -1. For a snapshot migration we sum(Kol*PlusMinus) per
/// (ArtRBrMat, FakturaU5Broj) and store the result as a single InventoryBalance row
/// at the default receiving location of the tenant.
///
/// This is deliberately coarse: we migrate the net "currently on hand" number, not the
/// full history. Full history reconstruction is out of scope for Phase 3; the goal is
/// "expert can see the same numbers as ELON".
/// </summary>
internal sealed class InventoryMapper
{
    private readonly MigrationContext _ctx;
    public InventoryMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run(int limit)
    {
        Console.WriteLine("[inventory] starting");

        if (_ctx.DefaultReceivingLocationId == null)
        {
            Console.Error.WriteLine("[inventory] no receiving location seeded for tenant; aborting");
            return 4;
        }

        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        // Legacy LagerMaterijali carries ArtRBrMat = NULL for all rows; the
        // authoritative cross-reference is ArtKatBrMat (string code). Our
        // ItemMapper stored ArtKatBr as Items.Code, so join on that.
        // Proces state:
        //   1 = Imported / received
        //   6 = In production (work-in-progress)
        //   7 = Exported (discharged)
        //   8 = Final domestic import (converted off-LON)
        //   9 = Waste
        // Physical balance on hand = Σ Kol[Proces=1] − Σ Kol[Proces IN 7,8,9].
        // PlusMinus is NULL for all rows (legacy never populated it); do not rely on it.
        var itemByCode = LoadItemsByCode(lon);
        string top = limit > 0 ? $"TOP {limit}" : "";
        var sel = new SqlCommand(
            $"""
            SELECT {top} ArtKatBrMat, FakturaU5Broj, ZaklucokBroj,
                   SUM(CASE WHEN Proces = 1 THEN CAST(Kol AS decimal(18,4)) ELSE 0 END)
                 - SUM(CASE WHEN Proces IN (7, 8, 9) THEN CAST(Kol AS decimal(18,4)) ELSE 0 END)
                     AS NetQty
              FROM LagerMaterijali
             WHERE ArtKatBrMat IS NOT NULL
             GROUP BY ArtKatBrMat, FakturaU5Broj, ZaklucokBroj
            HAVING SUM(CASE WHEN Proces = 1 THEN CAST(Kol AS decimal(18,4)) ELSE 0 END)
                 - SUM(CASE WHEN Proces IN (7, 8, 9) THEN CAST(Kol AS decimal(18,4)) ELSE 0 END) > 0.0001
            """, legacy);

        sel.CommandTimeout = 600;
        using var rd = sel.ExecuteReader();

        int total = 0, written = 0, missingItem = 0;
        while (rd.Read())
        {
            total++;
            var code = AsString(rd["ArtKatBrMat"]);
            var fakt = AsString(rd["FakturaU5Broj"]);
            var zak = AsString(rd["ZaklucokBroj"]);
            var qty = AsDecimal(rd["NetQty"]);

            if (qty <= 0 || string.IsNullOrWhiteSpace(code)) continue;
            if (!itemByCode.TryGetValue(code!, out var itemId))
            {
                missingItem++;
                continue;
            }

            var mrn = $"LEG-{fakt}";
            var batch = zak;

            var id = DeterministicGuid("InvBal",
                $"{_ctx.TenantId}|{itemId}|{_ctx.DefaultReceivingLocationId}|{batch}|{mrn}");

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                MERGE InventoryBalances AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET Quantity = @q,
                    ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, ItemId, LocationId, BatchNumber,
                    MRN, Quantity, UoMId, QualityStatus, LonProcessState,
                    CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @item, @loc, @batch, @mrn, @q, @uom, 0, 1,
                        SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@item", itemId),
                ("@loc", _ctx.DefaultReceivingLocationId!.Value),
                ("@batch", (object?)batch ?? DBNull.Value),
                ("@mrn", mrn),
                ("@q", qty),
                ("@uom", _ctx.DefaultUoMId));
            written++;

            if (total % 1000 == 0) Console.WriteLine($"[inventory] progress total={total} written={written} missingItem={missingItem}");
        }

        Console.WriteLine($"[inventory] done total={total} written={written} missingItem={missingItem}");
        return 0;
    }

    private Dictionary<string, Guid> LoadItemsByCode(SqlConnection lon)
    {
        using var cmd = new SqlCommand(
            "SELECT Id, Code FROM Items WHERE TenantId=@t", lon);
        cmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        while (r.Read())
        {
            map[r.GetString(1)] = r.GetGuid(0);
        }
        return map;
    }
}
