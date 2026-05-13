using Microsoft.Data.SqlClient;
using static LON.Migration.Helpers;

namespace LON.Migration.Mappers;

/// <summary>
/// Phase 17 §E.MIGRATE — LagerMaterijali → InventoryMovement + recomputed
/// InventoryBalance per MAPPING.md §4.1 + §11.1 DocumentSource resolver.
///
/// Proces → MovementType + RelatedDocument:
///   Proces=1  → Receipt   (no exit doc; references FakturaU5Broj for IM linkage)
///   Proces=6  → Adjustment (rare WIP)
///   Proces=7  → Issue / ProductionIssue (DokRBr → Izdatnici.IzdatnicaRBr)
///   Proces=8  → Return    (DokRBr → Izdatnici return voucher)
///   Proces=9  → Shipment  (DokRBr → Ispratnici.IspratnicaRBr; treated as
///                          "waste destruction shipment" to align with the
///                          CustomsDeclaration(type=Waste) created by
///                          WasteDeclarationMapper)
///
/// After all movements imported, InventoryBalance is recomputed by replay:
///   Quantity = Σ Receipt − Σ (Issue+ProductionIssue+Shipment) on each
///   (Item, Location, Batch, MRN, UoM, QualityStatus) bucket.
/// </summary>
internal sealed class InventoryMapper
{
    private readonly MigrationContext _ctx;
    public InventoryMapper(MigrationContext ctx) => _ctx = ctx;

    public int Run(int limit)
    {
        Console.WriteLine("[inventory] starting");

        if (_ctx.DefaultReceivingLocationId == null)
        {
            Console.Error.WriteLine("[inventory] no receiving location seeded for tenant; aborting");
            return 4;
        }

        using var legacy = _ctx.OpenLegacy();
        using var lon = _ctx.OpenLon();

        var itemByCode = LoadItemsByCode(lon);
        Console.WriteLine($"[inventory] item map size={itemByCode.Count}");

        // 1) Per-row movements.
        string top = limit > 0 ? $"TOP {limit}" : "";
        var sql = $"""
                  SELECT {top} LagerRBr, LagerDatum, Proces, DokRBr, FakturaU5Broj, FakturaU5RBr,
                         OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr, Proizvoditel,
                         ArtKatBrMat, Kol, EdMerMat AS EdMer, ZemjaPoteklo
                    FROM LagerMaterijali
                   WHERE ArtKatBrMat IS NOT NULL{_ctx.ZaklucokWhere()}
                   ORDER BY LagerRBr
                  """;
        using var sel = new SqlCommand(sql, legacy);
        _ctx.AddZaklucokParam(sel);
        sel.CommandTimeout = 600;

        using var rd = sel.ExecuteReader();
        int total = 0, written = 0, missingItem = 0;
        var orderRecvLoc = _ctx.DefaultReceivingLocationId!.Value;
        var orderProdLoc = _ctx.DefaultProductionLocationId ?? orderRecvLoc;
        while (rd.Read())
        {
            total++;
            var lagerRBr = AsInt(rd["LagerRBr"]);
            var lagerDate = AsDate(rd["LagerDatum"]) ?? DateTime.UtcNow.Date;
            var proces = AsInt(rd["Proces"]);
            var dokRBr = AsInt(rd["DokRBr"]);
            var fakturaBroj = AsStringOrEmpty(rd["FakturaU5Broj"]);
            var odRBr = AsInt(rd["OdobrenieRBr"]);
            var zb = AsStringOrEmpty(rd["ZaklucokBroj"]);
            var artCode = AsString(rd["ArtKatBrMat"]) ?? string.Empty;
            var qty = AsDecimal(rd["Kol"]);
            var ed = AsString(rd["EdMer"]);

            if (qty == 0m || string.IsNullOrWhiteSpace(artCode)) continue;
            if (!itemByCode.TryGetValue(artCode, out var itemId)) { missingItem++; continue; }

            int movementType = proces switch
            {
                1 => 1,  // Receipt
                6 => 4,  // Adjustment
                7 => 6,  // ProductionIssue
                8 => 8,  // Return
                9 => 7,  // Shipment (waste destruction)
                _ => 4,  // Adjustment fallback
            };

            // From/To location per movement type:
            //   Receipt: into receiving location.
            //   ProductionIssue: from receiving → producer "out" (set ToLocation=null).
            //   Shipment: from production → null (left the warehouse).
            //   Return: into production location.
            Guid? fromLoc = null, toLoc = null;
            switch (movementType)
            {
                case 1: toLoc = orderRecvLoc; break;
                case 6: fromLoc = orderRecvLoc; break;
                case 7: fromLoc = orderProdLoc; break;
                case 8: toLoc = orderProdLoc; break;
                default: toLoc = orderRecvLoc; break;
            }

            // Reference number captures legacy linkage:
            //   For Receipt: FakturaU5Broj (the IM declaration)
            //   For ProductionIssue: 'IZD-{dokRBr}' (Izdatnica)
            //   For Shipment: 'ISP-{dokRBr}' (Ispratnica)
            string referenceNumber = proces switch
            {
                1 => $"IM-{fakturaBroj}",
                7 => $"IZD-{dokRBr}",
                9 => $"ISP-{dokRBr}",
                _ => $"LEG-{lagerRBr}",
            };

            // ReferenceId resolves the parent business document when possible.
            Guid? referenceId = proces switch
            {
                7 => DeterministicGuid("MaterialIssue", $"{_ctx.TenantId}|{odRBr}|{zb}|{dokRBr}"),
                9 => DeterministicGuid("WasteDecl", $"{_ctx.TenantId}|{odRBr}|{zb}|{dokRBr}"),
                _ => (Guid?)null,
            };

            var batch = string.IsNullOrEmpty(zb) ? null : zb;
            var mrn = string.IsNullOrEmpty(fakturaBroj) ? null : $"LEG-{fakturaBroj}";
            var uomId = _ctx.UoMByCode.TryGetValue(ed ?? "", out var u) ? u : _ctx.DefaultUoMId;
            var id = DeterministicGuid("InvMov", $"{_ctx.TenantId}|{lagerRBr}");
            var movNumber = $"M-{lagerRBr}";

            if (_ctx.DryRun) { written++; continue; }

            _ctx.Exec(lon,
                """
                MERGE InventoryMovements AS T
                USING (SELECT @id AS Id) S ON T.Id = S.Id
                WHEN MATCHED THEN UPDATE SET
                    MovementNumber = @num, MovementDate = @date, [Type] = @t, ItemId = @item,
                    BatchNumber = @batch, MRN = @mrn, FromLocationId = @fromLoc, ToLocationId = @toLoc,
                    Quantity = @q, UoMId = @uom, ReferenceNumber = @refN, ReferenceId = @refId,
                    Notes = @notes, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'migration', IsDeleted = 0
                WHEN NOT MATCHED THEN INSERT (Id, TenantId, MovementNumber, MovementDate, [Type],
                    ItemId, BatchNumber, MRN, FromLocationId, ToLocationId, Quantity, UoMId,
                    ReferenceNumber, ReferenceId, Notes, CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @num, @date, @t, @item, @batch, @mrn,
                            @fromLoc, @toLoc, @q, @uom, @refN, @refId, @notes,
                            SYSUTCDATETIME(), 'migration', 0);
                """,
                ("@id", id),
                ("@tenant", _ctx.TenantId),
                ("@num", movNumber),
                ("@date", lagerDate),
                ("@t", movementType),
                ("@item", itemId),
                ("@batch", (object?)batch ?? DBNull.Value),
                ("@mrn", (object?)mrn ?? DBNull.Value),
                ("@fromLoc", (object?)fromLoc ?? DBNull.Value),
                ("@toLoc", (object?)toLoc ?? DBNull.Value),
                ("@q", qty),
                ("@uom", uomId),
                ("@refN", referenceNumber),
                ("@refId", (object?)referenceId ?? DBNull.Value),
                ("@notes", $"[LEGACY Proces={proces} OdobrenieRBr={odRBr} Zaklucok={zb} DokRBr={dokRBr}]"));
            written++;
        }
        rd.Close();

        Console.WriteLine($"[inventory] movements done total={total} written={written} missingItem={missingItem}");

        // 2) Recompute InventoryBalance from the movements we just inserted
        //    (scoped to this tenant + any zaklucok-tagged batch).
        if (!_ctx.DryRun)
        {
            string zwhere = string.IsNullOrEmpty(_ctx.ZaklucokFilter)
                ? string.Empty
                : " AND BatchNumber = @zb ";
            // Drop existing balances scoped to the filter so re-runs start clean.
            using (var del = new SqlCommand(
                "DELETE FROM InventoryBalances WHERE TenantId = @t" + zwhere, lon))
            {
                del.Parameters.AddWithValue("@t", _ctx.TenantId);
                _ctx.AddZaklucokParam(del);
                del.ExecuteNonQuery();
            }

            // Sign convention: Receipt (+) ; Issue / ProductionIssue / Shipment (−); Return (+);
            // Adjustment passes through (we keep it neutral here).
            string filterClause = string.IsNullOrEmpty(_ctx.ZaklucokFilter)
                ? string.Empty
                : " AND BatchNumber = @zb ";
            var sumSql = $"""
                         SELECT ItemId, COALESCE(ToLocationId, FromLocationId) AS LocId,
                                BatchNumber, MRN, UoMId,
                                SUM(CASE
                                    WHEN [Type] IN (1, 8) THEN Quantity
                                    WHEN [Type] IN (2, 6, 7) THEN -Quantity
                                    ELSE 0 END) AS NetQty
                           FROM InventoryMovements
                          WHERE TenantId = @t AND IsDeleted = 0{filterClause}
                          GROUP BY ItemId, COALESCE(ToLocationId, FromLocationId), BatchNumber, MRN, UoMId
                         HAVING SUM(CASE
                                    WHEN [Type] IN (1, 8) THEN Quantity
                                    WHEN [Type] IN (2, 6, 7) THEN -Quantity
                                    ELSE 0 END) > 0.0001
                         """;
            using var sumCmd = new SqlCommand(sumSql, lon);
            sumCmd.Parameters.AddWithValue("@t", _ctx.TenantId);
            _ctx.AddZaklucokParam(sumCmd);
            using var sumRd = sumCmd.ExecuteReader();
            var rows = new List<(Guid Item, Guid? Loc, string? Batch, string? Mrn, Guid Uom, decimal Qty)>();
            while (sumRd.Read())
            {
                rows.Add((
                    sumRd.GetGuid(0),
                    sumRd.IsDBNull(1) ? null : sumRd.GetGuid(1),
                    sumRd.IsDBNull(2) ? null : sumRd.GetString(2),
                    sumRd.IsDBNull(3) ? null : sumRd.GetString(3),
                    sumRd.GetGuid(4),
                    sumRd.GetDecimal(5)));
            }
            sumRd.Close();

            int balRows = 0;
            foreach (var row in rows)
            {
                var balId = DeterministicGuid("InvBal",
                    $"{_ctx.TenantId}|{row.Item}|{row.Loc}|{row.Batch}|{row.Mrn}|{row.Uom}");
                _ctx.Exec(lon,
                    """
                    INSERT INTO InventoryBalances (Id, TenantId, ItemId, LocationId, BatchNumber,
                        MRN, Quantity, UoMId, QualityStatus, LonProcessState,
                        CreatedAt, CreatedBy, IsDeleted)
                    VALUES (@id, @tenant, @item, @loc, @batch, @mrn, @q, @uom, 0, 1,
                        SYSUTCDATETIME(), 'migration', 0);
                    """,
                    ("@id", balId),
                    ("@tenant", _ctx.TenantId),
                    ("@item", row.Item),
                    ("@loc", row.Loc ?? (object)orderRecvLoc),
                    ("@batch", (object?)row.Batch ?? DBNull.Value),
                    ("@mrn", (object?)row.Mrn ?? DBNull.Value),
                    ("@q", row.Qty),
                    ("@uom", row.Uom));
                balRows++;
            }
            Console.WriteLine($"[inventory] recomputed {balRows} InventoryBalance rows");
        }

        return 0;
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
