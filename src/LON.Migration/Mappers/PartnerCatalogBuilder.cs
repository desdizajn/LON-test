using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — build a synthetic Partner catalog from the union of
/// numeric FK columns across legacy tables (FakturiU5Z.Primac / Ispracac /
/// Spediter / etc., plus Izdatnici.Proizvoditel, Ispratnici.Proizvoditel*).
///
/// Real partner names come from `tblFirmi` which is **absent** in the
/// TEKSPORT-only local slice (per MAPPING.md §9). Until Phase 21.1.1 backfill,
/// we materialise Partner rows keyed on `LEG-FIRM-{n}` so the FK constraints
/// of CustomsDeclaration / ClientOrder / MaterialIssue can resolve.
///
/// Idempotent: re-running maps the same integer to the same Partner Guid via
/// <see cref="Helpers.DeterministicGuid"/>.
/// </summary>
internal sealed class PartnerCatalogBuilder
{
    private readonly MigrationContext _ctx;
    public PartnerCatalogBuilder(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[partners] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var sources = new (string Sql, string Role)[]
        {
            // FakturiU5Z partner FK columns (filter by Zaklucok if set).
            ($"SELECT DISTINCT Primac AS Id FROM FakturiU5Z WHERE Primac IS NOT NULL{_ctx.ZaklucokWhere()}", "Customer"),
            ($"SELECT DISTINCT Ispracac AS Id FROM FakturiU5Z WHERE Ispracac IS NOT NULL{_ctx.ZaklucokWhere()}", "Supplier"),
            ($"SELECT DISTINCT Proizvoditel AS Id FROM FakturiU5Z WHERE Proizvoditel IS NOT NULL{_ctx.ZaklucokWhere()}", "Producer"),
            ($"SELECT DISTINCT Proizvoditel AS Id FROM Izdatnici WHERE Proizvoditel IS NOT NULL{_ctx.ZaklucokWhere()}", "Producer"),
            ($"SELECT DISTINCT Proizvoditel AS Id FROM Ispratnici WHERE Proizvoditel IS NOT NULL", "Producer"),
        };

        int total = 0, written = 0;
        // Aggregate (legacyId → role) — first role wins; if a producer appears
        // also as customer (rare), the producer role is preferred for clarity.
        var bag = new Dictionary<int, string>();
        foreach (var (sql, role) in sources)
        {
            using var cmd = new SqlCommand(sql, legacy);
            _ctx.AddZaklucokParam(cmd);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (r.IsDBNull(0)) continue;
                var n = Convert.ToInt32(r.GetValue(0));
                if (n <= 0) continue;
                if (!bag.ContainsKey(n) || role == "Producer") bag[n] = role;
                total++;
            }
        }
        Console.WriteLine($"[partners] {bag.Count} distinct legacy partner ints (raw lookups={total})");

        foreach (var (legacyId, role) in bag)
        {
            var code = $"LEG-FIRM-{legacyId}";
            var id = DeterministicGuid("Partner", $"{_ctx.TenantId}|{code}");
            var type = role switch
            {
                "Customer" => 0,
                "Supplier" => 1,
                "Producer" => 2,
                _ => 1,
            };

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                IF NOT EXISTS (SELECT 1 FROM Partners WHERE Id = @id)
                INSERT INTO Partners (Id, TenantId, Code, Name, [Type], TaxNumber, Address,
                    ContactPerson, Email, Phone, IsActive, CreatedAt, CreatedBy, IsDeleted)
                VALUES (@id, @tenant, @code, @name, @t, NULL, NULL, NULL, NULL, NULL, 1,
                        SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@code", code),
                ("@name", $"(Legacy firm #{legacyId})"),
                ("@t", type));
            written++;
        }

        Console.WriteLine($"[partners] done bag.Count={bag.Count} written={written}");
        return 0;
    }
}
