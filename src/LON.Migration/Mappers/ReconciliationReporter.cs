using Microsoft.Data.SqlClient;
using System.Text;

namespace LON.Migration.Mappers;

/// <summary>
/// Writes an HTML report comparing legacy ELON vs LON counts for the migrated tenant.
/// </summary>
internal sealed class ReconciliationReporter
{
    private readonly MigrationContext _ctx;
    public ReconciliationReporter(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[reconcile] starting");

        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        int legacyItems = Count(legacy, "SELECT COUNT(*) FROM tblArtikli WHERE ISNULL(Arhivirano,0)=0");
        int lonItems = Count(lon, "SELECT COUNT(*) FROM Items WHERE TenantId=@t AND IsDeleted=0", _ctx.TenantId);

        int legacyAuths = Count(legacy, "SELECT COUNT(*) FROM Zaklucoci WHERE ISNULL(Arhivirano,0)=0");
        int lonAuths = Count(lon, "SELECT COUNT(*) FROM LONAuthorizations WHERE TenantId=@t AND IsDeleted=0", _ctx.TenantId);

        int legacyDeclHeaders = CountScalar(legacy, "SELECT COUNT(DISTINCT FakturaU5Broj) FROM FakturiU5Z");
        int lonDeclHeaders = Count(lon, "SELECT COUNT(*) FROM CustomsDeclarations WHERE TenantId=@t AND IsDeleted=0", _ctx.TenantId);

        int legacyDeclLines = CountScalar(legacy, "SELECT COUNT(*) FROM FakturiU5");
        int lonDeclLines = Count(lon, "SELECT COUNT(*) FROM CustomsDeclarationLines WHERE TenantId=@t AND IsDeleted=0", _ctx.TenantId);

        decimal legacyInv = DecimalScalar(legacy,
            "SELECT SUM(CAST(Kol AS decimal(18,4)) * CAST(PlusMinus AS decimal(2,0))) FROM LagerMaterijali");
        decimal lonInv = DecimalScalar(lon,
            "SELECT ISNULL(SUM(Quantity),0) FROM InventoryBalances WHERE TenantId=@t AND IsDeleted=0", _ctx.TenantId);

        var zakSample = SampleZaklucok(legacy, lon);

        var sb = new StringBuilder();
        sb.Append("""
            <!doctype html><html lang="mk"><head><meta charset="utf-8">
            <title>LON ↔ ELON reconciliation</title>
            <style>
              body{font-family:system-ui,sans-serif;max-width:960px;margin:40px auto;color:#222;}
              h1{margin-bottom:8px;} .sub{color:#666;margin-top:0;}
              table{border-collapse:collapse;width:100%;margin:24px 0;}
              th,td{padding:8px 12px;border:1px solid #ddd;text-align:left;}
              th{background:#f2f4f7;}
              .ok{color:#0a7c36;font-weight:600;} .diff{color:#b00020;font-weight:600;}
              .muted{color:#888;font-size:90%;}
            </style></head><body>
            """);

        sb.Append($"<h1>Reconciliation — {_ctx.TenantCode}</h1>");
        sb.Append($"<p class='sub'>Генерирано {DateTime.Now:yyyy-MM-dd HH:mm}</p>");

        sb.Append("<table><thead><tr><th>Тип</th><th>ELON (legacy)</th><th>LON</th><th>Статус</th></tr></thead><tbody>");
        AppendRow(sb, "Items (tblArtikli → Items)", legacyItems, lonItems);
        AppendRow(sb, "Authorizations (Zaklucoci → LONAuthorizations)", legacyAuths, lonAuths);
        AppendRow(sb, "Declaration headers (FakturiU5Z → CustomsDeclarations)", legacyDeclHeaders, lonDeclHeaders);
        AppendRow(sb, "Declaration lines (FakturiU5 → CustomsDeclarationLines)", legacyDeclLines, lonDeclLines);
        AppendRowDecimal(sb, "Inventory net Qty (LagerMaterijali → InventoryBalances)", legacyInv, lonInv);
        sb.Append("</tbody></table>");

        if (zakSample != null)
        {
            sb.Append("<h2>Пример — Zaklucok side-by-side</h2>");
            sb.Append($"<p><b>Zaklucok:</b> {zakSample.Broj}</p>");
            sb.Append("<table><thead><tr><th>Метрика</th><th>ELON</th><th>LON</th></tr></thead><tbody>");
            sb.Append($"<tr><td>Број на U5 декларации</td><td>{zakSample.LegacyDecls}</td><td>{zakSample.LonDecls}</td></tr>");
            sb.Append($"<tr><td>Вкупна количина (материјал)</td><td>{zakSample.LegacyQty:N2}</td><td>{zakSample.LonQty:N2}</td></tr>");
            sb.Append("</tbody></table>");
        }

        sb.Append("<p class='muted'>Файлот е само за визуелна споредба. Секоја миграција go overwrite-ира овој фајл на почеток.</p>");
        sb.Append("</body></html>");

        var path = Path.Combine(AppContext.BaseDirectory, "migration_reconciliation.html");
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"[reconcile] wrote {path}");
        return 0;
    }

    private static void AppendRow(StringBuilder sb, string label, long a, long b)
    {
        bool ok = a == b;
        sb.Append("<tr><td>").Append(label).Append("</td>");
        sb.Append("<td>").Append(a).Append("</td>");
        sb.Append("<td>").Append(b).Append("</td>");
        sb.Append("<td class='").Append(ok ? "ok" : "diff").Append("'>")
          .Append(ok ? "MATCH" : $"Δ {(b - a):+0;-0;0}").Append("</td></tr>");
    }

    private static void AppendRowDecimal(StringBuilder sb, string label, decimal a, decimal b)
    {
        bool ok = Math.Abs(a - b) < 0.01m;
        sb.Append("<tr><td>").Append(label).Append("</td>");
        sb.Append("<td>").Append(a.ToString("N2")).Append("</td>");
        sb.Append("<td>").Append(b.ToString("N2")).Append("</td>");
        sb.Append("<td class='").Append(ok ? "ok" : "diff").Append("'>")
          .Append(ok ? "MATCH" : $"Δ {(b - a).ToString("+0.00;-0.00;0")}").Append("</td></tr>");
    }

    private int Count(SqlConnection c, string sql, params object[] args)
    {
        using var cmd = new SqlCommand(sql, c);
        cmd.CommandTimeout = 120;
        if (args.Length == 1) cmd.Parameters.AddWithValue("@t", args[0]);
        var r = cmd.ExecuteScalar();
        if (r == null || r is DBNull) return 0;
        return Convert.ToInt32(r);
    }

    private static int CountScalar(SqlConnection c, string sql)
    {
        using var cmd = new SqlCommand(sql, c);
        cmd.CommandTimeout = 300;
        var r = cmd.ExecuteScalar();
        if (r == null || r is DBNull) return 0;
        return Convert.ToInt32(r);
    }

    private decimal DecimalScalar(SqlConnection c, string sql, params object[] args)
    {
        using var cmd = new SqlCommand(sql, c);
        cmd.CommandTimeout = 300;
        if (args.Length == 1) cmd.Parameters.AddWithValue("@t", args[0]);
        var r = cmd.ExecuteScalar();
        if (r == null || r is DBNull) return 0m;
        return Convert.ToDecimal(r);
    }

    private record Sample(string Broj, int LegacyDecls, int LonDecls, decimal LegacyQty, decimal LonQty);

    private Sample? SampleZaklucok(SqlConnection legacy, SqlConnection lon)
    {
        // pick a recent open Zaklucok with data
        using var pick = new SqlCommand(
            "SELECT TOP 1 ZaklucokBroj FROM Zaklucoci WHERE ISNULL(Arhivirano,0)=0 AND Zakluceno=0 AND ZaklucokBroj IS NOT NULL AND ZaklucokBroj<>'' ORDER BY ZaklucokDatum DESC",
            legacy);
        var r = pick.ExecuteScalar();
        if (r == null || r is DBNull) return null;
        string broj = (string)r;

        int legDecls = CountScalar(legacy,
            $"SELECT COUNT(DISTINCT FakturaU5Broj) FROM FakturiU5Z WHERE ZaklucokBroj = N'{broj.Replace("'", "''")}'");
        decimal legQty = DecimalScalar(legacy,
            $"SELECT SUM(Kol) FROM FakturiU5 WHERE ZaklucokBroj = N'{broj.Replace("'", "''")}'");

        using var lonDeclsCmd = new SqlCommand(
            "SELECT COUNT(*) FROM CustomsDeclarations d JOIN LONAuthorizations a ON a.Id = d.LONAuthorizationId " +
            "WHERE d.TenantId = @t AND a.AuthorizationNumber = @n AND d.IsDeleted = 0", lon);
        lonDeclsCmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        lonDeclsCmd.Parameters.AddWithValue("@n", broj);
        int lonDecls = Convert.ToInt32(lonDeclsCmd.ExecuteScalar() ?? 0);

        using var lonQtyCmd = new SqlCommand(
            "SELECT ISNULL(SUM(l.Quantity),0) FROM CustomsDeclarationLines l " +
            "JOIN CustomsDeclarations d ON d.Id = l.CustomsDeclarationId " +
            "JOIN LONAuthorizations a ON a.Id = d.LONAuthorizationId " +
            "WHERE l.TenantId = @t AND a.AuthorizationNumber = @n AND l.IsDeleted = 0", lon);
        lonQtyCmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        lonQtyCmd.Parameters.AddWithValue("@n", broj);
        decimal lonQty = Convert.ToDecimal(lonQtyCmd.ExecuteScalar() ?? 0m);

        return new Sample(broj, legDecls, lonDecls, legQty, lonQty);
    }
}
