using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — GotoviProizvodi → ClientOrderFinishedGood per
/// MAPPING.md §5.1. One legacy row maps to one FG line on the ClientOrder.
/// Items resolved via ArtKatBr code lookup (post ItemMapper).
/// </summary>
internal sealed class FinishedGoodMapper
{
    private readonly MigrationContext _ctx;
    public FinishedGoodMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[fgs] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var itemByCode = LoadItemsByCode(lon);

        var sql = $"""
                   SELECT OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr, ArtKatBr, ArtNaziv, ArtNazivMK,
                          TarBr, Kol, EdMer, Cena, Vrednost, Valuta, NalogBroj
                     FROM GotoviProizvodi
                    WHERE 1=1{_ctx.ZaklucokWhere()}
                    ORDER BY OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr
                   """;
        using var sel = new SqlCommand(sql, legacy);
        _ctx.AddZaklucokParam(sel);

        using var rd = sel.ExecuteReader();

        int total = 0, written = 0, missingItem = 0;
        while (rd.Read())
        {
            total++;
            var odRBr = AsInt(rd["OdobrenieRBr"]);
            var zb = AsStringOrEmpty(rd["ZaklucokBroj"]);
            var gpRBr = AsInt(rd["GotovProizvodRBr"]);
            var artCode = AsString(rd["ArtKatBr"]) ?? string.Empty;
            var nameMk = AsString(rd["ArtNazivMK"]) ?? AsString(rd["ArtNaziv"]);
            var qty = AsDecimal(rd["Kol"]);
            var ed = AsString(rd["EdMer"]);
            var price = AsDecimal(rd["Cena"]);
            var valuta = AsString(rd["Valuta"]) ?? "EUR";
            var nalog = AsString(rd["NalogBroj"]);

            if (!itemByCode.TryGetValue(artCode, out var itemId))
            {
                missingItem++;
                continue;
            }

            var clientOrderId = ClientOrderMapper.ResolveId(_ctx, odRBr, zb);
            var fgId = DeterministicGuid("FG", $"{clientOrderId}|{gpRBr}|{artCode}");
            var uomId = _ctx.UoMByCode.TryGetValue(ed ?? "", out var u) ? u : _ctx.DefaultUoMId;

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                MERGE ClientOrderFinishedGoods AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    ClientOrderId = @co, ItemId = @item, Quantity = @q, UoMId = @uom,
                    UnitPriceForeign = @price, Currency = @cur, Notes = @notes,
                    ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, ClientOrderId, ItemId, Quantity,
                    UoMId, BOMId, UnitPriceForeign, Currency, Notes,
                    CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @co, @item, @q, @uom, NULL, @price, @cur, @notes,
                            SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", fgId),
                ("@tenant", _ctx.TenantId),
                ("@co", clientOrderId),
                ("@item", itemId),
                ("@q", qty),
                ("@uom", uomId),
                ("@price", price),
                ("@cur", valuta),
                ("@notes", $"[LEGACY GotovProizvodRBr={gpRBr} NalogBroj={nalog}] {nameMk}".Trim()));
            written++;
        }

        Console.WriteLine($"[fgs] done total={total} written={written} missingItem={missingItem}");
        return 0;
    }

    public static Guid ResolveFgId(MigrationContext ctx, int odobrenieRBr, string zaklucokBroj, int gpRBr, string artCode)
    {
        var clientOrderId = ClientOrderMapper.ResolveId(ctx, odobrenieRBr, zaklucokBroj);
        return DeterministicGuid("FG", $"{clientOrderId}|{gpRBr}|{artCode}");
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
