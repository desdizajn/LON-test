using Microsoft.Data.SqlClient;

namespace LON.Migration;

internal sealed class MigrationContext
{
    public string LegacyConnStr { get; init; } = string.Empty;
    public string LonConnStr { get; init; } = string.Empty;
    public string TenantCode { get; init; } = "TEKSPORT";
    public Guid TenantId { get; set; }
    public bool DryRun { get; init; }

    /// <summary>
    /// Phase 17 §E.MIGRATE — when non-null, every mapper filters its source
    /// SELECT by `ZaklucokBroj = <value>`. Required for happy-path drills;
    /// nullable for full-tenant runs (Phase 21.1).
    /// </summary>
    public string? ZaklucokFilter { get; init; }

    public Guid DefaultUoMId { get; set; }
    public Guid? DefaultSupplierPartnerId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public Guid? DefaultReceivingLocationId { get; set; }
    public Guid? DefaultProductionLocationId { get; set; }

    /// <summary>
    /// Procedure-code → Guid lookup (4200, 3151, 6121, etc.). Populated by
    /// <see cref="Hydrate"/>; auto-creates missing 4200 + 3151 + 6121 rows
    /// when LON DB only has the post-§E8 minimal seed.
    /// </summary>
    public Dictionary<string, Guid> ProcedureByCode { get; private set; } = new();

    /// <summary>UoM-code → Guid lookup (PCS, MTR, KG, etc.).</summary>
    public Dictionary<string, Guid> UoMByCode { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public SqlConnection OpenLegacy()
    {
        var c = new SqlConnection(LegacyConnStr);
        c.Open();
        return c;
    }

    public SqlConnection OpenLon()
    {
        var c = new SqlConnection(LonConnStr);
        c.Open();
        return c;
    }

    /// <summary>Pre-fetches tenant id + reference ids so mappers don't refetch.</summary>
    public void Hydrate()
    {
        using var lon = OpenLon();

        using (var cmd = new SqlCommand(
                   "SELECT Id FROM Tenants WHERE Code = @c AND IsDeleted = 0", lon))
        {
            cmd.Parameters.AddWithValue("@c", TenantCode);
            var r = cmd.ExecuteScalar();
            if (r == null || r is DBNull)
                throw new InvalidOperationException(
                    $"Tenant '{TenantCode}' not found in LON DB. Seed it first.");
            TenantId = (Guid)r;
        }

        // UoM map — PCS, MTR, KG etc. Legacy ELON uses codes that diverge
        // slightly from LON's seed: alias the well-known ones so per-line
        // EdMer lookups don't silently fall back to PCS (which corrupts R6
        // NaimU5 aggregation by tariff/UoM/country group).
        using (var cmd = new SqlCommand(
                   "SELECT Id, Code FROM UnitsOfMeasure WHERE IsDeleted = 0", lon))
        {
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                UoMByCode[rd.GetString(1)] = rd.GetGuid(0);
        }
        // Legacy → LON code aliases.
        var aliases = new (string Legacy, string Lon)[]
        {
            ("MTR", "M"),
            ("MET", "M"),
            ("MTS", "M"),
            ("PRS", "PCS"),
            ("PAI", "PCS"),
            ("PAR", "PCS"),
            ("KGM", "KG"),
            ("LTR", "L"),
            ("LIT", "L"),
            ("PKG", "BOX"),
            ("PCK", "BOX"),
        };
        foreach (var (legacy, lonCode) in aliases)
        {
            if (UoMByCode.TryGetValue(lonCode, out var id) && !UoMByCode.ContainsKey(legacy))
                UoMByCode[legacy] = id;
        }
        if (UoMByCode.TryGetValue("PCS", out var pcs))
            DefaultUoMId = pcs;
        else if (UoMByCode.Count > 0)
            DefaultUoMId = UoMByCode.First().Value;
        else
            throw new InvalidOperationException("No UoMs in LON DB. Seed UnitsOfMeasure first.");

        // CustomsProcedure map — ensure 4200, 3151, 6121, WASTE exist (auto-
        // create missing ones since the LON seed only inserts when table is
        // empty). Type int values mirror LON.Domain.Enums.CustomsProcedureType
        // (LocalPurchase=0, TemporaryImport=1, InwardProcessing=3, Export=5,
        // FinalClearance=6) — we inline ints because LON.Migration deliberately
        // doesn't reference LON.Domain.
        EnsureProcedure(lon, "4200", "Увоз за облагородување (42 00)", procType: 3, true, 50, 180);
        EnsureProcedure(lon, "3151", "Re-export of LON goods (31 51)", procType: 5, false, 0, null);
        EnsureProcedure(lon, "6121", "Re-import after export (61 21)", procType: 3, false, 0, null);
        EnsureProcedure(lon, "WASTE", "Уништување на отпад (waste destruction)", procType: 5, false, 0, null);

        using (var cmd = new SqlCommand(
                   "SELECT Id, Code FROM CustomsProcedures WHERE IsDeleted = 0", lon))
        {
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
                ProcedureByCode[rd.GetString(1)] = rd.GetGuid(0);
        }

        using (var cmd = new SqlCommand(
                   """
                   SELECT TOP 1 w.Id,
                       (SELECT TOP 1 l.Id FROM Locations l WHERE l.WarehouseId = w.Id AND l.IsDeleted = 0 ORDER BY CASE WHEN l.[Type] = 1 THEN 0 ELSE 1 END, l.Code) AS RecvId,
                       (SELECT TOP 1 l.Id FROM Locations l WHERE l.WarehouseId = w.Id AND l.IsDeleted = 0 ORDER BY CASE WHEN l.[Type] = 4 THEN 0 ELSE 1 END, l.Code) AS ProdId
                   FROM Warehouses w
                   WHERE w.TenantId = @t AND w.IsDeleted = 0
                   ORDER BY w.Code
                   """, lon))
        {
            cmd.Parameters.AddWithValue("@t", TenantId);
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                DefaultWarehouseId = rd.GetGuid(0);
                DefaultReceivingLocationId = rd.IsDBNull(1) ? null : rd.GetGuid(1);
                DefaultProductionLocationId = rd.IsDBNull(2) ? null : rd.GetGuid(2);
            }
        }

        // Fallback supplier partner (synthetic) so FK constraints satisfy
        // when legacy data has NULL Primac.
        DefaultSupplierPartnerId = EnsureLegacyPartner(lon, "LEGACY-MIG", "(Мигрирано од ELON)", partnerType: 1);
    }

    private void EnsureProcedure(SqlConnection lon, string code, string name,
        int procType, bool requiresGuarantee, decimal guaranteePct, int? dueDays)
    {
        using var existsCmd = new SqlCommand(
            "SELECT COUNT(*) FROM CustomsProcedures WHERE Code = @c AND IsDeleted = 0", lon);
        existsCmd.Parameters.AddWithValue("@c", code);
        var n = Convert.ToInt32(existsCmd.ExecuteScalar() ?? 0);
        if (n > 0) return;
        if (DryRun) return;

        using var ins = new SqlCommand(
            """
            INSERT INTO CustomsProcedures (Id, Code, Name, [Type], Description,
                RequiresGuarantee, GuaranteePercentage, DueDays, RequiresMRNTracking,
                AllowsProduction, AllowsExport, IsActive, CreatedAt, CreatedBy, IsDeleted)
            VALUES (NEWID(), @c, @n, @t, '(auto-seeded by migration)',
                @rg, @gp, @dd, 1, 1, 1, 1, SYSUTCDATETIME(), 'migration', 0);
            """, lon);
        ins.Parameters.AddWithValue("@c", code);
        ins.Parameters.AddWithValue("@n", name);
        ins.Parameters.AddWithValue("@t", procType);
        ins.Parameters.AddWithValue("@rg", requiresGuarantee);
        ins.Parameters.AddWithValue("@gp", guaranteePct);
        ins.Parameters.AddWithValue("@dd", (object?)dueDays ?? DBNull.Value);
        ins.ExecuteNonQuery();
    }

    private Guid EnsureLegacyPartner(SqlConnection lon, string code, string name, int partnerType)
    {
        var id = Helpers.DeterministicGuid("Partner", $"{TenantId}|{code}");
        if (DryRun) return id;
        using var existsCmd = new SqlCommand(
            "SELECT COUNT(*) FROM Partners WHERE Id = @id", lon);
        existsCmd.Parameters.AddWithValue("@id", id);
        var n = Convert.ToInt32(existsCmd.ExecuteScalar() ?? 0);
        if (n > 0) return id;
        using var ins = new SqlCommand(
            """
            INSERT INTO Partners (Id, TenantId, Code, Name, [Type], TaxNumber, Address,
                ContactPerson, Email, Phone, IsActive, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@id, @tenant, @code, @name, @t, NULL, NULL, NULL, NULL, NULL, 1,
                SYSUTCDATETIME(), 'migration', 0);
            """, lon);
        ins.Parameters.AddWithValue("@id", id);
        ins.Parameters.AddWithValue("@tenant", TenantId);
        ins.Parameters.AddWithValue("@code", code);
        ins.Parameters.AddWithValue("@name", name);
        ins.Parameters.AddWithValue("@t", partnerType);
        ins.ExecuteNonQuery();
        return id;
    }

    public int Exec(SqlConnection lon, string sql, params (string k, object? v)[] ps)
    {
        if (DryRun) return 0;
        using var cmd = new SqlCommand(sql, lon);
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Adds `AND ZaklucokBroj = @z` to a SELECT clause when filter is set.</summary>
    public string ZaklucokWhere(string columnQualifier = "")
    {
        if (string.IsNullOrEmpty(ZaklucokFilter)) return string.Empty;
        return $" AND {columnQualifier}ZaklucokBroj = @zb ";
    }

    public void AddZaklucokParam(SqlCommand cmd)
    {
        if (string.IsNullOrEmpty(ZaklucokFilter)) return;
        cmd.Parameters.AddWithValue("@zb", ZaklucokFilter);
    }
}
