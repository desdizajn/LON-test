using Microsoft.Data.SqlClient;

namespace LON.Migration;

internal sealed class MigrationContext
{
    public string LegacyConnStr { get; init; } = string.Empty;
    public string LonConnStr { get; init; } = string.Empty;
    public string TenantCode { get; init; } = "TEKSPORT";
    public Guid TenantId { get; set; }
    public bool DryRun { get; init; }

    public Guid DefaultUoMId { get; set; }
    public Guid? DefaultSupplierPartnerId { get; set; }
    public Guid? InwardProcessingProcedureId { get; set; }
    public Guid? DefaultWarehouseId { get; set; }
    public Guid? DefaultReceivingLocationId { get; set; }

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

        using (var cmd = new SqlCommand(
                   "SELECT TOP 1 Id FROM UnitsOfMeasure WHERE Code = 'PCS' AND IsDeleted = 0", lon))
        {
            var r = cmd.ExecuteScalar();
            if (r == null || r is DBNull)
                throw new InvalidOperationException("Base UoM 'PCS' not found in LON DB.");
            DefaultUoMId = (Guid)r;
        }

        using (var cmd = new SqlCommand(
                   "SELECT TOP 1 Id FROM CustomsProcedures WHERE Code IN ('4200', 'INW-PROC') AND IsDeleted = 0 ORDER BY CASE Code WHEN '4200' THEN 0 ELSE 1 END", lon))
        {
            var r = cmd.ExecuteScalar();
            InwardProcessingProcedureId = r is Guid g ? g : null;
        }

        using (var cmd = new SqlCommand(
                   "SELECT TOP 1 w.Id, (SELECT TOP 1 l.Id FROM Locations l WHERE l.WarehouseId = w.Id AND l.IsDeleted = 0 ORDER BY CASE WHEN l.[Type] = 1 THEN 0 ELSE 1 END, l.Code) AS LocId FROM Warehouses w WHERE w.TenantId = @t AND w.IsDeleted = 0 ORDER BY w.Code", lon))
        {
            cmd.Parameters.AddWithValue("@t", TenantId);
            using var rd = cmd.ExecuteReader();
            if (rd.Read())
            {
                DefaultWarehouseId = rd.GetGuid(0);
                DefaultReceivingLocationId = rd.IsDBNull(1) ? null : rd.GetGuid(1);
            }
        }

        using (var cmd = new SqlCommand(
                   "SELECT TOP 1 Id FROM Partners WHERE TenantId = @t AND IsDeleted = 0 ORDER BY Code", lon))
        {
            cmd.Parameters.AddWithValue("@t", TenantId);
            var r = cmd.ExecuteScalar();
            DefaultSupplierPartnerId = r is Guid g ? g : null;
        }
    }

    public int Exec(SqlConnection lon, string sql, params (string k, object? v)[] ps)
    {
        if (DryRun) return 0;
        using var cmd = new SqlCommand(sql, lon);
        foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }
}
