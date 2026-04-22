# LON — Work Plan

> **Правила на работа:** види [`CLAUDE.md`](CLAUDE.md). Verification Protocol е задолжителен за секој таск.
>
> **Roadmap за преостанатите фази (P7–P13):** [`docs/ROADMAP.md`](docs/ROADMAP.md) — detailed traceability на секој placeholder screen со ефорт, приоритет, зависности и препорачан sprint редослед.

---

## 🎯 SESSION KICKOFF — што да направиш ПРВО во нова сесија

**НЕ ПРАШУВАЈ ЗА ВПС / КРЕДЕНЦИЈАЛИ / ТЕСТОВИ — СИТЕ СЕ ВО МЕМОРИЈА.** Прочитај ги овие 5 работи (5 минути) пред било што друго:

1. **`memory/MEMORY.md`** — индекс на persistent факти (autoloaded, само провери да не си заборавил нешто).
2. **`CLAUDE.md` секции 3–7** — Verification Protocol, Environments, Defaults. Особено **Contract Hygiene Protocol** (точки 1–5 под §3).
3. **🔔 Deferred Backlog на врв од овој WORK_PLAN** — експлицитно одложени таскови. Кога примарниот таск природно ги допира, подигни ги!
4. **WORK_PLAN „Current Active Task"** (на дно) + состојбата на активните фази.
5. **Последни 3 записи во `SESSION_LOG.md`** — последни documented работи + discoveries.

### Quick facts (можеш да ги користиш одма без проверка):

| Работа | Вредност |
|---|---|
| VPS | `root@173.212.254.216` (Contabo, Ubuntu 24.04), passwordless SSH преку `~/.ssh/id_ed25519` |
| App path на VPS | `/opt/apps/LON/LON-test` |
| Домен | `https://elon.elbosoft.click` (Caddy reverse proxy + auto SSL) |
| Admin login | `admin` / `Admin123!` (seeded) |
| Контејнери | `lon-sqlserver`, `lon-api`, `lon-worker`, `lon-frontend` |
| TEKSPORT tenant id | `b8d4fe76-8d94-470b-a251-f8111d3f1db3` (seeded на VPS) |
| Legacy ELON DB | `localhost` Windows auth, DB=`ELON`, read-only |
| i18n јазици | mk (primary), sr, sq, en — `frontend/web/src/i18n/locales/` |
| Deploy flow | Локален commit + push → `ssh root@... && cd /opt/apps/LON/LON-test && git pull && docker compose build <svc> && docker compose up -d <svc>` |
| Тестови | `tests/LON.IntegrationTests/` (Testcontainers-MsSql; CI на Ubuntu) |

### Ако корисникот ти пише „продолжи" или „почнувај":

Оди на **Current Active Task** на дното од овој документ и започни веднаш. Без прашања. Без „како сте", без re-introduction. Продолжи тамо каде последната сесија запре.

---

## Status Legend
- `[ ]` Не започнат
- `[/]` Во тек
- `[x]` Готов + верификуван (со SESSION_LOG доказ)
- `[!]` Блокиран (причина во SESSION_LOG)
- `[~]` Скипнат (причина во SESSION_LOG)

---

## 🔔 Deferred Backlog — НЕ ЗАБОРАВАЈ

> Експлицитно одложени таскови што се лесно заборавливи („паралелно", „follow-up"). Скенирај ја оваа секција на почеток на секоја сесија. Правило: кога природно ја допираш областа во примарен таск, подигни го одложениот наместо да го оставиш.

**Phase 0 cleanup leftover:**
- [x] **P0.3.4** — *2026-04-19*: only 1 warning remained (`LONAuthorization.GuaranteePercentageOverride`); earlier session fixed the other 7. Added `HasColumnType("decimal(5,2)")` + migration `20260419192743_P0_3_4_DecimalPrecision_CompensatingTariffNullable`. Same migration also corrects `LONAuthorizationItem.CompensatingTariffCode` from `IsRequired()` to `IsRequired(false)` so the schema mirrors the `string?` CLR type (eliminates the `string.Empty` seed workaround).

**Phase 2.5 i18n retrofit (паралелно со Phase 2+):**
- [ ] **P2.5.4** — Retrofit на постоечки страници (Dashboard, WMS, Production, Customs, Guarantees, Reports, Advanced, Master Data, Admin) — кога се допираат
- [ ] **P2.5.5** — `Intl.NumberFormat` + `Intl.DateTimeFormat` helpers
- [ ] **P2.5.6** — Backend error codes → `t('errors.<code>')`
- [ ] **P2.5.7** — PDF/Excel/XML i18n (gated on Phase 4)

**Phase 6 Priority-B (паралелно со Phase 2+):**
- [x] **P6.21** — *2026-04-20 (commit `eaeab96`)*: root cause was unrelated to EF closures. The real bug was a silent enum-default trap: `QualityStatus` only defined `OK=1/Blocked=2/Quarantine=3`, so any receipt/import that omitted the field persisted the CLR default `0`. Resolvers filtered `== QualityStatus.OK` and silently skipped those rows. Fixed by adding `QualityStatus.None=0` explicit label, coercing unset → OK on create (CreateReceiptCommand, ReceiptsImportExecutor), accepting OK OR None on read paths (MaterialIssue, Export) as defense-in-depth, and a data-only migration `P6_21_QualityStatusBackfill` that UPDATEs legacy 0 → 1 on InventoryBalances + ReceiptLines. Regression test `Issue_LegacyQualityStatusNone_IsResolvedLikeOk` engineers a legacy balance and asserts the resolver now surfaces it.
- [x] **P6.22** ✅ — **KW12 gap G1: `ProductionOrders` import target.** New `ProductionOrdersTargetSchema` + `ProductionOrdersImportExecutor`. Groups rows by `workOrderNumber` → 1 PO per group + 1 ProductionOrderMaterial per row. VPS verified on 3-WO Matriks slice: 210 rows → 3 POs + 210 materials = 213 entities committed atomically (commit `69471b2`).
- [x] **P6.23** ✅ — **KW12 gap G2: `CustomsDeclarationLine.IsPreferentialOrigin` column.** Nullable bool (null = unknown). Added to CustomsDeclarations target schema as row-level field; executor populates from the resolved row (commit `69471b2`).
- [x] **P6.24** ✅ — **KW12 gap G3: `ProductionOrderMaterial.PreAssignedMRN/Batch + EfficiencyFactor`.** New nullable columns + configuration. `IssueAllMaterialsCommand` honours pre-assignment (falls through to FEFO when null). `ProductionOrders` import target populates from Matriks. VPS verified: PreAssignedMRN `26MKIM10150003D7B3` + EfficiencyFactor `0.8934` persisted per material (commit `69471b2`).
- [x] **P6.25** ✅ — **KW12 gap G7: Items import executor upserts soft-deleted rows.** `IApplicationDbContext.CurrentTenantId` exposed; executor does `IgnoreQueryFilters + TenantId == Current` lookup, undeletes + refreshes fields instead of failing "already taken" (commit `69471b2`).
- [x] **P6.26** ✅ — **KW12 gap G8: `POST /masterdata/uom` default active.** `UoMRequest.IsActive` is nullable with `true` default; both POST + PUT treat null/true as IsDeleted=false, only explicit `false` soft-deletes (commit `69471b2`).
- [x] **P6.27** ✅ — **KW12 gap G9: CustomsDeclarations executor pre-checks MRN.** Separate `mrn` field in schema (falls back to DeclarationNumber). Executor pre-validates both `(TenantId, DeclarationNumber)` and `(TenantId, MRN)` uniqueness before SaveChanges (commit `69471b2`).
- [x] **P6.28** ✅ — **KW12 soft gaps S1+S2+S5** shipped (CustomerOrderNumber, WeekNumber, EfficiencyFactor). S3 (CMRNumber/ClosingNumber/CommercialInvoiceNumber) + S7 (TotalGross/TotalNet) deferred — bundle when we wire up Transport-sheet import or declaration PDF reporting.
- [x] **P6.29** ✅ — **KW12 gap G4+G5: seed STK + KO UoMs** idempotently; `BackfillKw12SupportingDataAsync` runs on every startup, adds missing rows and undeletes phantoms (commit `69471b2`). Warehouse 222 left as manual seed (tenant-specific).
- [x] **P6.30** — *2026-04-20 (commit `59a57cf`)*: `POST /api/masterdata/items/backfill-base-variants` (admin-only, `?dryRun=true` supported). New MediatR `BackfillItemBaseVariantsCommand` walks every current-tenant Item with null BaseCode/ColorCode/SizeCode, runs `ItemsImportExecutor.DecomposeCode`, creates/links base Items, patches variant fields + ParentItemId. Idempotent. DecomposeCode promoted internal → public.
- [x] **P6.31** — *2026-04-20 (commit `0713cfe`)*: `GET /api/masterdata/items/{id}/import-attributes` returns distinct (TariffCode, CountryOfOrigin, IsPreferentialOrigin, Supplier, DutyRate, VATRate) tuples across active-MRN CustomsDeclarationLines plus aggregated available quantity per tuple. MediatR `GetItemImportAttributesQuery`.
- [x] **P6.32** — *2026-04-19*: shipped migration `20260419190825_P6_32_FilteredUniqueIndexes` — adds `WHERE [IsDeleted] = 0` SQL Server filtered-index predicate to 20 unique indexes spanning Items, Partners, Warehouses, Locations, WorkCenters, Machines, Employees (both), UoM, ItemUoMConversions, Routings, RoutingOperations, BOMs, BOMLines, ProductionOrders, ProductionOrderMaterials, ProductionOrderOperations, MaterialIssues, ProductionReceipts, CustomsDeclarations (both), CustomsDeclarationLines, MRNRegistries, GuaranteeAccounts, LONAuthorizations, ImportMappingProfiles, CodeListItems, DeclarationRules, TariffCodes, CustomsProcedures. Soft-deleted rows no longer block re-insert of the same value.
- [~] **P6.33** — **UI for parent-variant rollups** (KW12). Production orders list: collapsible main PA → variant children **✓ shipped 2026-04-19 (`f2eeeed`)**. Items list: color/size badges + Base column **✓ shipped (`f318f92`)**. Still TODO: toggle "show variants" at list level + aggregate by BaseCode; Inventory filters for base-article rollup; reports at both levels per user requirement.
- [x] **P6.34** — *2026-04-20 (commit `5889c86`)*: `POST /api/import/presets/kw12` orchestrator. One xlsx upload → 3 pre-configured ImportSessions (Matriks→Items, Faktura→CustomsDeclarations, Transport→Receipts). Cyrillic sheet-name aliases honoured. New `IXlsxMultiSheetParser` sibling contract + `XlsxImportParser.ParseAllSheets`. Frontend wizard wiring deferred (backend mechanics unblock the 3-manual-slice pain).
- [x] **P6.35** — *2026-04-20 (commit `0fdcdbb`)*: BOMsImportExecutor shipped. Groups rows by `parentItemCode` (Either-scope so header defaults flow into every row), creates 1 BOM + 1 BOMLine per row per group, auto-bumps Version over existing (TenantId, ItemId). Honours optional `position` for line ordering. Regression test `Commit_Boms_CreatesBomWithLinesForHeaderParent` covers the happy path.
- [/] **P6.36** — *2026-04-20*: `components/common/MrnMeter.tsx` — inline consumption strip (Used/Total + Discharged/Used + outstanding badge + days-to-expiry). Mounted in the main Customs page MRN column + used across `/customs/deadlines`. **Still TODO**: per-line duty breakdown panel, waste-slot preview in WasteDeclarationModal, advisory panel from rule-engine warnings in CustomsDeclarationForm (rule engine endpoint `POST /api/customs/declarations/validate` already returns Errors + Warnings).
- [/] **P6.37** — **Sidebar + IA redesign — role + process driven (NOT architectural modules).** Factory sells stitching service (minutes, capacity, on-time delivery) не finished goods. Nav organized by **job role + daily tasks + process flow + critical decisions**. See [`docs/design/P6-37-ia.md`](docs/design/P6-37-ia.md) — single source of truth. Groups: 🏭 Магацин · 🛃 Царина · ✂️ Производство · 📦 Готов производ · 👥 HR · ⚙️ Машини · 💵 Финансии · 🎯 Менаџмент · 🧰 Поставки (admin) + cross-cutting (Search / AI / Import / Language). Role-based filtering: магационер не гледа финансии/HR.
  - [x] **P6.37.0** — IA design doc (`docs/design/P6-37-ia.md`) + WORK_PLAN breakdown + TodoWrite — *2026-04-19*
  - [x] **P6.37.1** — Verified seeded roles + frontend AuthService role exposure — *2026-04-19*
  - [x] **P6.37.2** — `PlaceholderPage` component + i18n `placeholder.*` in 4 langs — *2026-04-19*
  - [x] **P6.37.3** — Sidebar refactor: role-aware filtering (`useNavForRoles`) + collapsible groups + localStorage persist — *2026-04-19*
  - [x] **P6.37.4** — TopBar cross-cutting bar (Search stub / AI → /knowledge-base / Import admin-only / Logout / user identity) — *2026-04-19*
  - [x] **P6.37.5** — 🏭 Warehouse group (**pilot**): 9 items (3 reuse existing pages, 1 partial, 5 placeholders) — *2026-04-19*
  - [x] **P6.37.6** — 🛃 Customs group: 8 items (3 reuse, 5 placeholders) — *2026-04-19*
  - [x] **P6.37.7** — ✂️ Production group: 10 items (1 reuse Production page, 9 placeholders) — *2026-04-19*
  - [x] **P6.37.8** — 📦 Finished Goods group: 9 items (all placeholders — new domain) — *2026-04-19*
  - [x] **P6.37.9** — 👥 HR group: 9 items (2 reuse Employee/Shift mgmt, 7 placeholders) — *2026-04-19*
  - [x] **P6.37.10** — ⚙️ Machines / Work Centers / Efficiency group: 9 items (1 reuse WorkCenters, 8 placeholders) — *2026-04-19*
  - [x] **P6.37.11** — 💵 Finance group: 10 items (1 reuse Guarantees, 9 placeholders) — *2026-04-19*
  - [x] **P6.37.12** — 🎯 Management (KPI) group: 11 items (1 reuse Dashboard, 10 placeholders) — *2026-04-19*
  - [/] **P6.37.13** — VPS deploy done (commit `3f78c6f`), HTTP 200 + tekuser login OK with `Warehouse Manager` role. **Visual per-role smoke pending user check.** 7 additional roles seeded in P6.37.14.
  - [x] **P6.37.14** — *2026-04-19 (commit `dd78b32`, VPS fast-forwarded via SSH)*. Legacy flat Sidebar section removed; `<Navigate>` redirects added for `/dashboard`, `/inventory`, `/production`, `/customs`, `/guarantees`, `/traceability`; `resolveActiveModule` rewritten per new IA. Idempotent `RoleTopUpSeed` creates 8 missing roles + 8 TEKSPORT test users (Customs Officer `tek-customs`, Warehouse Operator `tek-wh-op`, Production Operator `tek-operator`, QC `tek-qc`, HR Manager `tek-hr`, Maintenance Tech `tek-maint`, Finance Clerk `tek-finance`, Manager `tek-mgr`; password `Test123!`). API log confirmed `RoleTopUpSeed: added 8 missing roles + created 8 test users`. All 8 logins verified — JWT carries correct role. Bundle check: new nav keys present; `Поставки` (not `Настройки`) in mk.json live. **Per-role visual smoke = user-driven (P6.37 consumer verification).**
  - [ ] **P6.37.15** — (follow-up, deferred) `design:accessibility-review` audit across full app
- [x] **P6.41** — *2026-04-20*: user supplied the key; updated `/opt/apps/LON/LON-test/.env` `OPENAI_API_KEY=` (backup at `.env.bak.p641`, mode `chmod 600`). Compose already injects it as `OpenAI__ApiKey`. Recreated `lon-api`: VectorStoreBackgroundService logged `✅ Vector Store initialized with 9 chunks` — 9 successful OpenAI embedding calls end-to-end. Zero 401s / zero errors in the 5 minutes after restart.
- [x] **P6.42** — *2026-04-20*: converted `KnowledgeBaseController`'s 4 local request DTOs (`QuestionRequest` / `ConceptRequest` / `SearchRequest` / `CodeListItemRequest`) from positional records to records with init-only properties, unblocking System.Text.Json binding of `{"query":"..."}` bodies. Regression guard in `KnowledgeBaseSearchTests`. VPS verified: lowercase query body → HTTP 200 with 3 RAG chunks from Правилник (similarity 0.79–0.80); empty-query → HTTP 400 with controller validation message "Query не може да биде празен". Commit `de3a848`.
- [/] **P6.38** — **Frontend catch-up sweep (umbrella).** 2026-04-20 progress: ExportDeclarationModal (P2.6a), ReturnDeclarationModal (P2.6b), TrafficLightGuarantees widget mounted on Dashboard (P4.4), `/admin/tenant-settings` (FEFO + InflateWaste toggles wired to `PUT /tenants/{id}` + `PUT /tenants/{id}/settings/fefo`), `/admin/audit-log` viewer (GET /api/audit). Earlier 2026-04-20: KB search, KW12 wizard, Items backfill, ItemImportAttributes, MassTransfer, QuickEntry, RecentValues hook. **Still owed:** declaration detail line editor, MRN usage meter inline, guarantee ledger tree, Inventory filter-by-base toggle, ProductionOrder materials table with PreAssignedMRN/EfficiencyFactor visibility, TariffCodeRate CRUD, BOM/Routings builders, Reports (per-material import breakdown).
- [x] **P6.10** — *2026-04-20* — 10 domain controllers under `src/LON.API/Controllers/MasterData/`. URL contract preserved. Commit `0a7027c`.
- [x] **P6.11** — *2026-04-20 (partial)* — Items CRUD → MediatR handlers in `src/LON.Application/MasterData/Items/ItemHandlers.cs`. Partners stays direct DbContext (no business logic to extract). Commits `f38a1ae` + `168c70e`.
- [ ] **P6.12** — Consistent API response shape `{ data, errorMessage?, errors[]? }`
- [~] **P6.13** — investigated 2026-04-19: API correctly returns `locationType` field populated with enum value (e.g. `locationType: 1` for Receiving); frontend `LocationList` + `LocationInquiry` consume it correctly. Original description referenced a `type: null` bug that no longer reproduces. Likely fixed upstream; closing as not-a-bug.
- [x] **P6.14** — *2026-04-19 (commit `6cdb949` + VPS verified)*: root cause found — `DocumentChunkingService.ChunkDocument` had an infinite loop when `endIndex` clamped to `content.Length` but `startIndex = endIndex - overlap` didn't advance (same tail chunk re-emitted forever → OOM via `List<string>.set_Capacity`). Fixed by (a) breaking when `endIndex >= content.Length` after emitting the final chunk, (b) guarding forward progress with `Math.Max(endIndex - overlap, startIndex + 1)`. Added 4 unit tests in `DocumentChunkingUnitTests` as regression guard. VPS confirms: chunking now completes; next error is a **clean 401 Unauthorized from OpenAI embedding call** (not an OOM) — see new task P6.41 below.
- [/] **P6.15** — *2026-04-19*: `/health/live` + `/health/ready` (DB probe, 503 on failure) shipped; legacy `/health` and `/health/db` kept as aliases. Structured logging (Serilog JSON) **deferred** to P6.15b — heavier rework (assembly-wide switch from Microsoft.Extensions.Logging), not part of this session.
- [x] **P6.16** — *2026-04-19*: added explicit `AddDataProtection().SetApplicationName("LON-API").PersistKeysToFileSystem(...)` so the startup warning ("key may be persisted to storage in unencrypted form") is no longer implicit. Certificate-based encryption deferred until cert-management lands on VPS.
- [x] **P6.18** — *2026-04-19*: added root `Directory.Build.props` with `<CodePage>65001</CodePage>` to force the C# compiler to read source as UTF-8 regardless of Windows system codepage (dev box is CP866 OEM Cyrillic). Verified assembly contains correct UTF-16 LE bytes for `Скопје` in seed. VPS `Tenants.Address` already correct (`"Скопје, Република Северна Македонија"`) — no backfill needed since VPS builds inside Linux Docker (C.UTF-8). The fix is defense-in-depth against future local `dotnet publish`.

**Verification напомена:** секоја completed сесија проверува дали нешто од оваа секција е природно подигнато. Ако е — означи `[x]` со SESSION_LOG доказ.

---

## Фаза 0 — VPS Stabilization + дијагноза

**Цел:** Апликацијата работи end-to-end на VPS. Може да се логира корисник, да се види празен dashboard, да се создаде 1 receipt.

- [x] **P0.1** — SSH access setup од локален Windows до VPS
  - Verify: `ssh user@vps` враќа shell prompt; `docker ps` прикажува сите контејнери ✅
- [x] **P0.2** — Дијагноза на VPS: статус на сите 4 контејнери, logs, DB migrations, reverse proxy, CORS, env vars
  - Verify: документиран „health snapshot" во SESSION_LOG со конкретни findings ✅
- [ ] **P0.3** — Fix на блокерите најдени во P0.2 (секој fix = посебен sub-task)
  - [x] **P0.3.1** — Recreate `lon-api` (главен блокер): `docker compose rm -f api && docker compose up -d api` ✅
  - [x] **P0.3.2** — Затвори SQL Server public exposure: `127.0.0.1:1433:1433` ✅
  - [x] **P0.3.3** — Persistent volume `lon_dataprotection_keys` за DataProtection keys ✅
  - [x] **P0.3.4** — *2026-04-19*: last remaining decimal warning (`LONAuthorization.GuaranteePercentageOverride`) fixed via `HasColumnType("decimal(5,2)")` + migration `P0_3_4_DecimalPrecision_CompensatingTariffNullable`. Also closed the orphan `LONAuthorizationItem.CompensatingTariffCode` `IsRequired()` vs `string?` mismatch.
  - [x] **P0.3.5** — Поправи `BOM.ItemId1` shadow property ✅ (`.WithMany(i => i.BOMs)`)
  - [x] **P0.3.6** — Тргни `version: '3.8'` од compose file (cleanup) ✅
  - [x] **P0.3.7** — Memory/CPU limits на LON контејнери (shared VPS) ✅
  - [x] **P0.3.8** — Bump API memory 1.5G → 3G ✅ (adequate за нормален API workload; Vector Store OOM отделен проблем → види P6.X)
  - Verify: before/after evidence per sub-task во SESSION_LOG
- [x] **P0.4** — E2E smoke test на VPS: login → create receipt → GET returns it ✅ (API-ниво). UI потврда: pending од корисник.
  - Откриени и поправени 3 bug-ови: IApplicationDbContext incomplete (expanded to 38 DbSets), CreateReceiptCommandHandler missing Add() (fixed), JSON cycle в GET (IgnoreCycles)
- [x] **P0.5** — Замена на `CreatedBy = "System"` hack со `ICurrentUserService` ✅
  - Verify: нов receipt има `createdBy: "admin"`; стар (пред fix) остана `"System"` — audit trail работи
- [x] **P0.6** — Receipt ажурира InventoryBalance + InventoryMovement (откриено од доменскиот експерт: „нема инвентори, а има приеми")
  - ResolveLandingLocation со fallback chain (explicit → Type=Receiving → code prefix RCV → first active)
  - Verify: POST receipt `qty=25` → GET `/api/wms/inventory` враќа 1 балансот со quantity 25.0000 на RCV-01 ✅

**🎯 ФАЗА 0 ГОТОВА.** Следен focus: Phase 1 (multi-tenant foundation).

**Фаза 0 DONE = ✅ сите checkboxes x + final SESSION_LOG запис „Phase 0 complete"**

---

## Фаза 1 — Multi-tenant foundation ⚠️ КРИТИЧНО

**Цел:** Сè што постои е tenant-scoped. Два tenant-а работат изолирано.

- [x] **P1.1** — `Tenant` entity + CRUD API + seed `TEKSPORT` tenant ✅
  - Verify: GET `/api/tenants` → TEKSPORT seeded со HQ address, lang=mk, legacyUvoznik=TEKSPORT
- [/] **P1.2** — `ITenantScoped` interface + auto-fill TenantId во SaveChangesAsync. Split in two:
  - [x] **P1.2-B1** ✅ — 10 core entities (Item, Warehouse, Location, Partner, Employee, User, Receipt+Line, InventoryBalance, InventoryMovement). Migration со inline SQL backfill за постојни редови → TEKSPORT. Auto-fill inlined во SaveChangesAsync (ICurrentTenantService постои за handlers, но DbContext го избегнува за DI cycle).
  - [x] **P1.2-B2** ✅ — 31 entities добија ITenantScoped (MasterData: Shift/WorkCenter/Machine; WMS: Transfer±Line/CycleCount±Line/PickingWave/PickTask/Shipment±Line; Customs: Declaration±Line/Document/MRN; LONAuthorization±Item; Guarantee: Account/Ledger/Duty; Production: BOM±Line/Routing±Op/PO+Mat+Op/MaterialIssue/ProductionReceipt; Traceability: TraceLink/BatchGenealogy). Migration со 31 AddColumn + 31 Index + inline SQL backfill + 31 FK. Verified on VPS (commit `bbf8ac9`). 41/~45 business entities tenant-scoped.
  - [x] **P1.2-B3** ✅ — `WH-TEK-VN` (TEKSPORT Vinica) + 7 default locations seeded. `SeedWarehouses` refactored to per-code idempotent upsert. TenantId auto-filled (commit `f845c5d`, verified on VPS via API + SQL).
- [x] **P1.3** ✅ — JWT `tenant_id` claim + `ICurrentUserService.TenantId` + DbContext auto-fill prefers claim (zero DB hits for authenticated writes). Integration test added. Verified on VPS (commit `e723f7e`).
  - Verify: login враќа token со tenant claim; decode потврдува
- [x] **P1.4** ✅ — EF global query filter `!IsDeleted && (CurrentTenantId == null || TenantId == CurrentTenantId)` на сите ITenantScoped entities преку reflection pass во `OnModelCreating`. Verified VPS: admin (TEKSPORT) не може да види item на 2-ри tenant (insert/check/cleanup test — commit `5cc6f72`).
  - Verify: integration test — user од tenant A не гледа записи од tenant B
- [x] **P1.5** ✅ — 22 composite `(TenantId, X)` unique indices наместо глобални. Positive (2-ри tenant може да користи `RM-001`) + negative (интра-тенант duplicate отфрлен) тестови поминаа на VPS. User.Username/Email и reference/KB data остануваат globally unique (commit `2a2924d`).
  - Verify: ист Item.Code може во tenant A и tenant B без колизија
- [x] **P1.6** ✅ — MediatR `CreateUserCommand` со опционален `TenantId` + `[Authorize(Roles="Administrator")]`. Admin може да провизионира user под било кој tenant; omitted tenantId fallback-ира на caller-ов tenant (auto-fill). VPS потврда: `dup-p16-admin` под DUP-CODE-TEST, bidirectional isolation (admin не го гледа, новиот user не гледа TEKSPORT items/users), JWT tenant_id claim коректен (commit `59878b6`).
  - **Deferred:** UI tenant switcher за super-admin (чека реална потреба); multi-tenant login UX reform (P1.7).

**Фаза 1 DONE = ✅ два tenant-а (TEKSPORT + TEST), изолирани**

---

## Фаза 2 — Еден end-to-end flow (увоз за облагородување 42 00)

**Цел:** Комплетен циклус за еден TEKSPORT пример: увоз → магацин → производство → извоз, со гаранција коректно раздолжена.

- [x] **P2.1** ✅ — `CreateCustomsDeclarationCommand` (MediatR) со enforce LONAuthorizationId за Box 37=4200/5100, auto-MRN fallback (YYMK<8hex>A1), MRNRegistry creation со Total/Used/Expiry, `DeclarationStatus` lifecycle (Draft/Registered/Submitted/Cleared/Cancelled), `CustomsDeclarationCreatedEvent` (P2.2 ќе слуша). 3 нови validation rules (Currency ISO 4217, Country ISO 3166, LONAuthRequired). Frontend: conditional LON auth picker + Box 02/15/17 полиња + status badge. Seed: `INW-PROC`→`4200`, TEKSPORT LON Odobrenie `26/TEKSPORT/0001`. VPS потврдено: declaration creirana со MRN=`26MK62636F15A1`, Status=Registered(1), Duty=50, VAT=189, registry row со Expires=2026-10-15 (commit `c37b011`).
- [x] **P2.2** ✅ — Sync guarantee auto-debit во `CreateCustomsDeclarationCommandHandler`. Формула `(Duty+VAT) × procedure.GuaranteePercentage / 100`. Hard-enforce: currency-matched account + available limit. VPS потврда: IM 4200 1000 EUR → debit 119.5 EUR, balance 0→119.5 на GUA-2024-001; `GuaranteeDebitedEvent` емитнат; negative paths покриени во интеграциски тестови (commit `63bf612`).
- [x] **P2.3** ✅ — Handler pre-validates MRN (registered + active + unexpired + no aggregate overdraw), applies TEKSPORT inflate-for-waste on balance/movement (declaration stays at declared), atomically increments `MRNRegistry.UsedQuantity`, flips `IsActive=false` when fully used, refines `LonProcessState=Imported` only for 4200/5100. VPS verified: qty=40 → balance=42.1053 (5% waste), registry tracks declared qty (commits `f557899`/`38ce54f`).
- [ ] **P2.4** — `MaterialIssue` на `ProductionOrder` (batch+MRN задолжителни; no-negative)
  - Verify: обид за issue > залиха враќа 400; обид без batch/MRN враќа 400
- [ ] **P2.5** — `ProductionReceipt` — нов batch + `TraceLink` до суровината
  - Verify: BatchGenealogy query враќа lineage од FG назад до raw materials + MRN
- [ ] **P2.6a** — Извоз (EX декларација + Shipment) → Guarantee credit
  - Verify: Guarantee balance се враќа на нула после извоз на сите количини
- [ ] **P2.6b** — Враќање материјал (Vrakanje) → нов `MaterialReturnDeclaration` + credit
  - Verify: партијално враќање — credit = count * unit guarantee
- [ ] **P2.6c** — Отпад (Otpad) → нов `WasteDeclaration` + credit
  - Verify: waste declaration редуцира MRN available + credits guarantee
- [ ] **P2.7** — Declaration validation rules — пополни ги недостигачките (currency, weight sum, partner country)
  - Verify: невалидна декларација враќа структурирани errors по rule

**Фаза 2 DONE = ✅ еден реален TEKSPORT flow извршен end-to-end на VPS**

---

## Фаза 2.5 — Internationalization (i18n) инфраструктура

**Цел:** Апликацијата е multilingual. Сите UI стрингови се извлечени во translation dictionaries; има language switcher; датуми/броеви/валути се локализирани.

**Зошто овде (пред Phase 4):** Секоја нова страница додадена во Phase 4/5 треба да е i18n-ready од почеток. Retrofit-от на 30+ постоечки страници треба да се направи пред да се додадат уште 30+.

- [x] **P2.5.1** — react-i18next@13 + i18next@23 + browser-languagedetector setup, `src/i18n/i18n.ts` ✅
- [x] **P2.5.2** — `locales/{mk,sr,sq,en}.json` со common/nav/login/dashboard/wms/qualityStatus/errors namespaces ✅
- [x] **P2.5.3** — `LanguageSwitcher` компонента, постојана во localStorage (`lon.lang`), flag emojis + native names; поставена во Sidebar + Login footer ✅
- [/] **P2.5.4** — Retrofit на постоечки страници. Dashboard + Customs + Guarantees prelazija na `formatQuantity`/`formatDate` (2026-04-20). Паралелен backlog: останатите страници се преведуваат при следно touch (helpers готови, еден-линија replace).
- [x] **P2.5.5** ✅ — *2026-04-20*: `utils/format.ts` со `formatQuantity`, `formatInteger`, `formatCurrency`, `formatPercent`, `formatDate`, `formatDateTime`, `formatTime`, `formatRelativeDate`. Locale мапинг mk→mk-MK, sr→sr-RS, sq→sq-AL, en→en-GB.
- [x] **P2.5.6** ✅ — *2026-04-20*: `Result<T>.ErrorCode` + `Failure(code, message)` overload; `ErrorCodes` static; ~14 handlers мигрирани (MRN probes, Certify, Waste slots, Export/Return, MaterialIssue incl. FEFO flag, MassTransfer, MoveBatch, QuickEntry). Frontend `utils/translateError.ts` + ~90 keys во `errors.*` namespace на 4 locales. Regression tests во `QuickEntryTests`.
- [/] **P2.5.7** — *2026-04-20*: `utils/export.ts` ship-ан со `exportToCsv(rows, columns, filename)`. Locale-aware (mk-MK → 1.234,56 vs en-GB → 1,234.56), RFC 4180 quoting, UTF-8 BOM за Excel. Wired во LONAuthorizationsList / DeclarationsByType / MrnDeadlines. PDF export + customs XML i18n остануваат за PDF/print round.

**Фаза 2.5 DONE = ✅ свичот mk↔en (+ други) работи на сите страници**

---

## Фаза 3 — Data migration од ELON

**Цел:** Мигрирани реални TEKSPORT податоци во нова апликација за side-by-side споредба со ELON продукција.

- [x] **P3.1** ✅ — `src/LON.Migration` .NET 8 console (`lon-migrate`). CLI: items/auths/decls/inventory/reconcile/all with `--tenant`, `--limit`, `--dry-run`. Idempotent via deterministic MD5-derived Guids. Runs against VPS LON DB via SSH tunnel to `127.0.0.1:11433`. Session 2026-04-19.
- [x] **P3.2** ✅ — tblArtikli → Items. **11,012 rows** written on VPS (2 dupe-codes skipped). ItemType ∈ {Raw=0, Component=1, FG=2, SemiFG=3} derived from legacy ArtKatTip + ArtKatSurovina.
- [x] **P3.3** ✅ — **Skipped as documented.** Legacy ELON has no firms table; Partner FKs anchored on a synthetic `LEGACY-MIG` Partner seeded per tenant. Real partner reconstruction deferred.
- [x] **P3.4** ✅ — Odobrenija (4 parent permits) + Zaklucoci (261 decisions) → LONAuthorizations (261). Parent guarantee amount + expiry cascaded to all children whose date window matches.
- [x] **P3.5** ✅ — FakturiU5Z + FakturiU5 → CustomsDeclarations + Lines. **702 declarations + ~31405 lines** written. 329 had no matching authorization (ZaklucokBroj archived). DeclarationNumber composed `{Broj}/{yyMMdd}/{OdobrenieRBr}` to avoid cross-time collisions.
- [x] **P3.6** ✅ — LagerMaterijali → InventoryBalance. **804 open balances** written. Join switched to ArtKatBrMat (string code) because legacy ArtRBrMat is 100% NULL. Σ Kol[Proces=1] − Σ Kol[Proces ∈ 7,8,9] aggregation. Legacy PlusMinus column was never populated.
- [x] **P3.7** ✅ — Reconciliation report (`migration_reconciliation.html`). Side-by-side counts + sample Zaklucok check. **Critical pass: Zaklucok 2827 shows ELON 97,905.26 kg = LON 97,905.26 kg**, 1 declaration in each.

**Фаза 3 DONE = ✅ експертот може визуелно да потврди „LON покажува исто како ELON за оваа Zaklucok"**

---

## Фаза 4 — Legacy gap coverage

**Цел:** Недостигачките legacy features имплементирани во новата архитектура (без legacy кварц).

- [x] **P4.1** ✅ — Zaverka. `ZaverkaNumber` + `ZaverkaDate` fields on CustomsDeclaration + `POST /api/customs/declarations/{id}/certify`. Draft/Registered/Submitted → Cleared. Tenant-scoped uniqueness guard. VPS verified: one Registered declaration certified, second certify call on same id returns 400 with "Декларацијата веќе е заверена." (commit `8462a2d`).
- [x] **P4.2** ✅ — PEE060 XML. `GET /api/customs/pee/060?authorizationId=...&from=...&to=...`. Envelope constants (C5/9999/111111) match legacy `cmdXML_PEE060_Click`; body aggregates by (TariffCode, Country) into Zadolzuvanje/Razdolzuvanje. VPS verified: 1342-byte XML with 2 TariffCodeSummary blocks (commits `8462a2d`, `4055fba` — XmlWriter flush-race fix).
- [x] **P4.3** ✅ — MozniMinusi. `GET /api/wms/inventory/mozni-minusi` returns `{ negativeMovements, negativeBalances, totalChecked }`. VPS verified returning existing negative-movement rows (FG-001 batch FG-VPS-P25-01 showing net=-3 from an over-shipment scenario).
- [x] **P4.4** ✅ — Traffic-light Guarantees. `GET /api/guarantee/accounts/traffic-light` returns `{ indicator: green/yellow/red/critical, utilisationPercent }`. Thresholds 60/80/95. VPS verified on two accounts (both green: 0.09% and 0%).
- [ ] **P4.5** — ECD auto-pull — **deferred, no test environment available.**
- [x] **P4.6** ✅ — 4 waste slots + Zaguba. `CreateWasteDeclarationCommand.Slots: List<WasteSlot>` (SlotIndex 0=Zaguba, 1..4=normal). Sum must equal total. Movement number suffixed `/W1..W4` or `/Z`. Backward compatible when Slots is null.
- [x] **P4.7** ✅ — Year-indexed tariff rates. New `TariffCodeRate` entity with `(TariffCodeId, ValidFrom, ValidTo?, CustomsRate, VATRate, Source)` + migration `P4_ZaverkaAndTariffCodeRates`. `DutyRateLookupWarningRule` consults the year-indexed row first; falls back to base TariffCode rate when no window matches the declaration date.

*(Verify criteria за секој P4.x ќе се детализира кога фазата ќе почне.)*

---

## Фаза 5 — Productivity parity

**Цел:** Функции што го направија ELON usable 30 години. Сè насочено кон **минимум/нула keystrokes** за повторувачки операции.

### 5A — Generic data importer (замена за 26 `frmTransfer<Uvoznik>` форми)

**Еден UI, сите клиенти.** Не 26 custom форми — еден конфигурабилен importer со именовани mapping profiles.

- [x] **P5.1.1** ✅ — File upload (Excel .xlsx/.xls, CSV, TSV, XML, JSON) + format auto-detect + preview. `POST /api/import/sessions` (multipart, 25MB cap) → `ImportSession` entity (tenant-scoped) persists parsed grid as JSON; 20-row preview returned immediately. Parsers: ClosedXML for xlsx; hand-rolled CSV with RFC-4180 quoting + comma/semicolon/tab auto-detect; JSON (array or {data:[]}); XML (most-frequent repeated child → rows). Migration `P5_1_AddImportSessions`. 5 integration tests. VPS verified: CSV 3 rows + GET round-trip + XML 2 rows + `.exe` → 400 + list shows both (commit `9a626a0`).
- [x] **P5.1.2** ✅ — Column mapping backend + named profiles. New `ImportMappingProfile` entity (TenantScoped, unique on Tenant+Target+Partner+Label). `PUT /api/import/sessions/{id}/mapping` validates every column against uploaded headers, flips session to `Mapped`, optionally upserts a profile. `GET /api/import/mapping-profiles?targetEntity=&partnerContextId=` prefers partner-scoped profiles then LastUsedAt/UsageCount; `DELETE` soft-deletes. 5 integration tests. VPS verified: CSV uploaded → PUT mapping w/ profile saved → GET session echoes mapping → suggest returns profile (UsageCount=1) → unknown header 400 → DELETE → suggest empty (commit `f8c2b17`). Frontend wizard is bundled with P5.1 final UI pass.
- [x] **P5.1.3** ✅ — Header-level defaults. `PUT /api/import/sessions/{id}/defaults` persists `{ values: Record<string, string?> }`; empty/null entries stripped; merged per-row by resolver when column not mapped. 2 integration tests. VPS verified (commit `d650efa`).
- [x] **P5.1.4** ✅ — Column transforms. `PUT /api/import/sessions/{id}/transforms` persists rules per header; `GET /preview-transformed` applies in-memory pipeline (TRIM/UPPER/LOWER/DECIMAL_COMMA_TO_DOT/DATE_PARSE:`<fmt>`); LOOKUP:`<Entity>.<Field>` deferred to commit. 3 integration tests (bundled with P5.1.3 in commit `d650efa`).
- [x] **P5.1.5** ✅ — Target entity schemas. 5 targets with field-level metadata (type/required/scope/enumValues/lookupEntity): Receipts, Items, Partners, BOMs, CustomsDeclarations. `IImportTargetSchema` + `ImportTargetRegistry` (singleton DI). `GET /api/import/targets`, `GET /api/import/targets/{name}`. `ApplyImportMapping` validates both target AND target-field names. 3 integration tests + 2 negative mapping tests. VPS verified (commit `f59b128`).
- [x] **P5.1.6** ✅ — Dry-run + atomic commit. `ImportRowResolver` runs mapping + defaults + transforms + DB LOOKUP + type coercion + required-field check per row. `POST /sessions/{id}/dry-run` returns per-row report (no SaveChanges). `POST /sessions/{id}/commit` dispatches to `IImportTargetExecutor` (Items, Partners, Receipts fully implemented; BOMs stub; CustomsDeclarations live) then calls SaveChanges once — atomic. 5 integration tests covering missing required, header-default fills, commit-creates-items, duplicate-in-file rollback, unknown-LOOKUP. VPS verified: CSV → mapping → defaults → dry-run (0 errors) → commit (2 items created) → re-commit blocked (commit `1623aaa`).
- [x] **P5.1.7** ✅ — CustomsDeclarations executor. Upgraded from stub to working commit path: header fields (declarationNumber, declarationDate, procedureCode via CustomsProcedures.Code lookup, partner, currency, LON auth) + row `CustomsDeclarationLine` population; lands as Status=Draft so user reviews and promotes via the regular Declarations UI. No bespoke PEE envelope parser — generic XmlImportParser already handles partner record-set XML; a preset can be added when a concrete PEE sample surfaces (commit `6bcd20b`).
- [x] **P5.1-UI** ✅ — React wizard at `/tools/import` (5 steps: upload → mapping → defaults → transforms → dry-run/commit). Auto-matches columns by case-insensitive name; applies saved profile from partner-scoped suggestion list; shows live transform preview; blocks Commit until dry-run says committable. i18n namespace `import.*` across mk/sr/sq/en (~55 keys each). Sidebar entry under "Advanced" (commit `135ef4a`). Bundle hash `main.403850bf.js` live on VPS.

### 5B — Bulk workflow actions (zero/min keystroke движења)

**Legacy inspiration:** `frmPodeliBaranjaBrz`, `frmRaspredeliPoProizvoditeliBrz`, template auto-apply.

- [x] **P5.2.1** ✅ — **Issue all materials for Production Order** (1 клик). `POST /api/production/orders/{id}/issues/bulk`. Walks `ProductionOrderMaterial` rows, computes remaining (Required - Issued), delegates to existing CreateMaterialIssueCommand with FEFO auto-pick. Rolls back on any per-line failure (single SaveChanges scope).
- [x] **P5.2.2** ✅ — **Move batch across stages** (1 клик). `POST /api/wms/inventory/move-batch` со `{batchNumber, targetStage, warehouseId?, targetLocationId?, reason?}`. Секоја `InventoryBalance` со даден batch се пренесува во target LocationType (Production/Shipping/Quarantine/...); multi-source + multi-warehouse; LonProcessState се зачувува; DbSet.Local консолидација; idempotent. Frontend: per-row `🔀` button на Inventory со modal и i18n (mk/sr/sq/en). VPS verified: 100 units moved од RCV-222 → PROD-222, idempotent repeat, unknown batch 400 (commits `a7a4ffb`, `b6699ae`). Bundle `main.56c19dea.js`.
- [x] **P5.2.3** ✅ — *2026-04-20*: `POST /api/wms/receipts/bulk-from-declaration`. `BulkReceiptFromDeclarationCommand` explode-ира CustomsDeclarationLine → ReceiptLineDto, делегира на `CreateReceiptCommand` (MRN пропагација + inflate + LON state останува во еден hot path). Frontend page `/warehouse/bulk-receipt`. Regression тест: 2-линиска IM 4200 → 2 receipt линии; unknown decl id → 400 + `errorCode=declaration.not_found`.
- [x] **P5.2.4** ✅ — *2026-04-20*: `POST /api/wms/shipments/bulk-from-fg`. `BulkShipmentFromFGCommand` — filter (Item/Batch/MRN/PO/location/warehouse) → Shipment + per-balance ShipmentLine + drain + `Type=Shipment` movement. Ако `CreateExportDeclaration=true` и selection-от е single-MRN, chain-ува `CreateExportDeclarationCommand` атомично. Frontend page `/warehouse/bulk-shipment` (reuse-ува новиот ArticlePicker). Regression тест + negative path `transfer.no_filter`.
- [x] **P5.2.5** ✅ — *2026-04-20*: `Tenant.AllowFefoAutoPick` flag (default `true` — keeps existing FEFO auto-pick behaviour). Migration `P5_2_5_AllowFefoAutoPickFlag` backfills `DEFAULT 1` for existing tenants. `CreateMaterialIssueCommand.ResolveBalanceAsync` short-circuits the auto-pick path with `"FEFO auto-pick is disabled for this tenant…"` when flag is false, forcing the caller to supply Batch/MRN/Location explicitly. Extended `PUT /api/tenants/{id}` + dedicated `PUT /api/tenants/{id}/settings/fefo` so admin UI can flip the flag without round-tripping the entity. Integration test `FefoAutoPickFlagTests`. Export / Return / Waste FEFO still always on (those are business-rule ordering, not audit preference).
- [x] **P5.2.6** ✅ — **Release Production Order** (1 клик). `POST /api/production/orders/{id}/release`. Draft → Released; BOM lines scaled by `OrderQty / BaseQty × (1 + ScrapPct/100)` into ProductionOrderMaterials; Routing ops copied into ProductionOrderOperations. Already-released = idempotent success.
- [x] **P5.2.7** ✅ — **Mass location change** — *2026-04-20*: `POST /api/wms/inventory/mass-transfer` (+ `/preview`). Филтер по Item/Batch/MRN/SourceWarehouse/SourceLocation/QualityStatus/LonProcessState (барем еден задолжителен) → bulk transfer кон експлицитна таргет локација во еден атомичен повик. DbSet.Local консолидација, zero-qty source leftover за audit trail, `Type=Transfer` movement per source. Frontend page `/warehouse/transfers` со два-стапки wizard (preview → confirm). 3 integration тестови + i18n во 4 јазици. Сliдбар: placeholder→exists.
- [x] **P5.2.8** ✅ — *2026-04-20*: `POST /api/QuickEntry/execute` с `{command:"..."}`. Parser за 4 верба: `issue <po>` (→IssueAllMaterials), `release <po>` (→ReleaseProductionOrder), `move <batch> <stage>` (→MoveBatchAcrossStages), `help`. Page `/tools/quick-entry` со ↑/↓ история, live outcome log. 4 integration тестови (help / empty / unknown verb / unknown stage).

### 5C — Template auto-apply (legacy pattern)

- [x] **P5.3.1** ✅ — *2026-04-20*: `CreateProductionOrderCommand` auto-applies BOM + Routing when caller omits them. Picks latest ACTIVE, currently-valid (ValidFrom ≤ now < (ValidTo ?? +∞)) BOM by Version for the Item; same for Routing (no ValidTo on Routing). Repeat products → zero BOM keystrokes. Integration test `BomTemplateAutoApplyTests` seeds v1 expired + v2 current and asserts v2 lands on the PO.
- [x] **P5.3.2** ✅ — *2026-04-20*: `BOM.PartnerId` nullable column (Migration `P5_3_2_BomPartnerOverride`). `CreateProductionOrderCommand.PartnerId` new optional input. Auto-apply prefers partner-scoped BOM first (exact PartnerId match, ignoring Version if a match exists, otherwise latest Version within the partner scope), then falls back to global (PartnerId=null). Integration test seeds (global v5, partner v1) and verifies partner v1 wins when PO specifies partnerId, global v5 wins when PO does not.
- [x] **P5.3.3** ✅ — `Tenant.InflateImportForWaste` + Receipt handler (CreateReceiptCommand.cs:356) shipped with P2.2.5 I1 (2026-04-18). Closed retroactively 2026-04-20 when the P5 sweep re-verified it against the current codebase. TEKSPORT seed enables it; other tenants opt-in.
- [x] **P5.3.4** ✅ — *2026-04-20*: `GET /api/MasterData/items/article-picker?query=&limit=`. `ArticlePickerQuery` групира резултати по normalised base code (trailing 'A' stripped) така што и base и A-суфикс sibling се прикажуваат рамо до рамо. Frontend component `components/common/ArticlePicker.tsx` (debounced dropdown + A-badge) reused by BulkShipment. i18n `articlePicker.*` во 4 locales. Regression test `ArticlePickerTests`: base + A-sibling симметрично групирани.
- [x] **P5.3.5** ✅ — *2026-04-20*: `UserFieldHistory` (TenantScoped, User-owned; FieldKey + Value natural-key with filtered unique index). Two endpoints: `GET /api/UserPrefs/field-history?fieldKey=...&limit=10` + `POST /api/UserPrefs/field-history {fieldKey, value}`. MediatR handlers upsert on hit, insert + prune beyond top-50 on miss. Frontend `useFieldHistory` hook + `RecentValuesInput` component + MassTransfer page reason field proof-point. Migration `P5_3_5_UserFieldHistory`. Future callers opt-in per field.

---

## Фаза 6 — Code quality & foundations ⬅️ **СЕГА АКТИВНА (пред Phase 1)**

**Одлука 2026-04-18:** Корисник одлучи Phase 6 оди прво. Не заради „срамот" — туку затоа што тука се базичните инфра работи (testing harness, contract hygiene, чиста архитектура). Подобро да се вградат пред мулти-тенант и пред Phase 2 каде се очекува најмногу итерација.

### 6A — Repo hygiene (брзи победи)

- [x] **P6.1** — Restore `.gitignore` + untrack bin/obj ✅
- [x] **P6.2** — Cleanup на bin/obj заостанати од `f92c754` ✅ (rolled into P6.1)

### 6B — Contract hygiene (пред Phase 1 refactor)

- [x] **P6.3** — OpenAPI → TypeScript pipeline ✅ (`./scripts/gen-api-types.sh`, Swashbuckle.CLI 6.6.2, openapi-typescript 6.7.6)
- [x] **P6.4** — ReceiptForm користи `CreateReceiptCommand` + `ReceiptLineDto` ✅

### 6C — Testing infrastructure (највисок ROI)

- [x] **P6.5** — xUnit + WebApplicationFactory + Testcontainers-MsSql harness ✅ (`tests/LON.IntegrationTests/`)
- [x] **P6.6** — `AuthTests.Login_*` ✅
- [x] **P6.7** — `ReceiptFlowTests.CreateReceipt_ThenGetInventory_*` ✅ (би ги фатил сите P0.4/P0.6 bugs)
- [x] **P6.8** — Auth guard tests (401 без token) ✅ (вметнато во AuthTests)
- [x] **P6.9** — CI gate (GitHub Actions `ci.yml`) со contract drift check ✅

**Верификација напомена:** Тестовите ќе се извршат на CI (Ubuntu runner со Docker). Локално — Docker Desktop мора да е активен. Види следен CI run на GitHub Actions.

### 6D — Architecture consolidation (parallel with Phase 2+)

**Ревизија 2026-04-18:** Првично беше означено „пред Phase 1" под претпоставка дека без консистентна архитектура секој query мора мануелно да се tenant-scope-ира. **Но ова не е точно** — EF global query filter во P1.4 applies-ира на ниво на `DbContext`, независно дали caller-от е direct-controller или MediatR handler. 6D е **consistency/maintainability**, не correctness. Преминат во параллелен backlog со Phase 2+ (потврдено од корисник).

- [x] **P6.10** — *2026-04-20* — 10 domain controllers под `src/LON.API/Controllers/MasterData/`. URL contract зачуван.
- [x] **P6.11** — *2026-04-20 (partial)* — Items CRUD → MediatR. Partners стои на direct DbContext (нема business logic за извлекување).
- [ ] **P6.12** — Consistent response shape: `{ data, errorMessage?, errors[]? }` везде. Refactor controllers што враќаат голи entities.

### 6E — Follow-ups од Phase 0 (bugs забележани но не блокери)

- [x] **P6.20** — *2026-04-19 (commit pending)*: `UpsertRestoredBalanceAsync` and `UpsertFgBalanceAsync` in `CreateReturnDeclarationCommand` now probe `DbSet.Local` first (fast path for multi-line same-command consolidation) then fall back to `FirstOrDefaultAsync` against the DB (consolidates with pre-existing untracked rows). Same fallback added to `UpsertWasteBalanceAsync` in `CreateWasteDeclarationCommand`. Export handler already had both probes. Storage no longer bloats by a sibling row per return/waste call. `dotnet build` clean.
- [x] **P6.19** ✅ — `CreateProductionOrderCommandHandler` now calls `Add(order)` before SaveChanges (commit `8462a2d`). Missing integration test `CreateProductionOrder_Persists_VisibleInList` still a follow-up.
- [ ] **P6.13** — **LocationDto serialization drops Type** — API враќа `type: null` и покрај MapLocation. Или DTO constructor или JSON naming policy. Handler-от го користи code prefix fallback; UI-от не може да филтрира по тип.
- [x] **P6.14** — **Vector Store OOM root cause** — fixed 2026-04-19. Not embedding/IO-related at all: `DocumentChunkingService.ChunkDocument` spun an infinite loop when the final chunk clamped to `content.Length` (re-emitted same tail forever → `List<string>.set_Capacity` OOM). Patched the loop-exit + forward-progress guard; added `DocumentChunkingUnitTests` with 4 cases (empty, short, 1 050-char boundary, 120 KB Pravilnik-shape). See commit + SESSION_LOG.
- [/] **P6.15** — health endpoints shipped; Serilog JSON split to new **P6.15b** (deferred).
- [x] **P6.16** — explicit `AddDataProtection()` config shipped; cert-based encryption deferred.
- [x] **P6.15b** — *2026-04-20 (commit `953176b`)*: Serilog + CompactJsonFormatter on stdout; per-request middleware pushes RequestId / UserName / TenantId into LogContext so every log event carries them; `UseSerilogRequestLogging` emits structured access logs. appsettings adds Serilog MinimumLevel section.
- [x] **P6.18** — *2026-04-19 (commit pending)*: shipped root `Directory.Build.props` with `<CodePage>65001</CodePage>`. Forces csc to decode source as UTF-8 on any OS; avoids CP1251/CP866 guessing on Windows dev boxes. Verified `LON.Infrastructure.dll` contains correct UTF-16 LE bytes for `Скопје`. Confirmed VPS `Tenants.Address` row already correct (Linux docker build was always UTF-8) — no data backfill required.

### 6F — Claude self-workflow (вградено во CLAUDE.md)

- [x] **P6.17** — `CLAUDE.md` ажуриран со **Contract Hygiene Protocol** (5 точки: grep frontend при DTO change, regenerate TS на OpenAPI промена, integration test за handler, Preview tools за UI smoke, IApplicationDbContext провера при нов DbSet) ✅. VPS деталите запишани (не повеќе „TBD"). Verification Protocol прошитен со „OpenAPI → TS regenerated" и „Integration test" чекори.

**🎯 Phase 6 Priority-A ГОТОВА.** Следно: Phase 2.5 i18n setup.

---

## Фаза 7 — Flutter mobile (последно)

**Цел:** Scan-first mobile app за магационери (receive, pick, issue, FG receipt) со offline queue.

*(Детални таскови после фази 0-5.)*

---

## Current Active Task

> **>>>** **2026-04-22 (latest) — P14.9: 29 placeholder pages → real + warning backlog clean — HEAD `dccd0b9`, VPS green**
>
> ### 🎯 Completed in this pass
>
> User directive: „Те молам да ги направиме сите placeholder страници за да биде комплетна платформата." + „решавај ги сите Pre-existing warnings." Memory saved to `feedback_fix_all_warnings.md`.
>
> **Warnings:** 22 files cleaned; build exits `Compiled successfully` (0 warnings).
>
> **29 placeholders → real (navGroups backendStatus = exists):**
>
> | Group | Pages |
> |---|---|
> | Finance | cost-accounting · margin · ap · payroll · pnl · cash-flow · reports |
> | Management | capacity · margin · risks · trends · escalations · client-scorecard · monthly-pack |
> | HR | overtime · performance · training · payroll-export |
> | Machines | oee · capacity · setup-time · bottleneck |
> | Production | cutting-queue · sewing-queue · minutes-variance · rework |
> | Finished Goods | packing · pack-lists · returns |
>
> **Total pages on new pattern: 60** (up from 31).
>
> **LocalStorage-backed registers** (MVP persistence until EF entities land): cost rates, supplier invoices, payroll rates, risks, escalations, training.
>
> **i18n × 4 locales:** +25 top-level key groups each (mk / sr / sq / en).
>
> ### 🧭 Next session: backend migration of the 5 localStorage stores
>
> Simple tenant-scoped CRUD entities (1 session):
> 1. `CostRate` (WorkCenterId × ShiftId → RatePerMinute)
> 2. `SupplierInvoice` (Number, SupplierId, IssueDate, DueDate, Amount, Status)
> 3. `EmployeePayrollRate` (EmployeeId, RatePerHour, Currency)
> 4. `RiskRegisterItem` (Title, Category, Severity, Status, Owner, Mitigation)
> 5. `Escalation` (Title, Party, Severity, Status, Owner, DueDate, Resolution)
> 6. `TrainingRecord` (EmployeeId, Topic, SkillArea, Provider, CompletionDate, ExpiryDate, Certificate)
>
> UI swap is mechanical — replace localStorage helpers with API calls.
>
> ---

> **>>>** **2026-04-22 — P14.7 + P14.8 + Wave-10 — HEAD `53c7799`, VPS green (api + frontend)**
>
> ### 🎯 Затворени deferred items + final hub-page audit
>
> **P14.7** — Bulk move N selected balances → нов backend `BulkMoveBalancesCommand` + endpoint `POST /api/WMS/inventory/bulk-move-balances` + UI акција во Inventory BulkActionBar (модал со SearchableSelect target + reason).
> **P14.8** — Declaration drawer EDIT mode — Draft декларациите имаат inline editor за `declarationNumber` + `dueDate` + `notes` + `specialRemarks`; non-Draft се locked.
> **Wave 10** — filters на главните hub pages: Customs (search + procedure + status), Production (search + status), Guarantees (account search + active search + MRN dropdown), MRNUsageTracking (search + Active/Depleted).
>
> **Кумулативно: 31 страници со нов UX pattern** + 2 backend bulk commands + 4 reusable frontend primitives.
>
> ### 🧭 Свесно недопрено (за иден sprint, ако стане приоритет):
> - MasterData CRUD листи (Partners/Items/BOMs/Routings/Warehouses/Locations/UoMs/Machines/WorkCenters) — користат стариот `DataTable` component со built-in search + actions; функционално покриени.
> - Admin pages (User/Role/Shift/Employee/CodeList management) — не се hot-path.
>
> **Готово за корисник UAT.**
>
> ---

> **>>>** **2026-04-22 — UX wave 9: dashboard-style Reports filters — HEAD `12675ff`, VPS green**
>
> ### 🎯 Handoff
>
> **Wave 9** (`12675ff`) затвора 3 dashboard страници:
> - `Reports/CycleCountAccuracy` — accuracy bucket quick filter (≥98% / 95-98% / <95%) врз date+employee+location set.
> - `Reports/WarehouseUtilization` — zone + occupied/empty + text search; layout collapse кога user одбира една половина.
> - `Reports/WMSDashboard` — period selector (7/30/90/365) за movement aggregates + table-search across top items + locations.
>
> **Кумулативно: 27 list pages** со wave-1 пристап или native filters.
>
> ### 🧭 Преостанува за P14:
> - **P14.7** Bulk move-across-location на Inventory (deferred since wave 1).
> - **P14.8** Declaration drawer EDIT mode (deferred since wave 1).
> - MasterData миграција (`PartnersList/ItemsList/BOMsList/RoutingsList`) од стариот `DataTable` на нов pattern — функционално веќе има search, само за визуелна конзистентност.
>
> ---

> **>>>** **2026-04-21 (latest) — UX waves 6-8: P14.6 rollout extended to Maintenance + Reports — HEAD `7a3b44b`, VPS green**
>
> ### 🎯 Handoff
>
> **Wave 6** (`7e5c1dd`) — `MaintenanceHistory` (search) + `MachineDowntime` (search + category + open-only).
> **Wave 7** (`ca5a8af`) — `MaintenancePlan` (search + risk bucket dropdown).
> **Wave 8** (`7a3b44b`) — `Reports/BlockedInventory` (search alongside QC dropdown) + `Reports/MovementReports` (shared text search across receipts + shipments).
>
> **Total list pages touched in P14: 24** (4 wave-1 hot screens + 10 waves-2-5 + 5 waves-6-8 + helpers).
>
> ### 🧭 Followup опционо:
> - `Reports/CycleCountAccuracy`, `Reports/WarehouseUtilization`, `Reports/WMSDashboard` — small dashboards, light pass.
> - `Reports/InventoryByLocation/Batch/MRN` — already filter-rich; consider migrating to SearchableSelect for visual consistency.
> - MasterData migration to new pattern (currently `DataTable` based — works но визуелно различно).
> - **P14.7** Bulk move-across-location + **P14.8** Declaration drawer EDIT mode (still deferred since wave 1).
>
> ---

> **>>>** **2026-04-21 (later) — Cross-cutting UX waves 2–5: P14.6 rollout to 10 more list pages — HEAD `ad591bf`, VPS green**
>
> ### 🎯 Handoff за следна сесија
>
> Wave-1 примитивите (`SearchableSelect`, `DetailDrawer`, `BulkActionBar`, `useRowSelection`) се сега инсталирани на **14 list pages вкупно**:
>
> **Wave 2** (commit `e30e448`) — 6 warehouse + customs:
> - `IncomingShipments` (search + declaration drawer)
> - `QcHold` (filters + row select + bulk release modal)
> - `ShipmentsByStatus` (drawer)
> - `StockByCustomer` (text search + min qty)
> - `VarianceReport` (count + item search)
> - `MrnDeadlines` (text search + drawer)
>
> **Wave 3** (`5e18c4f`) — 5 production:
> - `ProductionToday` (search + status dropdown)
> - `ProductionWip` (двосекциски search)
> - `ProductionCompleted` (search + period)
> - `ProductionAtRisk` (red/amber buckets + search)
> - `ProductionShortage` (material + PO search)
>
> **Wave 4** (`5de3188`) — Machines + HR + Finance:
> - `MachineStatus`, `Absences`, `Invoicing` — search + categorical dropdowns.
>
> **Wave 5** (`ad591bf`) — `ClientContracts` text search.
>
> ### 🧭 Следно за P14:
> - **P14.6 Reports pass** — `BlockedInventory`, `MovementReports`, `CycleCountAccuracy`, `WarehouseUtilization`, `WMSDashboard`. `InventoryByLocation/Batch/MRN` веќе имаат богати филтри — провери и пресекни ако се покрива.
> - **P14.6 MasterData consolidation** — Partners/Items/Warehouses користат стариот `DataTable` (има built-in search). Размисли дали да ги мигрираш на новиот pattern за визуелна конзистентност, или да живеат paralelno (DataTable е acceptable for admin pages).
> - **P14.7 Bulk move-across-location** на Inventory (deferred since wave 1).
> - **P14.8 Declaration drawer EDIT** (deferred).
> - **Maintenance/Downtime/MaintenancePlan/MaintenanceHistory** — еще не е тажурано; check next session.
>
> ### 🧰 Quick facts
> - HEAD: `ad591bf` (main). VPS deployed.
> - Components паттерн: `SearchableSelect` за dropdown + search, `DetailDrawer` за row-click drilling, `BulkActionBar` за multi-row actions, `useRowSelection` hook за checkbox state.
> - i18n: секоја нова page додава search placeholder + статус-dropdown labels во сите 4 локали (mk/sr/sq/en).
>
> ---

> **>>>** **2026-04-21 — Cross-cutting UX wave 1: reusable list patterns (P14.1–P14.4) — VPS green**
>
> ### 🎯 Handoff за следна сесија
>
> **Корисник feedback (2026-04-21):** 4 UX болки на hot-path screens:
> 1. Увозни документи + Царински предмети — ред не се отвора, не може да се види детал.
> 2. BulkShipmentFromFG — MRN/Партија/Референца како plain text input; не е јасно зошто се вика „bulk".
> 3. Магацин и залихи — нема филтри, нема checkbox / bulk акции.
> 4. Принцип: секаде каде листа → филтри; секаде каде акции → row selection.
>
> **Shipped (frontend-only, zero backend changes):**
> - **P14.1 Reusable list primitives** — 3 нови компоненти + 1 hook за да се одбегне повторување кога се рол-аут-ува на останатите ~60 placeholder + существувачки screens:
>   - `components/common/SearchableSelect.tsx` — generic dropdown со search (MRN, batch, partner, warehouse, location, custom procedure). Controlled value, clearable, loading state, optional hint per option.
>   - `components/common/DetailDrawer.tsx` — right-side slide-in drawer (scrim + Esc close + body scroll-lock) за row detail/edit без navigation loss.
>   - `components/common/BulkActionBar.tsx` — sticky bar over the table кога има select-аn redova; конфигурабилен action array (variant default/primary/danger).
>   - `hooks/useRowSelection.ts` — Set-based selection прунира automatski kога филтери се менуваат; expose select-all + indeterminate.
> - **P14.2 BulkShipmentFromFG redesign** (`Warehouse/BulkShipmentFromFG.tsx`) — MRN/Партија/Партнер/Склад/Локација/Постапка сите SearchableSelect (MRN + Batch options derived од живи inventory balances, со qty hint). Нов **preview panel** ги покажува FG редовите кои филтер ги допира со count + total qty + MRN count + table (first 50 rows, truncated badge). Export blocker прикажан inline кога createEx=true но selection покрива ≠ 1 MRN. Button label меняет на „Создај испратница (N редови)" + about-to-ship summary. Насловот и subtitle-от преименувани на „Масовна испратница" + објаснување „еден филтер → N FG редови → една испратница" за да одговара на корисничката забелешка. Client-side preview ги избегнува backend round-trips; bulk command неменет.
> - **P14.3 DetailDrawer на read-only customs listings** — `Customs/DeclarationsByType.tsx` row-click → fetch `GET /Customs/declarations/{id}` → drawer со header fields grid + lines табела + zaverka + notes. `Customs/LONAuthorizationsList.tsx` row-click → drawer со сите полиња (auth type, system, operation, partner, issue/expiry, guarantee amount+currency+ref+pct-override, customs offices, notes).
> - **P14.4 Inventory page refactor** (`Inventory.tsx`) — filter bar (item text + SearchableSelect за location/batch/MRN + QC status dropdown + clear button + showing-count), row checkboxes (header with indeterminate state) и BulkActionBar со 3 акции: Export selected CSV, Bulk Block QC (со задолжителна reason + audit log), Bulk Release QC. Bulk QC endpoint го повикува `POST /WMS/inventory/quality-status` per selected row — локална loop; на крај toast успех/delimičen failure со first error. Row `Премести` е задржана на single-row level (bulk move cross-location е deferred).
> - **P14.5 i18n coverage (4 locales mk/sr/sq/en)** — нови keys: `common.{searchPlaceholder,noResults,clear}`, `bulkActions.{selected,clear,selectAll,selectRow}`, proširен `bulkShipment.{preview*,noMatches,exportMultiMrn,commitWithCount,aboutToShip,*Placeholder,refreshStock,mrnHint,noBatches,noMrns,preview.{item,location,batch,qty}}`, проширен `declarationsByType.{clickToOpen,linesTitle,noLines,partnerCode,zaverkaNumber,zaverkaDate,notes,line.*}`, проширен `lonAuthorizations.{clickToOpen,authType,systemType,operationType,partnerCode,guaranteeReference,guaranteePctOverride,supervisingOffice,notes}`, проширен `inventory.{filters.*,bulkSummary,bulkQc.*}`.
> - **Build green** — `npm run build` поминува. Zero нови lint warnings на touched фajlови; JSON валидност потврдена преку Node.
>
> **Deferred (follow-up sessions):**
> - **P14.6 Rollout** — апликирај истиот pattern (filter bar + row selection + BulkActionBar + DetailDrawer) на 30+ останати list pages (Warehouse/*, Production/*, Finished Goods, Finance/Invoicing, etc.) — incremental.
> - **P14.7 Bulk move-across-location** — денешниот BulkActionBar на Inventory не поддржува масовно преместување кога селекцијата е на различни локации (non-trivial UX + server op, потребен е нов batch endpoint или повторувач на `/WMS/transfers`).
> - **P14.8 Declaration detail EDIT** — drawer моментално read-only; inline edit (status, zaverka, notes) треба да се врзе со постоечки `updateDeclaration` endpoint.
>
> ### 🧰 Quick facts
> - HEAD: (commit pending после овој SESSION_LOG запис).
> - VPS: `root@173.212.254.216` → `cd /opt/apps/LON/LON-test && git pull && docker compose build frontend && docker compose up -d frontend`.
> - Components паттерн е spread-ready: било кое наредно list screen може да adopts без backend промени.
>
> ---

> **>>>** **2026-04-20 (latest) — Sprint 7: Phase 13.1 on-time + 13.3 by-customer + 13.5 exception alerts — HEAD `951eaa1`, VPS green**
>
> ### 🎯 Handoff за следна сесија
>
> **Shipped во оваа сесија (1 commit):**
> - **P13.1 On-time delivery** (`GET /api/Management/on-time?from&to`): batch-join `ShipmentLine.BatchNumber → ProductionReceipt.BatchNumber → PO.PlannedEndDate`. Shipment се смета on-time ако ShipmentDate ≤ max(linked PO.PlannedEndDate). 4 buckets: OnTime / Late1To7 / LateOver7 / Unknown (Unknown excluded од denominator за %). Returns per-shipment rows + per-customer rollup + overall rollup. FE `/management/on-time` — 3-panel: overall KPI (color-coded по 90%/75% прагови) + per-customer rollup table + per-shipment detail table.
> - **P13.3 By-customer** (`GET /api/Management/by-customer?from&to`): one row per customer partner = open + completed POs (CustomerPartnerId-scoped) + producedQty, shipment count + qty (Shipped/Delivered), invoices issued + outstanding + paid (Cancelled excluded). FE `/management/by-customer` — sortable ranked table + CSV export + summary totals strip.
> - **P13.5 Alerts feed** (`GET /api/Management/alerts`): 5 sources aggregated — MRN expiring (≤30d, Critical if ≤7d or expired), overdue invoices (severity by days overdue), material shortage (Required−Issued vs OK/None Imported inventory), at-risk POs (schedule_used − progress heuristic mirrored од P8.4), LON auth expiring. Sorted Critical → Warning → Info. FE `/management/alerts` — dashboard cards со severity-band + category filter + deep-link buttons.
> - 3 integration tests (`ManagementTests.cs`): on-time bucket distribution, by-customer rollup включает инвојс, alerts feed includes MRN expiring + overdue invoice entries со correct severity.
>
> **VPS smoke (2026-04-20 18:42 UTC) — real TEKSPORT insights returned:**
> - `/api/Management/on-time` — empty result for empty shipment window (correct zero-count rollup).
> - `/api/Management/by-customer` — Firma-100 (KW12 client) = 132 open POs, Italian Customer SRL = 1 invoice from Sprint 6 (SMOKE-CT-1).
> - `/api/Management/alerts` — LON auth 2691 expired 110d ago (Critical), 5600013460 Конец Арамид 70 short 429,764.00 M across 126 POs (Warning), plus other material shortages. Real production data surfaced.
>
> **No migrations** — pure aggregation over existing data. No new DbSets.
>
> ### 🧭 Остануваат (long-tail per ROADMAP Sprint 8+)
>
> - **P8.6/7** — Cutting + sewing queues (requires `ProductionOrderOperation.Status` enum + OperationType tag; не се во schema).
> - **P8.8** — Rework view.
> - **P8.9** — Minutes variance (requires new `OperationTimeLog` entity).
> - **P9.2/5/7** — FG returns, quality log, exports by customer.
> - **P10.3–7** — HR overtime, performance, payroll export.
> - **P11.3/7/8** — Machine OEE, setup time, bottleneck.
> - **P12.4–10** — Cost accounting, margin, AP, P&L, cash-flow, reports index.
> - **P13.2** — Capacity utilization (needs machine time rollup — after P8.9).
> - **P13.4** — Margin per customer (needs P12.4 + P12.5).
> - **P13.6** — Risks (new `RiskRegisterItem` entity).
> - **P13.7** — Trends (time-series).
> - **P13.8** — Escalations (workflow entity).
> - **P13.9** — Client scorecard (composite of P13.1 + P13.3 + P13.4).
> - **P13.10** — Monthly review pack (PDF).
> - **Cross-cutting:** P2.5.4 i18n retrofit (~30 страници), P6.12 response envelope refactor, P6.36 MRN meter + waste slot preview, P6.37.15 a11y audit, Flutter mobile (whole app).
>
> **Demo-gate пред long-tail:** ROADMAP recommends a retro демо со TEKSPORT експертот сега — сите hot-path screens требa да се поклапуваат со неговите дневни операции. Оди на TEKSPORT pre-prod session пред да навлегуваме во P8.6+ / P13.6+ кои имаат low priority од нивниот аспект.
>
> ### 🧰 Quick facts
>
> - HEAD: `951eaa1` (main). Всички смени деплојирани.
> - Management endpoints под `/api/Management/` со `GET` методи — без POST, без mutate semantics.
> - Batch-joining в P13.1 се потпира на ProductionReceipt.BatchNumber == ShipmentLine.BatchNumber exact match. Ако textile flow ги префаксира batch-овите во различен начин (што постои ризик), Unknown bucket ќе биде значителен и индицира coverage gap — видливо на UI.
> - Alerts feed е пресметан fresh на секој request (нема cache) — прифатливо за експертски dashboard, а не background push. Ако evolve-ира на push notifications, ќе треба cache + polling.
>
> ---

> **>>>** **2026-04-20 (later) — Sprint 6: Phase 12.3 ClientContracts + Phase 12.2 Invoicing MVP — HEAD `7e2cd40`, VPS green**
>
> ### 🎯 Handoff за следна сесија
>
> **Shipped во оваа сесија (1 commit + 1 fixup):**
> - **P12.3 ClientContract + RateCardEntry**: tenant-scoped entities + filtered unique index на (TenantId, Number). `RateType` enum = PerPiece (итемизирано) | PerMinute (opCode). Контракт носи currency + payment-terms + ValidFrom/To.
> - **P12.2 Invoice + InvoiceLine**: Draft → Issued → Paid → Cancelled. Draft-овите имаат `DRAFT-XXXXXXXX` provisional number; Issue генерира sequential `INV-{yyyy}-{NNNN}` во тенант. GenerateFromPOCommand = резолвира PO.CustomerPartnerId → активен контракт → PerPiece RateCardEntry кое важи на IssueDate + ItemId. OverrideUnitPrice за бusines cases без rate.
> - FinanceController под `/api/Finance/` (contracts + invoices + rates + generate-from-po + issue + mark-paid + cancel).
> - 6 integration tests (POST→GET→DB assert + negative paths за rate validation, no-contract, empty-invoice-issue, paid-immutable-cancel).
> - FE 2 страници: `/finance/contracts` (split-pane со rate-card CRUD) + `/finance/invoicing` (filter + detail со issue/mark-paid/cancel + generate-from-PO form + CSV export). i18n × 4 јазика.
> - Nav: `backendStatus: missing → exists` за finance-invoicing + finance-contracts.
>
> **VPS smoke (2026-04-20 18:22 UTC):**
> - `POST /api/Finance/contracts` → `SMOKE-CT-1` created (contractId `1306afed-7030-46b2-991c-6ab08b8e83bc`).
> - `POST /api/Finance/invoices` (draft со 1 line, 10×2.50=25.00 EUR).
> - `POST /api/Finance/invoices/{id}/issue` → `INV-2026-0001`.
> - `POST /api/Finance/invoices/{id}/mark-paid` → status=3.
> - Negative: `invoice.po_not_found` (generate-from-po са empty PO id) + `invoice.paid_immutable` (cancel after paid).
>
> **Migration live:** `20260420175358_P12_Finance` (ClientContracts + RateCardEntries + Invoices + InvoiceLines).
>
> ### 🧭 Остануваат (приоритетен редослед)
>
> **Sprint 7 (ROADMAP recommends):** Phase 13.1 On-time delivery + 13.3 By-customer + 13.5 Exception alerts. Aggregations врз shipment / PO / MRN — нема нови entities потребни.
>
> **Phase 12 long-tail (Sprint 8+):**
> - P12.4 Cost accounting (CostRate per machine/work-center × minute).
> - P12.5 Margin (aggregate: invoice.total − cost rollup).
> - P12.6 AP (VendorInvoice entity).
> - P12.7 Payroll aggregate (дел веќе во P10.7).
> - P12.8 P&L preview.
> - P12.9 Cash-flow forecast.
> - P12.10 Reports index.
>
> **Follow-ups за Phase 12 MVP (nice-to-have):**
> - PDF render за Issued invoices (P2.5.7 extension).
> - Aging buckets (0-30-60-90) на `/finance/invoicing` dashboard header.
> - Rate effectivity overlap validation (два PerPiece rates за ист Item + overlapping ValidFrom/To → warn на save).
> - `GenerateInvoiceFromShipmentCommand` (за non-LON flow каде billing е per shipped qty, не per PO).
>
> **Phase 2.5 i18n retrofit:** постоечки long-tail (~30 страници).
>
> ### 🧰 Quick facts
>
> - HEAD: `7e2cd40` (main). Сите смени деплојирани на VPS.
> - Админ login и семата работаат per quick-facts по default.
> - Contract hygiene почитуван: grep пред DTO change + regenerated TS + integration tests + Preview smoke (заменет со live VPS curl). 6 нови тестови во `FinanceTests.cs`.
> - Не беа потребни нови DbSets во `IApplicationDbContext` за да поминат сите handlers — додадени се 4 (`ClientContracts`, `RateCardEntries`, `Invoices`, `InvoiceLines`).
> - Enum `BackendStatus` има **только** `missing | partial | exists` — `shipped` не постои; користи `exists` за live-backend-и.
> - `exportToCsv` signature = `(rows, columns[], filename)` (не `(filename, rows)`).
>
> ---

> **>>>** **2026-04-20 (late) — Phase 5 sweep (7 tasks) + P6.38 FE catch-up batch (5 UIs) — HEAD `c5db4c6`, VPS green**
>
> ### 🎯 Handoff за следна сесија (чита се прво)
>
> **Каде запре:** Ти кажа "продолжи со must-have" после Phase 5 sweep. Јас направив 5 FE страници/модалки што ги покриваа shipped-без-UI backends: Export + Return declaration modals, Dashboard traffic-light widget, `/admin/tenant-settings`, `/admin/audit-log`. VPS green, сите тестирани преку curl.
>
> **Прв потег следна сесија:**
> 1. **User UAT на денешните UIs** — најави на https://elon.elbosoft.click/ како admin, провери Customs → Export/Return buttons работат, провери `/admin/tenant-settings` + `/admin/audit-log`, провери Dashboard traffic-light панел. Додаток ги допре и Phase 5 endpoints — mass transfer `/warehouse/transfers`, quick entry `/tools/quick-entry`, recent values (MassTransfer reason field).
> 2. **Ако UAT е OK → продолжи со P6.38 umbrella** (види "Остануваат" листа долу). Следен приоритет: declaration detail line editor + MRN usage meter, или TariffCodeRate CRUD — сите backend е жив, само UI.
> 3. **Ако UAT крие bug → тоа е првиот таск.**
>
> ### 📦 Денес shipped + VPS-verified (5 commits)
>
> **Phase 5 (3 commits):** `c002fac` + `23a29ef` + `031f0f3` — 7 таскови: P5.2.7 mass-transfer, P5.2.5 FEFO flag, P5.3.3 inflate retroactive, P5.3.5 UserFieldHistory, P5.2.8 quick-entry, P5.3.1 BOM auto-apply, P5.3.2 partner override.
>
> **P6.38 FE catch-up (1 commit):** `c5db4c6` — ExportDeclarationModal (P2.6a), ReturnDeclarationModal (P2.6b), Dashboard traffic-light widget mount, `/admin/tenant-settings` (FEFO + InflateWaste toggles), `/admin/audit-log` viewer.
>
> **Log commit:** `11d845a` (Phase 5 sweep WORK_PLAN update).
>
> ### 🗄️ Migrations live on VPS (applied automatically on restart)
>
> - `P5_2_5_AllowFefoAutoPickFlag` — `Tenants.AllowFefoAutoPick bit DEFAULT 1`.
> - `P5_3_5_UserFieldHistory` — new table + (UserId, FieldKey, Value) filtered unique index.
> - `P5_3_2_BomPartnerOverride` — `BOMs.PartnerId uniqueidentifier NULL` + FK.
>
> ### 🧭 Остануваат (приоритетен редослед)
>
> **P6.38 umbrella (must-have, UI-only — backends live):**
> - Declaration detail line editor (currently read-only)
> - MRN usage meter inline на Customs detail
> - Guarantee ledger tree со debit/credit math + release button
> - Inventory filter-by-base toggle (дел од P6.33)
> - ProductionOrder materials table со PreAssignedMRN + EfficiencyFactor visibility
> - TariffCodeRate CRUD (P4.7)
> - BOM / Routings builders
> - Reports per-material import breakdown
>
> **Phase 2.5 i18n (долг рут):**
> - P2.5.4 retrofit на ~30 постоечки страници
> - P2.5.5 Intl.NumberFormat / DateTimeFormat helpers
> - P2.5.6 backend errorCode → `t('errors.<code>')`
> - P2.5.7 PDF/Excel/XML i18n (gated на Phase 4 реално reports)
>
> **Phase 5 остаток (design работа потребна):**
> - P5.2.3 bulk receipt from invoice
> - P5.2.4 bulk shipment from FG selection + EX
> - P5.3.4 article picker "A"-суфикс варијанти
>
> **Phase 6 остаток:**
> - P6.12 consistent response envelope (big refactor across controllers)
> - P6.36 waste/calculations UI wiring (MRN consumption meter, waste-slot preview, advisory panel)
> - P6.37.13 user visual smoke (пер-роля)
> - P6.37.15 full-app a11y audit
>
> **Phase 7:** Flutter mobile — 0% started.
>
> ### ⚠️ Потенцијални ризици / забелешки
>
> - Integration тестовите **не се извршени локално** во оваа сесија (нема Docker Desktop). CI runner на GitHub Actions треба да ги провери — види последен run на `main`. Ако CI е жолт/црвен, тоа е првиот блокер.
> - Working tree и понатаму има **16 deleted legacy `*_COMPLETE.md`** + `docs/*.md` deletions од пред-сесиски cleanup што никој не го committnuvao. Проверка со корисник пред било каков `git add -A`.
> - VPS `.env` содржи `OPENAI_API_KEY` + `SQL_SA_PASSWORD`. Backup `.env.bak.p641`. Не логирај их.
>
> ### 🧰 Quick facts за hydration
>
> - HEAD: `c5db4c6` (main). Last 5 commits се сите од денес, сите од оваа сесија.
> - VPS deploy flow: `ssh root@173.212.254.216 → cd /opt/apps/LON/LON-test && git pull && docker compose build <svc> && docker compose up -d <svc>`.
> - SSH мора со explicit key path (бидејќи Windows HOME има cyrillic): `ssh -i "$HOME/.ssh/id_ed25519" -o UserKnownHostsFile="$HOME/.ssh/known_hosts" -o StrictHostKeyChecking=no root@173.212.254.216 "..."`.
> - Admin: `admin / Admin123!`. TEKSPORT test users: `Test123!` (tek-customs / tek-wh-op / tek-operator / tek-qc / tek-hr / tek-maint / tek-finance / tek-mgr).

> **>>>** **2026-04-20 — Phase 5 autonomous sweep: 7 tasks shipped + VPS green (HEAD `031f0f3`) (заменето погоре)**
>
> **All closed this session (3 batched commits):**
> - **P5.2.7** Mass location change — `POST /api/wms/inventory/mass-transfer[/preview]` + `/warehouse/transfers` page (preview→confirm). 3 integration tests. i18n × 4.
> - **P5.2.5** FEFO auto-pick per-tenant flag — `Tenant.AllowFefoAutoPick` (default true, opt-out). `PUT /api/tenants/{id}/settings/fefo` + extended PUT body. Integration test flips flag and verifies MaterialIssue 400. Migration `P5_2_5_AllowFefoAutoPickFlag`.
> - **P5.3.3** Inflate-for-waste flag — retroactively closed; shipped with P2.2.5 I1 as `Tenant.InflateImportForWaste`.
> - **P5.3.5** Recent values dropdown — `UserFieldHistory` entity + MediatR handlers + `GET/POST /api/UserPrefs/field-history` + `useFieldHistory` hook + `RecentValuesInput` component. MassTransfer reason proof-point. Migration `P5_3_5_UserFieldHistory`. VPS-verified UsageCount bump (1→2).
> - **P5.2.8** Quick-entry bar — `POST /api/QuickEntry/execute` parses `issue/release/move/help`, dispatches to existing MediatR handlers. `/tools/quick-entry` page with ↑/↓ history + result log. 4 integration tests. VPS-verified all error paths.
> - **P5.3.1** BOM template auto-apply — `CreateProductionOrderCommand` fills BOMId + RoutingId when caller omits them (latest-Version ACTIVE + currently-valid). Integration test v1-expired + v2-current → v2 lands.
> - **P5.3.2** BOM normative override per partner — `BOM.PartnerId` nullable + `CreateProductionOrderCommand.PartnerId`. Auto-apply prefers partner-scoped over global (Version 1 partner BOM trumps Version 5 global). Migration `P5_3_2_BomPartnerOverride`. SQL-verified column exists on VPS.
>
> **Migrations live on VPS:** `P5_2_5_AllowFefoAutoPickFlag`, `P5_3_5_UserFieldHistory`, `P5_3_2_BomPartnerOverride`.
>
> **Phase 5 remaining:**
> - `P5.2.3` — Bulk receipt from invoice (not tackled; needs Shipment/Receipt auto-gen design).
> - `P5.2.4` — Bulk shipment from FG selection (same design shape as P5.2.3).
> - `P5.3.4` — Article picker with "A"-suffix variants (needs KW12 catalog rules).
>
> **Commits:** `c002fac` (P5.2.7 + P5.2.5 + P5.3.3 + P5.3.5), `23a29ef` (P5.2.8 + P5.3.1), `031f0f3` (P5.3.2). VPS tested green after each push.

> **>>>** **2026-04-20 — Phase 6 Priority-B sweep closed (HEAD `168c70e`, VPS green)**
>
> **Just done + VPS-verified this session:**
> - **P6.42** — KnowledgeBase positional-record binder fix. 4 request DTOs converted to init-only-prop records; `POST /api/knowledgebase/search` binder accepts `{"query":"..."}` + returns RAG hits. `KnowledgeBaseSearchTests` regression guard. Commit `de3a848`.
> - **P6.37.13** — `filterNavGroupsByRoles` extracted from React hook + 13 Jest tests covering the full role × group matrix. Backend role claim verified for all 8 TEKSPORT test users via VPS curl. Commit `0f91d81`.
> - **P6.38** — 4 FE pages consuming P6.30/31/34/42 backends: `/knowledge-base/search`, `/tools/import/kw12`, `/master-data/items/backfill`, `ItemImportAttributes` panel inside `ItemDetail`. `ImportWizard` now honours `?session=<id>`. Envelope unwrap fix after Chrome smoke. VPS visual: KB search returns 3 real chunks (sim 0.79–0.88), Items backfill shows 2050/450/41/1600 stats + 10 sample changes. Commits `e592224` + `cde1d4d`.
> - **P6.10** — `MasterDataController` (1372 LoC) split into 10 domain controllers under `src/LON.API/Controllers/MasterData/`. Shared `MasterDataContracts.cs` + `MasterDataMappings.cs`. URL contract unchanged; all 11 paths (items/partners/warehouses/locations/employees/workcenters/work-centers/machines/uom/boms/routings) return 200 on VPS. Commit `0a7027c`.
> - **P6.11** — Items CRUD migrated to MediatR (`GetItemsQuery`, `GetItemByIdQuery`, `CreateItemCommand`, `UpdateItemCommand`, `DeleteItemCommand`). `ItemsMediatrTests` with 4 cases (POST→GET list/by-id, PUT→refetch reflects change, DELETE→404 on subsequent get by id, unknown id→404). VPS CRUD roundtrip via curl: create→200, get→200, delete→204, get-after→404. Partners stays on direct DbContext (no business logic justifies indirection). Commits `f38a1ae` + `168c70e`.
>
> **Prior 2026-04-20 commits (from HEAD `f039bcc`):** P6.21 MaterialIssue QualityStatus coerce, P6.35 BOMsImportExecutor, P6.30 items/backfill-base-variants, P6.31 items/{id}/import-attributes, P6.34 import/presets/kw12, P6.15b Serilog JSON logs, P6.41 OpenAI key wired.
>
> **Migrations live on VPS:** `P6_32_FilteredUniqueIndexes`, `P0_3_4_DecimalPrecision_CompensatingTariffNullable`, `P6_21_QualityStatusBackfill`.
>
> **Still deferred from Phase 6 Priority-B (not blocking Phase 5):**
> - **P6.12** — uniform `{ isSuccess, data, errorMessage, errors }` response envelope for the naked-entity endpoints. Breaks `GetFromJsonAsync<List<ItemRow>>` in integration tests + `schema.d.ts`; needs coordinated FE + test refactor.
> - **Partners MediatR migration** — pure pass-through CRUD; re-evaluate when first real business rule lands (e.g. EORI validation on create).
> - **P6.37.15** — full-app accessibility audit (`design:accessibility-review`).
>
> ### 🎯 User directive (2026-04-20): move to Phase 5 starting at P2.5.4
>
> Phase 6 Priority-B sweep closed. Next work is **Phase 5 (Productivity parity) + P2.5.4 i18n retrofit**. Likely entry points:
>
> 1. **P2.5.4 i18n retrofit** — start with Dashboard, Inventory, Customs, Guarantees pages (most-used). Hardcoded strings → `t('key.path')`; add keys in all 4 locales.
> 2. **P5.1 generic importer UX polish** — 7 sub-tasks from the original Phase 5 plan.
> 3. **P5.2.x bulk transitions** — one-click pick, move-batch, release, issue-bulk on the WMS / Production pages.
>
> ### 🧭 Quick-facts still valid
>
> - VPS: `root@173.212.254.216`, app `/opt/apps/LON/LON-test`, branch `main`, HEAD `168c70e`.
> - Admin login `admin / Admin123!`. TEKSPORT test users via `Test123!`.
> - `.env` keys (VPS only, never in git): `OPENAI_API_KEY`, `SQL_SA_PASSWORD`. Backup `.env.bak.p641`.
> - Deploy flow: local commit + push → SSH VPS → `git pull && docker compose build <svc> && docker compose up -d <svc>`.
> - Serilog logs: `docker logs lon-api 2>&1 | head` — one JSON event per line with `@t`, `@mt`, `@l`, `TenantId`, `UserName`, `RequestId`.
> - FE CI build step runs with `CI=true` — warnings-as-errors. Pre-existing lint debt survives; P6.38 additions introduced zero new warnings.

> **>>>** **🎉 Phase 3 (7/7 started, 5/7 committed) + Phase 4 (6/7 done; P4.5 ECD deferred) + Phase 5 quick wins (P5.2.1 + P5.2.6) + P6.19 shipped in autonomous overnight session 2026-04-19.**
>
> **Outstanding work for the user on wakeup:**
> 1. **UAT** the new endpoints end-to-end in the UI: `/certify`, `/pee/060`, `/mozni-minusi`, `/accounts/traffic-light`, `/release`, `/issues/bulk`. Backend is live; frontend wiring lives under P2.5.4 retrofit cycle.
> 2. **Finish Phase 3 runs** — `decls` was still in-progress at commit time (≈ 480/633). After it completes, run `inventory` (aggregates LagerMaterijali's ~738 K rows by MRN/batch into InventoryBalances) then `reconcile` to produce `migration_reconciliation.html`. Commands:
>
>    ```
>    dotnet run --project src/LON.Migration -- decls --tenant TEKSPORT --lon "$CONN"
>    dotnet run --project src/LON.Migration -- inventory --tenant TEKSPORT --lon "$CONN"
>    dotnet run --project src/LON.Migration -- reconcile --tenant TEKSPORT --lon "$CONN"
>    ```
>
>    where `$CONN` is the SSH-tunnel local endpoint (`Server=127.0.0.1,11433;Database=LONDB;User Id=sa;Password=$SQL_SA_PASSWORD;TrustServerCertificate=True`).
> 3. **Frontend i18n retrofit** (P2.5.4) — the new endpoints have no UI yet.
>
> **Deferred / not attempted (explicitly out of scope for a one-night push):**
> - P5.1 generic data importer (7 sub-tasks).
> - Phase 7 Flutter mobile (whole separate app).
> - P4.5 ECD auto-pull (no test env).
> - PEE010/PEE040 variants (only PEE060 done).
>
> **Phase 6 Priority-B quick wins (if user prefers cleanup first):**
> - P6.19 — CreateProductionOrderCommandHandler missing `Add()` (simple fix, half a day with tests).
> - P6.20 — Return/FG balance consolidation (medium, async DB probe fallback).
> - P6.18 — UTF-8 Cyrillic mojibake + Tenants.Address VPS backfill.
> - P6.14 — Vector Store OOM startup crash diagnosis (no blocker, just noisy).

**Алтернативи пред Phase 3 / 6 (не-блокери):**
- **P1.7** Multi-tenant login UX (decide username@tenant / subdomain / picker).
- **P6.18** UTF-8 source encoding in KB JSON (~30 min; unblocks i18n of errorMessageMK).
- **P6.14** Vector Store OOM root-cause (non-blocking but noisy startup crash).
- ~~P0.3.4 decimal precision warnings~~ **✅ closed 2026-04-19 (commit pending)**.
- ~~LONAuthorizationItem.CompensatingTariffCode nullable mismatch~~ **✅ closed 2026-04-19** together with P0.3.4 migration.

### Recent context (2026-04-18):

- **P2.2.5 (second pass) completed** — 8 IMPORTANT gaps (I1 Tenant inflate flag, I2 landing-costs pro-rata, I3 duty-rate warning, I4 PreviousProcedureCode, I5 Box 38 required + 30/35/47 advisories, I6 currency policy doc, I7 LonProcessState enum + Imported on Receipt, I8 audit log + GET /api/audit). Single migration bundles all schema changes. VPS verified: I1 flag, I2 end-to-end (1080 adjusted + 129.06 guarantee debit), I4 "00", I5 NetWeight hard-required, I8 live on GET /api/audit. Commits `6270306/eb408c4`.
- **P2.2.5 (first pass) completed** — 7 compliance blockers (B1 MRN global uniq, B2 immutable post-Draft, B3 per-auth bond ceiling, B4 auth completion days, B5 auth % override, B6 DeclarationType IM/EX, B7 tariff-within-auth). Commits `b933078/c65216e/39ef2d6`.
- **P2.2 completed** — Sync guarantee auto-debit in handler. Commit `63bf612`.
- **P2.1 completed** — IM 4200 backend + UI + E2E verified on VPS (commit `c37b011`).
- **P1.6** — MediatR CreateUserCommand + cross-tenant provisioning (commit `59878b6`).
- **P1.5** — composite `(TenantId, Code)` unique indices (commit `2a2924d`).
- **P1.4** — global query filter isolation (commit `5cc6f72`).
- **P1.3** — JWT tenant_id claim (commit `e723f7e`).
- **P1.2-B1/B2/B3** — 41/~45 entities tenant-scoped.

### Phase 1 outcome (recap):

Two tenants run isolated. Admin can provision users under any tenant; each user's JWT scopes them to their tenant; EF global query filter hides every other tenant's rows from all reads.

### Phase 2 progress:

- [x] P2.1 IM 4200 declaration + MRN registration + LON auth enforce + status lifecycle
- [x] P2.2 Guarantee auto-debit on declaration creation (sync, hard-enforce limit)
- [x] **P2.2.5** Compliance gaps B1–B7 + I1–I8:
      - B1 MRN global uniq; B2 immutable post-Draft; B3 per-auth bond ceiling; B4 auth completion days; B5 auth % override; B6 IM/EX; B7 tariff-within-auth rule.
      - I1 TEKSPORT inflate-for-waste flag; I2 landing-costs pro-rata; I3 duty-rate lookup warning; I4 PreviousProcedureCode; I5 SAD Box 38 required + 30/35/47 advisories; I6 strict currency policy documented; I7 LonProcessState enum + Imported on Receipt; I8 audit log with JSON diffs + GET /api/audit.
- [x] **P2.3** ✅ Receipt consumes MRN. Handler pre-validates (registered + active + unexpired + no aggregate overdraw), inflates booked qty for TEKSPORT via LONAuthorizationItem.AllowedWastePercentage, atomically increments MRNRegistry.UsedQuantity, flips IsActive=false when fully used, sets LonProcessState=Imported only for 4200/5100. VPS verified: qty=40 MRN → Used=40, balance=42.1053 (5% waste), overdraw/unknown/full-consumption negatives all return 400 (commits `f557899`, `38ce54f`).
- [x] **P2.4** ✅ MaterialIssue. `CreateMaterialIssueCommand` with ResolveBalance (exact-match with Imported-priority → FEFO auto-pick with LON-first ordering), LON-mandatory batch+MRN post-resolve, state split (issued portion → sibling InventoryBalance at state=InProduction), `Type=ProductionIssue` movement, `PO.Status` Draft/Released → InProgress. VPS verified: B-CLEAN 42.1053 split to 37.1053 Imported + 5.0 InProduction; over-draw/unknown-batch/FEFO-auto-pick all behaved (commit `3aab9bb`).
- [x] **P2.5** ✅ ProductionReceipt + TraceLink. `CreateProductionReceiptCommand` books FG `InventoryBalance` at caller's location (LonProcessState=null), writes `Type=ProductionReceipt=5` movement, rolls PO.Produced/Scrap, flips Draft/Released→InProgress and InProgress→Completed on threshold (emits `ProductionOrderCompletedEvent`). TraceLinks: auto-mode links every MaterialIssue on the PO; explicit `materialConsumption` mode decrements InProduction WIP by caller-supplied qty. VPS verified: PR qty=3 → 2 TraceLinks written + FG=3 booked; over-production → 400; qty=6+scrap=1 filling the PO → Status=Completed+ActualEndDate; post-completion PR → 400 (commit `f90cdc3`).
- [x] **P2.6a** ✅ EX declaration + pro-rata guarantee credit. `CreateExportDeclarationCommand` at `POST /api/customs/declarations/export`. Added `MRNRegistry.DischargedQuantity`; seeded procedure code `3151` (Re-export of LON goods). Handler: FG decrement + InProduction-then-Imported → Exported transition (DbSet.Local consolidation for same-line splits) + TraceLink IM→EX + pro-rata Credit (`debit × dischargeQty/MRN.TotalQty`; full-discharge path settles to exactly 0). VPS verified: partial qty=8→Credit 9.56 EUR, second partial qty=2→Credit 2.39 EUR with Exported consolidation, over-discharge 50>32 remaining → 400, unknown MRN → 400 (commits `ce176bb`, `ef4f25a`, `8b91b65`).
- [x] **P2.6c** ✅ Waste declaration. `CreateWasteDeclarationCommand` at `POST /api/customs/declarations/waste`. Pool Imported-first + InProduction for the MRN (+ optional Item/Batch/Location filters), transition to `LonProcessState=Waste` via DbSet.Local-consolidated sibling, emit `Type=Adjustment` movement per drained source (shared MovementNumber = `WST-…`). No guarantee impact in v1 (waste-inflate residual is physical-only). Reason field required for audit. VPS verified: waste qty=1 on `26MK8DF9122FA1` → Imported 31.1053→30.1053 + Waste 1.0; over-waste 9999 → 400; empty reason → 400; unknown MRN → 400 (commit `50a8bd1`).
- [x] **P2.6b** ✅ Return declaration (reverse of EX). `CreateReturnDeclarationCommand` at `POST /api/customs/declarations/return`. Seeded procedure `6121`. Handler: reverse-FEFO walk of Exported → Imported/InProduction (caller choice) + FG re-intake + TraceLink Return→IM + re-Debit `imDebit × returnQty/TotalQty` (symmetric with EX credit). Decrements `MRN.DischargedQuantity`; re-activates (`IsActive=true`) previously closed MRNs; flips prior full-release Credits' `IsReleased=false`. VPS verified: return qty=4 → Discharged 10→6, re-debit 4.78 EUR (net 40.63 outstanding); over-return 999 → 400; unknown MRN → 400 (commit `95501ae`).
- [x] **P2.7** ✅ Declaration validation rules. Rules 1-3 already existed. Added: `WeightSanityRule` (hard-error: negative/zero-when-set/net>gross), `VATRateWhitelistRule` (warn for rates outside {0,5,18}), `DuplicateLineWarningRule` (warn on same ItemId+TariffCode+Country across lines), `ExchangeRateWindowRule` (hard-error ±20% from NBRM; silent skip when MKD/unset/provider null). `IExchangeRateProvider` + `NullExchangeRateProvider` stub registered in DI — real NBRM implementation is a single-line swap. 14 unit tests. VPS verified: net>gross → 400, negative net → 400, VAT=10% warning → HTTP 200 (non-blocking) (commit `ac1378e`).
- [ ] P2.6a/b/c Export, Return, Waste → Guarantee credit
- [ ] P2.7 Remaining declaration validation rules

## Phase Order (finalized 2026-04-18, user approved refined hybrid)

1. ✅ Phase 0 — VPS stabilization (DONE)
2. ✅ Phase 6 Priority-A — foundational testing + contract hygiene (DONE)
3. ✅ Phase 2.5 — i18n infrastructure (P2.5.1–P2.5.6 done; P2.5.7 partial + opportunistic retrofit)
4. ✅ Phase 1 — Multi-tenant foundation (DONE)
5. ✅ Phase 2 — First end-to-end flow incl. P2.6a/b/c + P2.7 (DONE)
6. ✅ Phase 6 Priority-B (paralелно со Phase 2+) — огромна листа, shipped
7. ✅ Phase 3 — Data migration од ELON (DONE)
8. ✅ Phase 4 — Legacy gap coverage (P4.1–P4.7; P4.5 deferred) (DONE)
9. ✅ Phase 5 — Productivity parity (P5.1–P5.3 закрилени; P5.2.3+4 shipped 2026-04-20)
10. ⬅️ **Phase 6.37 placeholder-to-real conversion** (active) — 9 customs screens down, ~65 други остануваат
11. 🆕 **Phase 7–13** — види [`docs/ROADMAP.md`](docs/ROADMAP.md) **(single source of traceability)** за сите преостанати placeholder screens распоредени во фази P7–P13 со ефорт/приоритет/зависности. Препорачан sprint редослед + DoD per phase.
12. Flutter mobile (ex-Phase 7) — bumped to a later track bundled со stabilized desktop UI.

**Следен sprint (по sprint план во ROADMAP.md):** ~~Phase 7~~ ✅ · ~~Sprint 2 Phase 8.1–8.5~~ ✅ · ~~Sprint 3 Phase 11.1/11.2/11.4/11.5~~ ✅ · ~~Sprint 4 Phase 10.1/10.2/10.5~~ ✅ · ~~Sprint 5 Phase 9.1/9.3/9.6~~ ✅ · ~~Sprint 6 Phase 12.3 + 12.2~~ ✅ · ~~Sprint 7 Phase 13.1 + 13.3 + 13.5~~ ✅ (2026-04-20; zero new entities, aggregates only. VPS шоу real TEKSPORT insights: LON auth 2691 expired, Конец Арамид short 429,764 M across 126 POs, Firma-100 132 open KW12 POs). Sprint 8+ → long-tail per ROADMAP: P8.6–P8.9, P9.2/5/7, P10.3–P10.7, P11.3/7/8, P12.4–P12.10, P13.2/4/6–10.

*Оваа секција секогаш покажува еден активен таск. Се ажурира после секој commit.*
