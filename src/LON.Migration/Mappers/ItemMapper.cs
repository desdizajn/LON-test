using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// tblArtikli → Items.
///
/// Legacy columns:
///  - ArtRBr (int)  -- legacy primary key
///  - ArtKatBr (nvarchar) -- code (not guaranteed unique, we dedupe on (tenant,code))
///  - ArtNazivORG / ArtNazivMK -- names
///  - ArtKatEDM -- unit of measure string
///  - ArtTarBr -- tariff code (HS)
///  - ArtZemja -- country of origin
///  - ArtKatTip -- 0=raw-material, 1=FG (ItemType)
///  - ArtKatSurovina -- 1 if actually a raw material (additional flag)
///  - ArtOtpadProc -- waste percentage (ignored here; lives on LONAuthorizationItem)
///  - Arhivirano (bit) -- archived
/// </summary>
internal sealed class ItemMapper
{
    private readonly MigrationContext _ctx;
    public ItemMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run(int limit)
    {
        Console.WriteLine("[items] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        string top = limit > 0 ? $"TOP {limit}" : "";
        var sel = new SqlCommand(
            $"SELECT {top} ArtRBr, ArtKatBr, ArtNazivORG, ArtNazivMK, ArtKatEDM, ArtTarBr, ArtZemja, ArtKatTip, ArtKatSurovina, Arhivirano " +
            "FROM tblArtikli ORDER BY ArtRBr", legacy);

        using var rd = sel.ExecuteReader();

        int total = 0, inserted = 0, updated = 0, skipped = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (rd.Read())
        {
            total++;
            var legacyId = AsInt(rd["ArtRBr"]);
            var code = AsString(rd["ArtKatBr"]);
            var nameOrg = AsString(rd["ArtNazivORG"]);
            var nameMk = AsString(rd["ArtNazivMK"]);
            var edm = AsString(rd["ArtKatEDM"]);
            var hs = AsString(rd["ArtTarBr"]);
            var country = AsString(rd["ArtZemja"]);
            var tip = AsInt(rd["ArtKatTip"]);
            var surovina = AsInt(rd["ArtKatSurovina"]);
            var archived = AsBool(rd["Arhivirano"]);

            if (string.IsNullOrWhiteSpace(code))
            {
                skipped++;
                continue;
            }

            // dedupe on (tenant,code): first wins (we sort by ArtRBr ascending)
            if (!seen.Add(code))
            {
                skipped++;
                continue;
            }

            var id = DeterministicGuid("Item", $"{_ctx.TenantId}|{legacyId}");

            // ItemType: 0=Raw, 1=FG. Surovina flag refines to Raw on the 0 branch.
            // Enum LON: 0=RawMaterial, 1=Component, 2=FinishedGood, 3=SemiFinished.
            int type = tip == 1 ? 2 : (surovina != 0 ? 0 : 3);

            // Description: free-form notes; we pack the legacy id so reverse lookup works.
            string desc = $"[LEGACY ArtRBr={legacyId}] " + (nameOrg ?? "");

            if (_ctx.DryRun) { inserted++; continue; }

            var n = _ctx.Exec(lon,
                """
                MERGE Items AS T
                USING (SELECT @id AS Id, @tenant AS TenantId, @code AS Code, @name AS Name,
                              @desc AS Description, @type AS Type, @hs AS HSCode,
                              @country AS CountryOfOrigin, @uom AS BaseUoMId,
                              CAST(0 AS bit) AS IsBatchTracked, CAST(0 AS bit) AS IsMRNTracked,
                              CAST(0 AS decimal(18,4)) AS StandardCost,
                              CAST(@archived AS bit) AS IsDeleted) S
                ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    Code = S.Code, Name = S.Name, Description = S.Description,
                    Type = S.Type, HSCode = S.HSCode, CountryOfOrigin = S.CountryOfOrigin,
                    ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration',
                    IsDeleted = S.IsDeleted
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, Code, Name, Description, Type,
                    HSCode, CountryOfOrigin, BaseUoMId, IsBatchTracked, IsMRNTracked,
                    StandardCost, CreatedAt, CreatedBy, IsDeleted)
                    VALUES (S.Id, S.TenantId, S.Code, S.Name, S.Description, S.Type,
                        S.HSCode, S.CountryOfOrigin, S.BaseUoMId, 0, 0, 0,
                        SYSUTCDATETIME(), 'migration', S.IsDeleted);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@code", code),
                ("@name", (object?)(nameMk ?? nameOrg ?? code)),
                ("@desc", (object?)desc),
                ("@type", type),
                ("@hs", (object?)hs),
                ("@country", (object?)country),
                ("@uom", _ctx.DefaultUoMId),
                ("@archived", archived));

            if (n >= 1) inserted++;
            else updated++;

            if (total % 500 == 0) Console.WriteLine($"[items] progress total={total} inserted={inserted} skipped={skipped}");
        }

        Console.WriteLine($"[items] done total={total} written={inserted} skipped_dupe_or_empty={skipped}");
        return 0;
    }
}
