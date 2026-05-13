using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — synthesize ProductionOrder + MaterialIssue +
/// DeliveryNote(ProducerDispatch) from legacy Proces=7 LagerMaterijali rows
/// per MAPPING.md §6.1 + §6.1 auto-gen note.
///
/// Why synthesize a ProductionOrder? MaterialIssue.ProductionOrderId is
/// non-null in LON's schema (legacy had no explicit PO; everything happened
/// at the Zaklucok level). Per BLUEPRINT §3.1 + §5.4, each FG line on a
/// ClientOrder gets its own PO once production starts. The migrator creates
/// one PO per ClientOrderFinishedGood to preserve that invariant.
///
/// One MaterialIssue per (Izdatnica + Item) pair so the per-row Proces=7
/// movement aggregates cleanly. DeliveryNote(ProducerDispatch) auto-creates
/// for the parent MaterialIssue per BLUEPRINT §3.8.
/// </summary>
internal sealed class MaterialIssueMapper
{
    private readonly MigrationContext _ctx;
    public MaterialIssueMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run()
    {
        Console.WriteLine("[issues] starting");
        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var itemByCode = LoadItemsByCode(lon);
        var existingPOs = LoadExistingPOs(lon);

        // 1) Create one ProductionOrder per ClientOrderFinishedGood.
        var fgSql = $"""
                     SELECT OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr, ArtKatBr, ArtNaziv,
                            Kol, EdMer
                       FROM GotoviProizvodi
                      WHERE 1=1{_ctx.ZaklucokWhere()}
                     """;
        using var fgCmd = new SqlCommand(fgSql, legacy);
        _ctx.AddZaklucokParam(fgCmd);
        using var fgRd = fgCmd.ExecuteReader();
        int poCreated = 0, poSkipped = 0;
        var poByCoFg = new Dictionary<(int Od, string Zb, int Gp), Guid>();
        while (fgRd.Read())
        {
            var od = AsInt(fgRd["OdobrenieRBr"]);
            var zb = AsStringOrEmpty(fgRd["ZaklucokBroj"]);
            var gp = AsInt(fgRd["GotovProizvodRBr"]);
            var artCode = AsString(fgRd["ArtKatBr"]) ?? "";
            var qty = AsDecimal(fgRd["Kol"]);
            var ed = AsString(fgRd["EdMer"]);

            if (!itemByCode.TryGetValue(artCode, out var fgItemId)) { poSkipped++; continue; }
            var clientOrderId = ClientOrderMapper.ResolveId(_ctx, od, zb);
            var poId = DeterministicGuid("PO", $"{clientOrderId}|{gp}");
            poByCoFg[(od, zb, gp)] = poId;
            var bomId = DeterministicGuid("BOM", $"{_ctx.TenantId}|{od}|{zb}|{gp}|{artCode}");
            var uomId = _ctx.UoMByCode.TryGetValue(ed ?? "", out var u) ? u : _ctx.DefaultUoMId;
            var poNumber = $"PO-O{od}-Z{zb}-GP{gp}";

            if (_ctx.DryRun) { poCreated++; continue; }

            _ctx.Exec(lon,
                """
                MERGE ProductionOrders AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    OrderNumber = @num, ItemId = @item, OrderQuantity = @q, UoMId = @uom,
                    Status = 4, BOMId = @bom, ClientOrderId = @co,
                    ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, OrderNumber, ItemId, OrderQuantity,
                    ProducedQuantity, ScrapQuantity, UoMId, Status, PlannedStartDate, PlannedEndDate,
                    BOMId, ClientOrderId, CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @num, @item, @q, @q, 0, @uom, 4, @plan, @plan,
                            @bom, @co, SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", poId),
                ("@tenant", _ctx.TenantId),
                ("@num", poNumber),
                ("@item", fgItemId),
                ("@q", qty),
                ("@uom", uomId),
                ("@plan", DateTime.UtcNow.Date),
                ("@bom", bomId),
                ("@co", clientOrderId));
            poCreated++;
        }
        fgRd.Close();
        Console.WriteLine($"[issues] ProductionOrders created={poCreated} skipped(missing item)={poSkipped}");

        // 2) Create one MaterialIssue per (Zaklucok, DokRBr=IzdatnicaRBr, Item)
        //    by aggregating Proces=7 LagerMaterijali rows.
        var issueSql = $"""
                       SELECT OdobrenieRBr, ZaklucokBroj, DokRBr, GotovProizvodRBr, ArtKatBrMat,
                              SUM(CAST(Kol AS decimal(18,4))) AS Qty,
                              MIN(EdMerMat) AS EdMer, MIN(LagerDatum) AS Datum
                         FROM LagerMaterijali
                        WHERE Proces = 7 AND ArtKatBrMat IS NOT NULL{_ctx.ZaklucokWhere()}
                        GROUP BY OdobrenieRBr, ZaklucokBroj, DokRBr, GotovProizvodRBr, ArtKatBrMat
                       """;
        using var isCmd = new SqlCommand(issueSql, legacy);
        _ctx.AddZaklucokParam(isCmd);
        using var isRd = isCmd.ExecuteReader();
        int miCreated = 0, miMissingItem = 0, miMissingPO = 0;
        var izdatnicaIds = new HashSet<(int Od, string Zb, int Dok)>();
        var miLines = new List<(int Od, string Zb, int Dok, Guid PoId, Guid ItemId, decimal Qty, Guid Uom, string Code, DateTime When)>();
        while (isRd.Read())
        {
            var od = AsInt(isRd["OdobrenieRBr"]);
            var zb = AsStringOrEmpty(isRd["ZaklucokBroj"]);
            var dok = AsInt(isRd["DokRBr"]);
            var gp = AsInt(isRd["GotovProizvodRBr"]);
            var code = AsString(isRd["ArtKatBrMat"]) ?? "";
            var qty = AsDecimal(isRd["Qty"]);
            var ed = AsString(isRd["EdMer"]);
            var when = AsDate(isRd["Datum"]) ?? DateTime.UtcNow.Date;

            if (!itemByCode.TryGetValue(code, out var itemId)) { miMissingItem++; continue; }
            if (!poByCoFg.TryGetValue((od, zb, gp), out var poId)) { miMissingPO++; continue; }
            var uom = _ctx.UoMByCode.TryGetValue(ed ?? "", out var u) ? u : _ctx.DefaultUoMId;
            izdatnicaIds.Add((od, zb, dok));
            miLines.Add((od, zb, dok, poId, itemId, qty, uom, code, when));
        }
        isRd.Close();

        // Pull Izdatnica metadata to set IssueNumber (defaults to LEG-IZD-<rbr>).
        var izdNumByRbr = LoadIzdatnicaMetadata(legacy, izdatnicaIds);

        foreach (var l in miLines)
        {
            var miId = DeterministicGuid("MaterialIssue",
                $"{_ctx.TenantId}|{l.Od}|{l.Zb}|{l.Dok}|{l.Code}");
            // LON's MaterialIssue has UNIQUE (TenantId, IssueNumber) — one row
            // per (Izdatnica × Item) suffixes the IssueNumber with a short
            // hash of the item code so each row carries a distinct number.
            var baseIzd = izdNumByRbr.TryGetValue(l.Dok, out var nm) && !string.IsNullOrWhiteSpace(nm)
                ? nm! : $"LEG-IZD-{l.Dok}";
            var izdNumber = $"{baseIzd}-{ShortHash(l.Code)}";
            var batch = string.IsNullOrEmpty(l.Zb) ? null : l.Zb;

            if (_ctx.DryRun) { miCreated++; continue; }

            _ctx.Exec(lon,
                """
                MERGE MaterialIssues AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    IssueNumber = @num, IssueDate = @date, ProductionOrderId = @po,
                    ItemId = @item, BatchNumber = @batch, Quantity = @q, UoMId = @uom,
                    ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, IssueNumber, IssueDate, ProductionOrderId,
                    ItemId, BatchNumber, MRN, Quantity, UoMId, CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @num, @date, @po, @item, @batch, NULL, @q, @uom,
                            SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", miId),
                ("@tenant", _ctx.TenantId),
                ("@num", izdNumber),
                ("@date", l.When),
                ("@po", l.PoId),
                ("@item", l.ItemId),
                ("@batch", (object?)batch ?? DBNull.Value),
                ("@q", l.Qty),
                ("@uom", l.Uom));
            miCreated++;

            // Auto-gen DeliveryNote(ProducerDispatch) — one per (Izdatnica, line).
            CreateDeliveryNoteForIssue(lon, miId, l);
        }

        Console.WriteLine($"[issues] MaterialIssues created={miCreated} missingItem={miMissingItem} missingPO={miMissingPO}");
        return 0;
    }

    private void CreateDeliveryNoteForIssue(SqlConnection lon,
        Guid miId,
        (int Od, string Zb, int Dok, Guid PoId, Guid ItemId, decimal Qty, Guid Uom, string Code, DateTime When) l)
    {
        // One header per (Zaklucok, DokRBr); reuse if already created.
        var dnHeaderId = DeterministicGuid("DeliveryNote", $"{_ctx.TenantId}|{l.Od}|{l.Zb}|{l.Dok}");
        var dnNumber = $"DN-LEG-{l.Dok:D6}";

        if (_ctx.DryRun) return;

        _ctx.Exec(lon,
            """
            MERGE DeliveryNotes AS T
            USING (SELECT @id AS Id) S ON T.Id = S.Id
            WHEN MATCHED THEN UPDATE SET
                Number = @num, DocumentType = 1, RelatedDocumentId = @rel,
                DispatchDate = @date, Status = 3,
                FromLocationId = @loc, ToPartnerId = NULL,
                ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
            WHEN NOT MATCHED THEN INSERT (Id, TenantId, Number, DocumentType, RelatedDocumentId,
                DispatchDate, FromLocationId, ToLocationId, ToPartnerId, Status,
                CreatedAt, CreatedBy, IsDeleted)
                VALUES (@id, @tenant, @num, 1, @rel, @date, @loc, NULL, NULL, 3,
                        SYSUTCDATETIME(), 'migration', 0);
            """,
            ("@id", dnHeaderId),
            ("@tenant", _ctx.TenantId),
            ("@num", dnNumber),
            ("@rel", miId),
            ("@date", l.When),
            ("@loc", _ctx.DefaultReceivingLocationId!.Value));

        var dnLineId = DeterministicGuid("DeliveryNoteLine", $"{dnHeaderId}|{l.Code}");
        _ctx.Exec(lon,
            """
            MERGE DeliveryNoteLines AS T
            USING (SELECT @id AS Id) S ON T.Id = S.Id
            WHEN MATCHED THEN UPDATE SET
                DeliveryNoteId = @dn, ItemId = @item, Description = @desc,
                Quantity = @q, UoMId = @uom, BatchNumber = @batch,
                ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
            WHEN NOT MATCHED THEN INSERT (Id, TenantId, DeliveryNoteId, ItemId, Description,
                Quantity, UoMId, BatchNumber, MRN, CreatedAt, CreatedBy, IsDeleted)
                VALUES (@id, @tenant, @dn, @item, @desc, @q, @uom, @batch, NULL,
                        SYSUTCDATETIME(), 'migration', 0);
            """,
            ("@id", dnLineId),
            ("@tenant", _ctx.TenantId),
            ("@dn", dnHeaderId),
            ("@item", l.ItemId),
            ("@desc", l.Code),
            ("@q", l.Qty),
            ("@uom", l.Uom),
            ("@batch", string.IsNullOrEmpty(l.Zb) ? DBNull.Value : (object)l.Zb));
    }

    private Dictionary<int, string?> LoadIzdatnicaMetadata(SqlConnection legacy, HashSet<(int Od, string Zb, int Dok)> ids)
    {
        var map = new Dictionary<int, string?>();
        if (ids.Count == 0) return map;
        var distinctRbrs = ids.Select(x => x.Dok).Distinct().ToList();
        var inClause = string.Join(',', distinctRbrs);
        using var cmd = new SqlCommand(
            $"SELECT IzdatnicaRBr, IzdatnicaBroj FROM Izdatnici WHERE IzdatnicaRBr IN ({inClause})", legacy);
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
            map[AsInt(rd["IzdatnicaRBr"])] = AsString(rd["IzdatnicaBroj"]);
        return map;
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

    private HashSet<Guid> LoadExistingPOs(SqlConnection lon)
    {
        var set = new HashSet<Guid>();
        using var cmd = new SqlCommand("SELECT Id FROM ProductionOrders WHERE TenantId=@t AND IsDeleted=0", lon);
        cmd.Parameters.AddWithValue("@t", _ctx.TenantId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) set.Add(r.GetGuid(0));
        return set;
    }

    /// <summary>Short hex hash for item-code suffixes on IssueNumber (8 chars).</summary>
    private static string ShortHash(string s)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes, 0, 4); // 8 hex chars
    }
}
