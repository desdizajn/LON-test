# TEKSPORT_WIPE_PLAN — clean slate before Phase 17 E0

> Generated 2026-05-11 by Phase 17 PREP session. **NOT executed.** Requires explicit user approval and full DB backup before any wipe runs.

---

## §0 — Target environment (resolved)

The PREP session discovered that **there is no LON database on the local SQL Server**. The `Texport` DB present locally is the production Nebim V3 ERP (Turkish apparel ERP) of the customer, completely unrelated to LON. CLAUDE.md §4 referenced a local LON dev DB named `Teksport`, but `sys.databases` shows no such DB and `appsettings.Development.json` points at `Server=localhost;Database=LONDB;Integrated Security=True` — also absent locally.

**Wipe target = VPS-hosted `LONDB`** (Docker SQL Server inside `/opt/apps/LON/LON-test` compose stack, accessed via `root@173.212.254.216`). The PREP session does NOT touch the VPS; this plan is a design document for a separate execute-wipe session that the user will explicitly approve.

Pre-conditions before the wipe-execute session may run:
1. User confirms VPS is the correct target (no surprise local-dev workflow we missed).
2. User pulls a full BACKUP of `LONDB` to `/opt/apps/LON/backups/LONDB_pre-wipe_<UTC-timestamp>.bak` (see §6).
3. User confirms the seed credentials (admin password) source (env var injection vs interactive prompt).

If a local-dev DB is also desired (e.g. for offline iteration), it can be bootstrapped fresh with `dotnet ef database update --project src/LON.Infrastructure --startup-project src/LON.API` and then seeded with the same script — no wipe needed for the empty case.

---

## §1 — Migration baseline

- Migration count in `src/LON.Infrastructure/Migrations/` (excluding *.Designer.cs): **51** as of 2026-05-11.
- CLAUDE.md / PLAN.md state "43 migrations"; this is stale and should be updated post-Phase-17.prep commit. The eight extras are the Phase 16 C1/C2/C3a/C3b/C3c entities plus three earlier Phase 15 migrations that were added after the doc was last touched.
- Latest migration: `20260511111516_P16_C3c_AddSupplierInvoice`.

The wipe assumes the DB is fully migrated to head before the wipe runs. If the VPS DB is behind, run `dotnet ef database update` on VPS first.

---

## §2 — Truncation order (FK-respecting)

Derived from `IApplicationDbContext` (79 entity DbSets) + EF migrations. Wipe must traverse **leaf-to-root** so that no FK is violated mid-wipe. Truncate runs ahead of identity reseed; DELETE FROM is used where TRUNCATE is blocked by FKs (SQL Server will reject TRUNCATE on a referenced table even when childless).

Recommended approach for safety + speed:

```sql
USE LONDB;
SET XACT_ABORT ON;
BEGIN TRAN WipeAll;

-- 1) Disable all FK constraints (faster than honouring order strictly)
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- 2) DELETE everything except __EFMigrationsHistory and tables we explicitly seed
EXEC sp_MSforeachtable 'IF ''?'' NOT LIKE ''%__EFMigrationsHistory%'' DELETE FROM ?';

-- 3) Reset identity (skip for Guid PKs; the few int-identity tables get DBCC CHECKIDENT)
-- (See §3 for exact list.)

-- 4) Re-enable FKs WITH CHECK to ensure clean integrity
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

COMMIT;
```

Alternative (TRUNCATE per table in dependency order): listed below for the audit trail. Order derived by topological sort over `sys.foreign_keys`. **Run from top to bottom.**

### Group A — Audit, history, transient (no children)

- AuditLogEntries
- UserFieldHistories
- GuaranteeBalanceSnapshots
- InventoryMovements
- BatchGenealogies
- TraceLinks
- MachineStateEvents
- DowntimeEvents
- MaintenanceWorkOrders
- KnowledgeDocumentChunks
- ImportSessions  *(P5.1 audit of bulk imports)*

### Group B — Domain leaves (line tables, child rows)

- ReceiptLines
- ShipmentLines
- TransferLines
- CycleCountLines
- PickTasks
- PickingWaves
- InvoiceLines
- RateCardEntries
- ProductionOrderOperations
- ProductionOrderMaterialSizes
- ProductionOrderMaterials
- BOMLines
- RoutingOperations
- LONAuthorizationItems
- CustomsDeclarationLines
- CustomsDocuments
- MRNRegistries
- DutyCalculations
- GuaranteeLedgerEntries
- TariffCodeRates
- DeclarationRules
- KnowledgeDocuments
- PayrollLines
- ProductionReceipts
- MaterialIssues
- Skarts
- Absences
- AttendanceRecords
- OperatorMachineAssignments
- MaintenanceSchedules
- ItemUoMConversions
- CustomsProcedureDocuments
- EmployeeCertifications  *(P16.C2)*

### Group C — Domain headers

- Receipts
- Shipments
- Transfers
- CycleCounts
- Invoices
- ClientContracts
- ProductionOrders
- BOMs
- Routings
- CustomsDeclarations
- LONAuthorizations
- GuaranteeAccounts
- ImportMappingProfiles
- PayrollPeriods
- CostRates
- SupplierInvoices
- RiskRegisterItems  *(P16.C1)*
- InventoryBalances
- Machines
- Locations

### Group D — Master data + reference (will be re-seeded immediately in §5)

- Items
- Partners
- WorkCenters
- Warehouses
- UnitsOfMeasure
- Shifts
- TariffCodes
- CustomsRegulations
- CustomsProcedures
- CodeListItems
- Employees

### Group E — Auth + RBAC (will be re-seeded immediately in §5)

- RolePermissions
- UserRoles
- Permissions
- Roles
- Users

### Group F — Tenant root (single row re-seeded in §5)

- Tenants

`__EFMigrationsHistory` is **never touched.**

---

## §3 — Identity reseed

LON uses `Guid` PKs almost universally. The only tables with `int IDENTITY` PKs are the ones flagged in `OnModelCreating` as `ValueGeneratedOnAdd()` over `int` — to be confirmed at execute-wipe time with:

```sql
SELECT t.name AS TableName, c.name AS ColumnName, IDENT_CURRENT(t.name) AS CurrentIdent
FROM sys.tables t
JOIN sys.identity_columns c ON c.object_id = t.object_id
ORDER BY t.name;
```

For each row returned, run:

```sql
DBCC CHECKIDENT ('<TableName>', RESEED, 0);
```

(Most likely set: empty. Logging entities like AuditLogEntry are Guid-keyed; line tables likewise.)

---

## §4 — Tables to preserve

**None** for a v1 clean slate. Even reference data (TariffCodes, CodeListItems) is wiped and re-seeded with a curated minimal v1 set drawn from ELON's `DrzavaKor` (240 countries), `EdMerKor` (34 UoMs) and a hand-picked tariff subset.

`__EFMigrationsHistory` is the sole exception — preserved verbatim so EF doesn't think the DB is fresh.

---

## §5 — Seed script (runs immediately after the wipe in the same transaction)

The execute-wipe session will produce a `scripts/seed-v1.sql` file. This plan specifies its contents; the script itself is out of scope here.

### 5.1 Tenant

```sql
INSERT INTO Tenants (Id, Code, Name, CreatedAt, IsActive)
VALUES ('00000000-0000-0000-0000-000000000001', 'TEKSPORT',
        'TEKSPORT — Production lead tenant', SYSUTCDATETIME(), 1);
```

(Existing handlers and unit tests reference TEKSPORT by name + sentinel-zeros tenant id; preserve it across wipes so test-data and integration tests do not need rewrites.)

### 5.2 Roles (12 from BLUEPRINT §4.1)

Per BLUEPRINT §4.1: Administrator, Manager, ProductionPlanner, WarehouseOperator, CustomsOfficer, FinanceClerk, QCInspector, OperationsAnalyst, ReadOnlyAuditor, Subcontractor *(Phase 18 stub)*, Speditor *(Phase 19 stub)*, Operator.

Use deterministic GUIDs (e.g. `00000000-...-0010` through `0021`) so handler-side fixture lookups stay stable.

### 5.3 Permissions + RolePermissions

Source of truth = `src/LON.Application/Common/Authorization/Permissions.cs` (or equivalent). The seed script enumerates all permission strings and inserts (a) one `Permissions` row per name, (b) `RolePermissions` rows per BLUEPRINT §4.1 matrix.

Administrator gets every permission. Operator + ReadOnlyAuditor get the narrow subset BLUEPRINT specifies. Subcontractor + Speditor remain stub rows (no RolePermissions in v1 — Phase 18/19 fills these in).

### 5.4 Administrator user

```sql
DECLARE @pwd NVARCHAR(MAX) = N'$2a$11$...'; -- BCrypt hash, computed offline from env var LON_BOOTSTRAP_ADMIN_PASSWORD
INSERT INTO Users (Id, TenantId, Username, PasswordHash, Email, IsActive, CreatedAt)
VALUES ('00000000-0000-0000-0000-000000000100',
        '00000000-0000-0000-0000-000000000001',
        'admin', @pwd, 'admin@teksport.local', 1, SYSUTCDATETIME());

INSERT INTO UserRoles (UserId, RoleId, AssignedAt)
SELECT '00000000-0000-0000-0000-000000000100', Id, SYSUTCDATETIME()
FROM Roles WHERE Name = 'Administrator';
```

**No hard-coded password in the script.** The execute-wipe session prompts for `LON_BOOTSTRAP_ADMIN_PASSWORD` (env var) or interactive, hashes with BCrypt cost 11 via a helper `dotnet run --project tools/HashPassword`, and only then runs the seed.

### 5.5 CodeListItem reference

Minimum v1 set, drawn from ELON DrzavaKor + EdMerKor:

- **Currency**: EUR, USD, MKD, RSD (matches FakturiU5 only-EUR finding; USD/MKD/RSD added for future-proofing without auto-FX).
- **CountryOfOrigin**: top-30 by frequency in `FakturiU5.ZemjaPoteklo` (PREP recon found 30 distinct codes — AT, DE, BG, CN, TR, IT, TW, BE, US, NL, GB, FR, HK, JP, PK, plus ~15 long-tail). Seed the full set, marking each `IsActive=1`.
- **UoMCategory** + `UnitsOfMeasure` rows: the three actually used (`PCS`, `MTR`, `PRS`) plus the standard set (`KGM`, `LTR`) for future use. Names sourced from EdMerKor with Cyrillic transliteration fix (`localStorage` PREP run revealed the local DB stores names in CP1251-mangled Cyrillic — re-encode in seed script).
- **PreferentialOrigin**: empty for v1; populated post-migration.

### 5.6 Default WorkCenters

Placeholders for Phase 17 wiring; details come from Teksport actual factory layout (PREP did not have access to live floor plan).

- `WC-CUT-01` Cutting
- `WC-SEW-01` Sewing (line 1)
- `WC-SEW-02` Sewing (line 2)
- `WC-FIN-01` Finishing
- `WC-PCK-01` Packaging

Linked to tenant TEKSPORT. Machines table stays empty (Phase 17/22 populates).

### 5.7 Default CustomsProcedures

- `4051` — Inward processing import (legacy "увоз за облагородување")
- `1041` — Inward processing export (re-export)
- `6121` — Razdolzuvanje (seeded by migration `P26b_Seed6121Procedure`, will be re-seeded by this script too)
- `4200` — Free-circulation import (seeded by migration `AddDeclarationStatusAndProcedureCode4200`)

Codes only — DeclarationRules and CustomsRegulations stay empty until Phase 17 RAG-seed.

---

## §6 — Mandatory pre-wipe backup

The execute-wipe session must produce a backup BEFORE the BEGIN TRAN runs:

```bash
ssh root@173.212.254.216 \
  'docker exec lon-test-sqlserver-1 /opt/mssql-tools/bin/sqlcmd \
     -S localhost -U sa -P "$SA_PASSWORD" \
     -Q "BACKUP DATABASE LONDB TO DISK = N''/var/opt/mssql/backup/LONDB_pre-wipe_'$(date -u +%Y%m%dT%H%M%SZ)'.bak'' WITH INIT, COMPRESSION"'
```

Backup is then copied to the VPS host filesystem (`scp` to `/opt/apps/LON/backups/`). Wipe execution is GATED on a successful `RESTORE VERIFYONLY` against the backup file.

Rollback plan if wipe goes wrong:

```bash
RESTORE DATABASE LONDB FROM DISK = N'/var/opt/mssql/backup/LONDB_pre-wipe_<timestamp>.bak'
  WITH REPLACE, RECOVERY;
```

---

## §7 — Post-wipe verification

```sql
-- All business tables must be empty
DECLARE @nonEmpty INT = 0;
SELECT @nonEmpty = COUNT(*) FROM (
  SELECT t.name, SUM(p.rows) AS rws
  FROM sys.tables t
  INNER JOIN sys.partitions p ON p.object_id=t.object_id
  WHERE p.index_id IN (0,1)
    AND t.name NOT IN (
      'Tenants','Users','Roles','Permissions','UserRoles','RolePermissions',
      'CustomsProcedures','CodeListItems','UnitsOfMeasure','WorkCenters',
      '__EFMigrationsHistory'
    )
  GROUP BY t.name HAVING SUM(p.rows) > 0
) q;
SELECT @nonEmpty AS UnexpectedNonEmptyTables;  -- must be 0
```

```sql
-- Seed audit
SELECT 'Tenants', COUNT(*) FROM Tenants
UNION ALL SELECT 'Roles', COUNT(*) FROM Roles
UNION ALL SELECT 'Permissions', COUNT(*) FROM Permissions
UNION ALL SELECT 'RolePermissions', COUNT(*) FROM RolePermissions
UNION ALL SELECT 'Users (admin)', COUNT(*) FROM Users WHERE Username='admin'
UNION ALL SELECT 'WorkCenters', COUNT(*) FROM WorkCenters
UNION ALL SELECT 'CustomsProcedures', COUNT(*) FROM CustomsProcedures
UNION ALL SELECT 'CodeListItems', COUNT(*) FROM CodeListItems;
```

Expected:

- `Tenants` = 1 (TEKSPORT)
- `Roles` = 12
- `Permissions` >= 60 (matches enum count in `Permissions.cs`)
- `Users` = 1 admin
- `WorkCenters` = 5
- `CustomsProcedures` = 4
- `CodeListItems` >= 30 (currencies + origins + UoMs)

Login smoke from VPS shell: `curl -s -X POST https://elon.elbosoft.click/api/auth/login -d '{"Username":"admin","Password":"..."}' -H "Content-Type: application/json"` → 200 + JWT.

---

## §8 — What the wipe does NOT do

- Does not drop the DB or schema (only DELETEs row data).
- Does not modify `__EFMigrationsHistory` — EF still considers all 51 migrations applied.
- Does not touch the VPS file system outside of `/var/opt/mssql/backup/`.
- Does not modify deploy scripts, env files, or Caddy config.

---

## §9 — Awaiting user decision before execute-wipe session

- **Approval to wipe VPS LONDB.** This destroys all Phase 0–16 test data. Acceptable trade for a Phase 17 fresh-start ClientOrder hub. **User confirms?**
- **Source of admin password.** Env var (preferred for automation), interactive prompt (preferred for one-off), or rotate-after-bootstrap (admin sets new password on first login)?
- **Whether to also bootstrap a local LON dev DB** (`dotnet ef database update` on `localhost\LONDB`) in the same session, so offline iteration is possible. Recommended yes.
