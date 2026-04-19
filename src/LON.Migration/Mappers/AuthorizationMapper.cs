using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Odobrenija + Zaklucoci → LONAuthorizations.
///
/// Legacy model:
///  - Odobrenija = the parent authorization (OdobrenieBroj, Garancija*, OdobrenieDatum, DatumDo).
///  - Zaklucoci  = individual "conclusions" / decisions issued under an Odobrenije.
///
/// We map each Zaklucok to a LONAuthorization in LON (finer granularity, since each
/// decisionis what individual declarations cite). The parent Odobrenija contributes the
/// guarantee context.
/// </summary>
internal sealed class AuthorizationMapper
{
    private readonly MigrationContext _ctx;
    public AuthorizationMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run(int limit)
    {
        Console.WriteLine("[auths] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var partnerId = EnsureLegacyPartner(lon);

        var odobrenija = LoadOdobrenija(legacy);
        Console.WriteLine($"[auths] cached {odobrenija.Count} Odobrenija");

        string top = limit > 0 ? $"TOP {limit}" : "";
        var sel = new SqlCommand(
            $"SELECT {top} ZaklucokRBr, ZaklucokBroj, ZaklucokB, ZaklucokDatum, ZaklucokN, " +
            "ZaklucokAdr, ZaklucokTip, Arhivirano, Zakluceno, Ispracac, Proizvoditel " +
            "FROM Zaklucoci ORDER BY ZaklucokRBr", legacy);

        using var rd = sel.ExecuteReader();

        int total = 0, written = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (rd.Read())
        {
            total++;
            var legacyId = AsInt(rd["ZaklucokRBr"]);
            var broj = AsString(rd["ZaklucokBroj"])?.Trim();
            var b = AsString(rd["ZaklucokB"])?.Trim();
            var datum = AsDateOrNow(rd["ZaklucokDatum"]);
            var name = AsString(rd["ZaklucokN"]);
            var addr = AsString(rd["ZaklucokAdr"]);
            var tip = AsInt(rd["ZaklucokTip"]);
            var archived = AsBool(rd["Arhivirano"]);
            var zakluceno = AsInt(rd["Zakluceno"]);

            // AuthorizationNumber: prefer ZaklucokBroj, fallback to ZaklucokB or synthetic.
            var authNo = !string.IsNullOrWhiteSpace(broj) ? broj!
                       : !string.IsNullOrWhiteSpace(b) ? b!
                       : $"LEGACY-Z-{legacyId}";

            // dedupe: first-win per (tenant, auth number)
            if (!seen.Add(authNo))
            {
                continue;
            }

            // Match to parent Odobrenije via OdobrenieRBr reference — not stored on Zaklucok directly
            // but the OdobrenieRBr of the Odobrenija whose period covers datum.
            var parent = odobrenija.FirstOrDefault(o =>
                o.From <= datum && o.To >= datum);

            decimal guaranteeAmount = parent?.GuaranteeAmount ?? 0m;
            int completionDays = (parent?.Rok ?? 12) * 30;
            DateTime? expiry = parent?.To;

            // Status: legacy "Zakluceno" enum:
            //   0 = open / in progress
            //   1 = closed / completed
            //   2 = partially closed (various)
            string status = archived ? "Expired" : (zakluceno == 1 ? "Closed" : "Active");

            var id = DeterministicGuid("LONAuth", $"{_ctx.TenantId}|{legacyId}");

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                MERGE LONAuthorizations AS T
                USING (SELECT @id AS Id) S
                ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    AuthorizationNumber = @auth, PartnerId = @pid, IssueDate = @issue,
                    ExpiryDate = @expiry, GuaranteeAmount = @ga,
                    CompletionPeriodDays = @days, Status = @status,
                    Notes = @notes, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration',
                    IsDeleted = @deleted
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, AuthorizationNumber, PartnerId,
                    IssueDate, ExpiryDate, AuthorizationType, SystemType, OperationType,
                    GuaranteeAmount, CompetentCustomsOffice, CompletionPeriodDays, Status,
                    Notes, CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @auth, @pid, @issue, @expiry,
                            N'Повеќекратно', N'ОдложеноПлаќање', N'Обработка',
                            @ga, '', @days, @status, @notes, SYSUTCDATETIME(), 'migration', @deleted);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@auth", authNo),
                ("@pid", partnerId),
                ("@issue", datum),
                ("@expiry", (object?)expiry ?? DBNull.Value),
                ("@ga", guaranteeAmount),
                ("@days", completionDays),
                ("@status", status),
                ("@notes", (object?)$"[LEGACY ZaklucokRBr={legacyId}] {name} {addr}".Trim()),
                ("@deleted", archived));
            written++;

            if (total % 100 == 0) Console.WriteLine($"[auths] progress total={total} written={written}");
        }

        Console.WriteLine($"[auths] done total={total} written={written}");
        return 0;
    }

    private record OdRow(int RBr, string? Broj, DateTime From, DateTime To, int Rok, decimal GuaranteeAmount);

    private static List<OdRow> LoadOdobrenija(SqlConnection legacy)
    {
        using var cmd = new SqlCommand(
            "SELECT OdobrenieRBr, OdobrenieBroj, OdobrenieDatum, OdobrenieDatumDo, OdobrenieRok, " +
            "GarancijaIznos FROM Odobrenija", legacy);
        using var r = cmd.ExecuteReader();
        var list = new List<OdRow>();
        while (r.Read())
            list.Add(new OdRow(
                AsInt(r["OdobrenieRBr"]),
                AsString(r["OdobrenieBroj"]),
                AsDateOrNow(r["OdobrenieDatum"]),
                AsDate(r["OdobrenieDatumDo"]) ?? DateTime.UtcNow.AddYears(5),
                Math.Max(1, AsInt(r["OdobrenieRok"])),
                AsDecimal(r["GarancijaIznos"])));
        return list;
    }

    /// <summary>
    /// Legacy ELON has no firms table, so we create a single synthetic Partner to anchor FK
    /// requirements. This partner is tenant-scoped and flagged as legacy so domain experts can
    /// split it later if needed. Idempotent on code 'LEGACY-MIG'.
    /// </summary>
    private Guid EnsureLegacyPartner(SqlConnection lon)
    {
        var id = DeterministicGuid("Partner", $"{_ctx.TenantId}|LEGACY-MIG");
        if (_ctx.DryRun) return id;
        _ctx.Exec(lon,
            """
            IF NOT EXISTS (SELECT 1 FROM Partners WHERE Id = @id)
            INSERT INTO Partners (Id, TenantId, Code, Name, Type, TaxNumber, Address,
                ContactPerson, Email, Phone, IsActive, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@id, @tenant, 'LEGACY-MIG', N'(Мигрирано од ELON)', 0,
                    NULL, NULL, NULL, NULL, NULL, 1, SYSUTCDATETIME(), 'migration', 0);
            """,
            ("@id", id),
            ("@tenant", _ctx.TenantId));
        return id;
    }
}
