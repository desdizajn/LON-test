using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — Zaklucoci → ClientOrder per BLUEPRINT §3.1 +
/// MAPPING.md §2.2.
///
/// Per BLUEPRINT, each Zaklucok is exactly one ClientOrder. The migrator
/// resolves the customer Partner via the first FakturiU5Z row's Primac for
/// this Zaklucok (legacy data didn't carry an explicit customer on Zaklucok;
/// it was always derived from the IM declaration's recipient).
///
/// LONAuthorizationId resolves via OdobrenieRBr → OdobrenijaMapper.Lookup.
/// OrderNumber stamped as `CO-{year}-{seq:D6}` via deterministic seq —
/// SEQUENCE is bypassed here because migration runs once and we want
/// idempotent re-runs to produce the same OrderNumber strings.
/// </summary>
internal sealed class ClientOrderMapper
{
    private readonly MigrationContext _ctx;
    public ClientOrderMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[orders] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var odMap = OdobrenijaMapper.Lookup(_ctx, legacy, lon);

        // Pull first Primac per Zaklucok (for CustomerPartnerId).
        var sql = """
                  SELECT z.OdobrenieRBr, z.ZaklucokBroj, z.ZaklucokDatum, z.ZaklucokN,
                         z.Arhivirano, z.Zakluceno,
                         (SELECT TOP 1 f.Primac FROM FakturiU5Z f
                            WHERE f.OdobrenieRBr = z.OdobrenieRBr
                              AND f.ZaklucokBroj = z.ZaklucokBroj
                              AND f.Primac IS NOT NULL
                            ORDER BY f.FakturaU5Datum) AS PrimacInt,
                         (SELECT MIN(CAST(f.RazdolzenaDaNe AS int)) FROM FakturiU5Z f
                            WHERE f.OdobrenieRBr = z.OdobrenieRBr
                              AND f.ZaklucokBroj = z.ZaklucokBroj) AS MinRazd
                  FROM Zaklucoci z
                  WHERE z.ZaklucokBroj IS NOT NULL AND z.ZaklucokBroj <> '00000'
                  """;
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
            sql += " AND z.ZaklucokBroj = @zb";
        sql += " ORDER BY z.OdobrenieRBr, z.ZaklucokBroj";

        using var sel = new SqlCommand(sql, legacy);
        _ctx.AddZaklucokParam(sel);
        using var rd = sel.ExecuteReader();

        // Seed the local counter from the existing MAX(seq) per (tenant, year)
        // in LON. Without this, two single-Zaklucok migrations against the
        // same tenant both start at 1 and collide on the unique
        // (TenantId, OrderNumber) index. The counter is per-year because the
        // OrderNumber format `CO-{year}-{seq:D6}` namespaces the sequence by
        // year. Sequence is bumped per Zaklucok within the same year.
        int total = 0, written = 0, missingAuth = 0, missingCustomer = 0;
        var perYearSeed = new Dictionary<int, int>();
        using (var seedCmd = new SqlCommand(
            "SELECT YEAR(OrderDate) AS Y, MAX(CAST(SUBSTRING(OrderNumber, 9, 6) AS int)) AS Seq " +
            "FROM ClientOrders WHERE TenantId = @t AND OrderNumber LIKE 'CO-%' GROUP BY YEAR(OrderDate)", lon))
        {
            seedCmd.Parameters.AddWithValue("@t", _ctx.TenantId);
            using var srd = seedCmd.ExecuteReader();
            while (srd.Read())
                perYearSeed[Convert.ToInt32(srd["Y"])] = Convert.ToInt32(srd["Seq"]);
        }
        var rows = new List<(int OdobrenieRBr, string ZaklucokBroj, DateTime Date, string? Name, bool Archived, int Zakluceno, int? PrimacInt, bool AllRazdolzeno)>();
        while (rd.Read())
        {
            rows.Add((
                AsInt(rd["OdobrenieRBr"]),
                AsStringOrEmpty(rd["ZaklucokBroj"]),
                AsDateOrNow(rd["ZaklucokDatum"]),
                AsString(rd["ZaklucokN"]),
                AsBool(rd["Arhivirano"]),
                AsInt(rd["Zakluceno"]),
                rd["PrimacInt"] is DBNull ? null : Convert.ToInt32(rd["PrimacInt"]),
                rd["MinRazd"] is DBNull ? false : Convert.ToInt32(rd["MinRazd"]) == 1));
        }
        rd.Close();

        foreach (var row in rows)
        {
            total++;

            if (!odMap.TryGetValue(row.OdobrenieRBr, out var authId))
            {
                missingAuth++;
                continue;
            }

            Guid customerId;
            if (row.PrimacInt is int pInt && pInt > 0)
            {
                customerId = DeterministicGuid("Partner", $"{_ctx.TenantId}|LEG-FIRM-{pInt}");
            }
            else
            {
                customerId = _ctx.DefaultSupplierPartnerId!.Value;
                missingCustomer++;
            }

            // Reuse an existing OrderNumber on re-run (so the second pass of
            // the SAME zaklucok doesn't fight the unique index); otherwise
            // bump the per-year seed and stamp a fresh number.
            var id = DeterministicGuid("ClientOrder", $"{_ctx.TenantId}|{row.OdobrenieRBr}|{row.ZaklucokBroj}");
            string? existingOrderNumber = null;
            using (var existCmd = new SqlCommand(
                "SELECT OrderNumber FROM ClientOrders WHERE Id = @id", lon))
            {
                existCmd.Parameters.AddWithValue("@id", id);
                var r = existCmd.ExecuteScalar();
                if (r is string s) existingOrderNumber = s;
            }

            string orderNumber;
            if (existingOrderNumber != null)
            {
                orderNumber = existingOrderNumber;
            }
            else
            {
                var year = row.Date.Year;
                perYearSeed.TryGetValue(year, out var seed);
                seed++;
                perYearSeed[year] = seed;
                orderNumber = $"CO-{year:D4}-{seed:D6}";
            }

            // Status: Closed if all FakturiU5Z.RazdolzenaDaNe=true; Cancelled if archived; else Active.
            int status = row.Archived ? 99 : (row.AllRazdolzeno ? 4 : 1);

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                MERGE ClientOrders AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    OrderNumber = @num, CustomerPartnerId = @cust, LONAuthorizationId = @auth,
                    CustomerOrderReference = @ref, OrderDate = @date, Status = @status,
                    Notes = @notes, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, OrderNumber, CustomerPartnerId,
                    LONAuthorizationId, CustomerOrderReference, OrderDate, Status, Notes,
                    CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @num, @cust, @auth, @ref, @date, @status, @notes,
                            SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@num", orderNumber),
                ("@cust", customerId),
                ("@auth", authId),
                ("@ref", $"O{row.OdobrenieRBr}-Z{row.ZaklucokBroj}"),
                ("@date", row.Date),
                ("@status", status),
                ("@notes", $"[LEGACY OdobrenieRBr={row.OdobrenieRBr} ZaklucokBroj={row.ZaklucokBroj}] {row.Name}".Trim()));
            written++;
        }

        Console.WriteLine($"[orders] done total={total} written={written} missingAuth={missingAuth} missingCustomer={missingCustomer}");
        return 0;
    }

    /// <summary>Lookup map ((OdobrenieRBr, ZaklucokBroj) → ClientOrder.Id).</summary>
    public static Dictionary<(int, string), Guid> Lookup(MigrationContext ctx)
    {
        return new Dictionary<(int, string), Guid>();
    }

    public static Guid ResolveId(MigrationContext ctx, int odobrenieRBr, string zaklucokBroj)
        => DeterministicGuid("ClientOrder", $"{ctx.TenantId}|{odobrenieRBr}|{zaklucokBroj}");
}
