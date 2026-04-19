-- KW12 reset — soft-delete all fictitious production/WMS/customs data in TEKSPORT
-- so we can reimport the KW12 file as the new baseline.
--
-- Principle: keep legacy master data (Items from P3 migration, Partners,
-- Warehouses, Locations, UoMs, LONAuthorizations, Tenants, reference codes).
-- Delete only transactional data + post-cleanup Items (created after
-- 2026-04-19 08:00 UTC = the start of today's KW12 work).
--
-- Usage on VPS:
--   docker exec lon-sqlserver /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -d LONDB \
--     -i /path/to/kw12_cleanup_teksport.sql

SET NOCOUNT ON;
DECLARE @tek UNIQUEIDENTIFIER = (SELECT Id FROM Tenants WHERE Code = 'TEKSPORT');
DECLARE @cut DATETIME2 = '2026-04-19T08:00:00';

-- ========== BEFORE COUNTS ==========
PRINT '=== BEFORE ===';
SELECT 'Receipts'                  AS T, COUNT(*) AS LiveRows FROM Receipts                  WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ReceiptLines',             COUNT(*) FROM ReceiptLines             WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'InventoryBalances',        COUNT(*) FROM InventoryBalances        WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'InventoryMovements',       COUNT(*) FROM InventoryMovements       WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionOrders',         COUNT(*) FROM ProductionOrders         WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionOrderMaterials', COUNT(*) FROM ProductionOrderMaterials WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionOrderOperations',COUNT(*) FROM ProductionOrderOperations WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'MaterialIssues',           COUNT(*) FROM MaterialIssues           WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionReceipts',       COUNT(*) FROM ProductionReceipts       WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'BOMs',                     COUNT(*) FROM BOMs                     WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'BOMLines',                 COUNT(*) FROM BOMLines                 WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'Shipments',                COUNT(*) FROM Shipments                WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ShipmentLines',            COUNT(*) FROM ShipmentLines            WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'CustomsDeclarations',      COUNT(*) FROM CustomsDeclarations      WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'CustomsDeclarationLines',  COUNT(*) FROM CustomsDeclarationLines  WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'MRNRegistries',            COUNT(*) FROM MRNRegistries            WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'GuaranteeLedgerEntries',   COUNT(*) FROM GuaranteeLedgerEntries   WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'TraceLinks',               COUNT(*) FROM TraceLinks               WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'BatchGenealogies',         COUNT(*) FROM BatchGenealogies         WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'Items (all live)',         COUNT(*) FROM Items                    WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'Items (post-KW12 cutoff)', COUNT(*) FROM Items                    WHERE TenantId=@tek AND IsDeleted=0 AND CreatedAt >= @cut
ORDER BY T;

-- ========== SOFT-DELETE TRANSACTIONAL DATA ==========
-- Order respects FK directionality (children first, then parents). With
-- soft-delete via IsDeleted=1 the FK is not physically dropped so the order
-- is informative, not required — but keeps reports consistent at any point.

UPDATE ProductionOrderMaterials  SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE ProductionOrderOperations SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE MaterialIssues            SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE ProductionReceipts        SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE ProductionOrders          SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;

UPDATE BOMLines                  SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE BOMs                      SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;

UPDATE ReceiptLines              SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE Receipts                  SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE InventoryMovements        SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE InventoryBalances         SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE ShipmentLines             SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE Shipments                 SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;

UPDATE CustomsDeclarationLines   SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE CustomsDeclarations       SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE MRNRegistries             SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;

UPDATE GuaranteeLedgerEntries    SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;

UPDATE TraceLinks                SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;
UPDATE BatchGenealogies          SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset' WHERE TenantId=@tek AND IsDeleted=0;

-- ========== ITEMS: DELETE ONLY POST-CUTOFF ADDITIONS ==========
-- Keep legacy items (from P3 ELON migration on 2026-04-19 ~00:35 UTC) —
-- these are the real 11k+ master data catalog. Delete everything added
-- after 08:00 UTC (the moment today's KW12 work began).
UPDATE Items
SET IsDeleted = 1, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = 'kw12-reset'
WHERE TenantId = @tek AND IsDeleted = 0 AND CreatedAt >= @cut;

-- ========== AFTER COUNTS ==========
PRINT '';
PRINT '=== AFTER ===';
SELECT 'Receipts'                  AS T, COUNT(*) AS LiveRows FROM Receipts                  WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ReceiptLines',             COUNT(*) FROM ReceiptLines             WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'InventoryBalances',        COUNT(*) FROM InventoryBalances        WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'InventoryMovements',       COUNT(*) FROM InventoryMovements       WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionOrders',         COUNT(*) FROM ProductionOrders         WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionOrderMaterials', COUNT(*) FROM ProductionOrderMaterials WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionOrderOperations',COUNT(*) FROM ProductionOrderOperations WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'MaterialIssues',           COUNT(*) FROM MaterialIssues           WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ProductionReceipts',       COUNT(*) FROM ProductionReceipts       WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'BOMs',                     COUNT(*) FROM BOMs                     WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'BOMLines',                 COUNT(*) FROM BOMLines                 WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'Shipments',                COUNT(*) FROM Shipments                WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'ShipmentLines',            COUNT(*) FROM ShipmentLines            WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'CustomsDeclarations',      COUNT(*) FROM CustomsDeclarations      WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'CustomsDeclarationLines',  COUNT(*) FROM CustomsDeclarationLines  WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'MRNRegistries',            COUNT(*) FROM MRNRegistries            WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'GuaranteeLedgerEntries',   COUNT(*) FROM GuaranteeLedgerEntries   WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'TraceLinks',               COUNT(*) FROM TraceLinks               WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'BatchGenealogies',         COUNT(*) FROM BatchGenealogies         WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'Items (all live)',         COUNT(*) FROM Items                    WHERE TenantId=@tek AND IsDeleted=0
UNION ALL SELECT 'Items (post-KW12 cutoff)', COUNT(*) FROM Items                    WHERE TenantId=@tek AND IsDeleted=0 AND CreatedAt >= @cut
ORDER BY T;
