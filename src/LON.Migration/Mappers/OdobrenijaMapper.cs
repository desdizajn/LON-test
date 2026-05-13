using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — Odobrenija (parent authorization) → LONAuthorization
/// per BLUEPRINT §3.3 + MAPPING.md §2.1.
///
/// When a Zaklucok filter is supplied (happy-path drill), we only migrate the
/// Odobrenija whose OdobrenieRBr matches that Zaklucok's parent. For Z2779
/// that's `OdobrenieRBr=1` (the primary TEKSPORT authorization carrying 248
/// of 269 Zaklucoci).
/// </summary>
internal sealed class OdobrenijaMapper
{
    private readonly MigrationContext _ctx;
    public OdobrenijaMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[odobrenija] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var sql = "SELECT OdobrenieRBr, OdobrenieBroj, OdobrenieDatum, OdobrenieDatumDo, " +
                  "OdobrenieRok, GarancijaBroj, GarancijaIznos, OdobrenieN, OdobrenieCarSluz, Arhivirano " +
                  "FROM Odobrenija";
        if (!string.IsNullOrEmpty(_ctx.ZaklucokFilter))
        {
            sql += " WHERE OdobrenieRBr IN (SELECT DISTINCT OdobrenieRBr FROM Zaklucoci WHERE ZaklucokBroj = @zb)";
        }
        sql += " ORDER BY OdobrenieRBr";

        using var sel = new SqlCommand(sql, legacy);
        _ctx.AddZaklucokParam(sel);

        using var rd = sel.ExecuteReader();
        int total = 0, written = 0;

        while (rd.Read())
        {
            total++;
            var rbr = AsInt(rd["OdobrenieRBr"]);
            var broj = AsString(rd["OdobrenieBroj"]) ?? $"LEGACY-O-{rbr}";
            var issued = AsDateOrNow(rd["OdobrenieDatum"]);
            var expiry = AsDate(rd["OdobrenieDatumDo"]);
            var rokMonths = Math.Max(1, AsInt(rd["OdobrenieRok"]));
            var garancijaBroj = AsString(rd["GarancijaBroj"]);
            var garancijaIznos = AsDecimal(rd["GarancijaIznos"]);
            var name = AsString(rd["OdobrenieN"]);
            var carSluz = AsString(rd["OdobrenieCarSluz"]) ?? string.Empty;
            var archived = AsBool(rd["Arhivirano"]);

            var id = DeterministicGuid("LONAuth", $"{_ctx.TenantId}|odob|{rbr}");
            int completionDays = rokMonths * 30;
            string status = archived ? "Expired" : "Active";

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                MERGE LONAuthorizations AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    AuthorizationNumber = @auth, PartnerId = @pid, IssueDate = @issue,
                    ExpiryDate = @expiry, GuaranteeAmount = @ga, GuaranteeReference = @gref,
                    CompletionPeriodDays = @days, Status = @status,
                    CompetentCustomsOffice = @car, Notes = @notes,
                    ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = @deleted
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, AuthorizationNumber, PartnerId,
                    IssueDate, ExpiryDate, AuthorizationType, SystemType, OperationType,
                    GuaranteeAmount, GuaranteeReference, CompetentCustomsOffice,
                    CompletionPeriodDays, Status, Notes, CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @auth, @pid, @issue, @expiry,
                            N'Повеќекратно', N'ОдложеноПлаќање', N'Обработка',
                            @ga, @gref, @car, @days, @status, @notes,
                            SYSUTCDATETIME(), 'migration', @deleted);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@auth", broj),
                ("@pid", _ctx.DefaultSupplierPartnerId!.Value),
                ("@issue", issued),
                ("@expiry", (object?)expiry ?? DBNull.Value),
                ("@ga", garancijaIznos),
                ("@gref", (object?)garancijaBroj ?? DBNull.Value),
                ("@days", completionDays),
                ("@status", status),
                ("@car", carSluz),
                ("@notes", $"[LEGACY OdobrenieRBr={rbr}] {name}".Trim()),
                ("@deleted", archived));
            written++;
        }

        Console.WriteLine($"[odobrenija] done total={total} written={written}");
        return 0;
    }

    /// <summary>Lookup map (OdobrenieRBr → LONAuthorization.Id) used by ClientOrderMapper + DeclarationMapper.</summary>
    public static Dictionary<int, Guid> Lookup(MigrationContext ctx, SqlConnection legacy, SqlConnection lon)
    {
        var map = new Dictionary<int, Guid>();
        using var cmd = new SqlCommand("SELECT OdobrenieRBr FROM Odobrenija ORDER BY OdobrenieRBr", legacy);
        using var rd = cmd.ExecuteReader();
        var rbrs = new List<int>();
        while (rd.Read()) rbrs.Add(AsInt(rd["OdobrenieRBr"]));
        rd.Close();
        foreach (var rbr in rbrs)
        {
            map[rbr] = DeterministicGuid("LONAuth", $"{ctx.TenantId}|odob|{rbr}");
        }
        return map;
    }
}
