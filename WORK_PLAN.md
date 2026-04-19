# LON — Work Plan

> **Правила на работа:** види [`CLAUDE.md`](CLAUDE.md). Verification Protocol е задолжителен за секој таск.

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
- [ ] **P6.21** — **CreateMaterialIssue ResolveBalanceAsync returns "no inventory available" for a balance visible via GET /api/wms/inventory.** Flagged during 2026-04-19 P5.2.1 UAT. Same tenant, same auth token, same UoM, QualityStatus=0, Quantity=10, LonProcessState=1, MRN registered. Non-bulk `/issues` with exact (BatchNumber, MRN) also returns "no inventory matches…". Both handlers share IApplicationDbContext. Integration tests still pass. Likely root cause: EF closure in `ResolveBalanceAsync`'s `Where` chain misinterpreting `CurrentTenantId` on the global filter when called from a MediatR handler vs. direct controller injection, OR dual HasQueryFilter between `InventoryBalanceConfiguration.HasQueryFilter(!e.IsDeleted)` and the reflection-applied tenant-scoped filter. Repro: create PO w/ BOM, release, receipt matching UoM with MRN registered + LonProcessState=1, then POST /api/production/orders/{id}/issues with exact batch/mrn.
- [x] **P6.22** ✅ — **KW12 gap G1: `ProductionOrders` import target.** New `ProductionOrdersTargetSchema` + `ProductionOrdersImportExecutor`. Groups rows by `workOrderNumber` → 1 PO per group + 1 ProductionOrderMaterial per row. VPS verified on 3-WO Matriks slice: 210 rows → 3 POs + 210 materials = 213 entities committed atomically (commit `69471b2`).
- [x] **P6.23** ✅ — **KW12 gap G2: `CustomsDeclarationLine.IsPreferentialOrigin` column.** Nullable bool (null = unknown). Added to CustomsDeclarations target schema as row-level field; executor populates from the resolved row (commit `69471b2`).
- [x] **P6.24** ✅ — **KW12 gap G3: `ProductionOrderMaterial.PreAssignedMRN/Batch + EfficiencyFactor`.** New nullable columns + configuration. `IssueAllMaterialsCommand` honours pre-assignment (falls through to FEFO when null). `ProductionOrders` import target populates from Matriks. VPS verified: PreAssignedMRN `26MKIM10150003D7B3` + EfficiencyFactor `0.8934` persisted per material (commit `69471b2`).
- [x] **P6.25** ✅ — **KW12 gap G7: Items import executor upserts soft-deleted rows.** `IApplicationDbContext.CurrentTenantId` exposed; executor does `IgnoreQueryFilters + TenantId == Current` lookup, undeletes + refreshes fields instead of failing "already taken" (commit `69471b2`).
- [x] **P6.26** ✅ — **KW12 gap G8: `POST /masterdata/uom` default active.** `UoMRequest.IsActive` is nullable with `true` default; both POST + PUT treat null/true as IsDeleted=false, only explicit `false` soft-deletes (commit `69471b2`).
- [x] **P6.27** ✅ — **KW12 gap G9: CustomsDeclarations executor pre-checks MRN.** Separate `mrn` field in schema (falls back to DeclarationNumber). Executor pre-validates both `(TenantId, DeclarationNumber)` and `(TenantId, MRN)` uniqueness before SaveChanges (commit `69471b2`).
- [x] **P6.28** ✅ — **KW12 soft gaps S1+S2+S5** shipped (CustomerOrderNumber, WeekNumber, EfficiencyFactor). S3 (CMRNumber/ClosingNumber/CommercialInvoiceNumber) + S7 (TotalGross/TotalNet) deferred — bundle when we wire up Transport-sheet import or declaration PDF reporting.
- [x] **P6.29** ✅ — **KW12 gap G4+G5: seed STK + KO UoMs** idempotently; `BackfillKw12SupportingDataAsync` runs on every startup, adds missing rows and undeletes phantoms (commit `69471b2`). Warehouse 222 left as manual seed (tenant-specific).
- [ ] **P6.30** — **Legacy item color/size backfill.** User flagged 2026-04-19: legacy ELON had no color/size; 2 170 legacy items have NULL `BaseCode`/`ColorCode`/`SizeCode`. Run `ItemsImportExecutor.DecomposeCode` over the legacy catalog once (one-shot admin endpoint or EF core migration data script) + auto-create missing base items for newly-discovered variants.
- [ ] **P6.31** — **Per-import material attributes report.** Same material code (e.g. one cotton thread) can be imported from AT/TR/US with different tariff + preferential flag + duty rate. Data already per-line on `CustomsDeclarationLine`, but report surface is missing. Add: "for material X, what are the distinct (tariff, origin, pref, supplier) tuples across active MRN batches + aggregate qty per combo?"
- [x] **P6.32** — *2026-04-19*: shipped migration `20260419190825_P6_32_FilteredUniqueIndexes` — adds `WHERE [IsDeleted] = 0` SQL Server filtered-index predicate to 20 unique indexes spanning Items, Partners, Warehouses, Locations, WorkCenters, Machines, Employees (both), UoM, ItemUoMConversions, Routings, RoutingOperations, BOMs, BOMLines, ProductionOrders, ProductionOrderMaterials, ProductionOrderOperations, MaterialIssues, ProductionReceipts, CustomsDeclarations (both), CustomsDeclarationLines, MRNRegistries, GuaranteeAccounts, LONAuthorizations, ImportMappingProfiles, CodeListItems, DeclarationRules, TariffCodes, CustomsProcedures. Soft-deleted rows no longer block re-insert of the same value.
- [~] **P6.33** — **UI for parent-variant rollups** (KW12). Production orders list: collapsible main PA → variant children **✓ shipped 2026-04-19 (`f2eeeed`)**. Items list: color/size badges + Base column **✓ shipped (`f318f92`)**. Still TODO: toggle "show variants" at list level + aggregate by BaseCode; Inventory filters for base-article rollup; reports at both levels per user requirement.
- [ ] **P6.34** — **KW12 import wizard preset / multi-sheet upload.** User should drag `KW12.xlsx` in one go; wizard detects the 3 sheets (Matriks / Faktura / Transport) and runs them in the right order (Items → Customs declaration from Transport+Faktura → Matriks → Receipts) with defaults pre-filled (warehouseCode=222, partnerCode=TEXPORT-AT, procedureCode=4200, lonAuthorizationId=26/TEKSPORT/0001). Current: 3 manual CSV slices + 3 separate imports.
- [ ] **P6.35** — **BOMs import target with working commit path.** Schema exists, executor still a stub. Needed so Matriks-style files can produce reusable `BOM + BOMLine` records (not just per-PO `ProductionOrderMaterial`). Blocks "template auto-apply" (P5.3.1).
- [ ] **P6.36** — **Waste / calculations / controls UI wiring.** Backend (P2.3 MRN consume, P2.2 guarantee auto-debit, P4.6 waste slots, I2 landing-costs, I3 duty-rate warnings, I5 SAD advisories, P2.7 rule engine) is live but the Customs/Production/Guarantees pages don't surface them: no per-line duty breakdown, no MRN consumption meter, no waste-slot preview, no advisory panel. Needs a design pass per page (tie to P6.37).
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
- [ ] **P6.41** — **OpenAI API key missing on VPS** — surfaced 2026-04-19 after P6.14 fix. `OpenAIEmbeddingService.GenerateEmbeddingAsync` returns 401 Unauthorized, blocking Pravilnik seeding and RAG queries. Infra: set `OpenAI__ApiKey` (or whatever env var Program.cs binds) on the VPS `.env` + `docker compose up -d api`. **Requires user** — cannot extract keys from sandbox. Low priority: `VectorStoreBackgroundService` already degrades gracefully (logs 'The system will continue to function without RAG capabilities').
- [ ] **P6.38** — **Frontend catch-up sweep (umbrella).** Tracks the 2026-04-19 user feedback that ~300 backend features aren't reflected on UI. Break down per page: Dashboard KPIs, Customs (declaration detail, line editor, MRN usage meter, guarantee impact), Guarantees (ledger tree + debit/credit math + release button), Inventory (filter-by-base toggle, per-import attribute report P6.31), Production (materials table with PreAssignedMRN + EfficiencyFactor visibility, waste slots UI), MasterData (BOM builder, Routings editor, Code List browser with tariff/country/procedure tabs), Reports (per-material import breakdown). Split into subtasks once P6.37 settles the IA.
- [ ] **P6.10** — Split `MasterDataController` (1325 LoC → ~8 domain controllers)
- [ ] **P6.11** — Selective MediatR migration (почни: Items + Partners)
- [ ] **P6.12** — Consistent API response shape `{ data, errorMessage?, errors[]? }`
- [~] **P6.13** — investigated 2026-04-19: API correctly returns `locationType` field populated with enum value (e.g. `locationType: 1` for Receiving); frontend `LocationList` + `LocationInquiry` consume it correctly. Original description referenced a `type: null` bug that no longer reproduces. Likely fixed upstream; closing as not-a-bug.
- [x] **P6.14** — *2026-04-19 (commit `6cdb949` + VPS verified)*: root cause found — `DocumentChunkingService.ChunkDocument` had an infinite loop when `endIndex` clamped to `content.Length` but `startIndex = endIndex - overlap` didn't advance (same tail chunk re-emitted forever → OOM via `List<string>.set_Capacity`). Fixed by (a) breaking when `endIndex >= content.Length` after emitting the final chunk, (b) guarding forward progress with `Math.Max(endIndex - overlap, startIndex + 1)`. Added 4 unit tests in `DocumentChunkingUnitTests` as regression guard. VPS confirms: chunking now completes; next error is a **clean 401 Unauthorized from OpenAI embedding call** (not an OOM) — see new task P6.41 below.
- [ ] **P6.15** — Structured logging (Serilog JSON) + real health checks (`/health/ready`, `/health/live`)
- [ ] **P6.16** — DataProtection XML encryptor warning (cert-based или DPAPI-like)
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
- [ ] **P2.5.4** — Retrofit на постоечки страници (Dashboard, WMS, Production, Customs, Guarantees, Reports, Advanced, Master Data под-категории, Admin). Паралелно со Phase 2+ кога се допираат.
- [ ] **P2.5.5** — `Intl.NumberFormat` + `Intl.DateTimeFormat` хелпери (1.234,56 vs 1,234.56 итн.)
- [ ] **P2.5.6** — Backend error codes: командите враќаат `errorCode`, frontend `t('errors.<code>')`
- [ ] **P2.5.7** — PDF/Excel exports + customs XML i18n — кога Phase 4 ги имплементира

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
- [ ] **P5.2.3** — **Bulk receipt from invoice** (1 клик) — постоечки CustomsDeclaration + upload-наa faktura → авто-генерирање на Receipt со сите ReceiptLines
- [ ] **P5.2.4** — **Bulk shipment from FG selection** — selektiraj FG редови по item/batch/PO → креира Shipment + EX декларација во еден flow
- [ ] **P5.2.5** — **FIFO/FEFO auto-pick** — кога издаваш количина, системот автоматски го избира најстариот compatible batch/MRN (можно disable per tenant)
- [x] **P5.2.6** ✅ — **Release Production Order** (1 клик). `POST /api/production/orders/{id}/release`. Draft → Released; BOM lines scaled by `OrderQty / BaseQty × (1 + ScrapPct/100)` into ProductionOrderMaterials; Routing ops copied into ProductionOrderOperations. Already-released = idempotent success.
- [ ] **P5.2.7** — **Mass location change** — филтрирај inventory (by item/batch/PO/warehouse) → избери target location → сите се transfer-ираат
- [ ] **P5.2.8** — **Quick-entry bar** — single-line command за power users: `issue PO-123 50 BATCH-X` → auto-parse + execute

### 5C — Template auto-apply (legacy pattern)

- [ ] **P5.3.1** — `BOMTemplate` + auto-apply при ProductionOrder creation (legacy NormativTemplO/S) — zero keystrokes за repeat products
- [ ] **P5.3.2** — `NormativeOverride` per partner/tenant (различни BOM-ови per Uvoznik за ист item)
- [ ] **P5.3.3** — Inflate-for-waste опционална калкулација (per-tenant flag)
- [ ] **P5.3.4** — Article picker со „A"-суфикс варијанти за tariff differences
- [ ] **P5.3.5** — **Recent values** dropdown — полињата памтат последните 10 внесени вредности per user + date

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

- [ ] **P6.10** — Расцепи `MasterDataController` (1325 линии) на ~8 domain-focused контролери (Items, Partners, Warehouses, Locations, UoMs, BOMs, Routings, WorkCenters+Machines).
- [ ] **P6.11** — Селективна MediatR миграција: за секое read/write во контролер, командa/query преку Mediator. Почни со Items + Partners (најмногу користени).
- [ ] **P6.12** — Consistent response shape: `{ data, errorMessage?, errors[]? }` везде. Refactor controllers што враќаат голи entities.

### 6E — Follow-ups од Phase 0 (bugs забележани но не блокери)

- [ ] **P6.20** — **Imported/FG restore non-consolidation in Return handler.** `UpsertRestoredBalance` (and `UpsertFgBalance`) in `CreateReturnDeclarationCommand` probe `DbSet.Local` only; an existing DB row that isn't tracked in the current context won't match, so returns append a new sibling instead of consolidating. Aggregate sum queries are correct; storage bloats by one row per restore. Fix options: (a) add async DB lookup fallback keyed on (Item, Location, Batch, MRN, UoM, Quality, State), (b) batch all restores at the end and run one SaveChanges per (key) group. Same pattern present implicitly in `UpsertFgBalance` for return re-intake.
- [x] **P6.19** ✅ — `CreateProductionOrderCommandHandler` now calls `Add(order)` before SaveChanges (commit `8462a2d`). Missing integration test `CreateProductionOrder_Persists_VisibleInList` still a follow-up.
- [ ] **P6.13** — **LocationDto serialization drops Type** — API враќа `type: null` и покрај MapLocation. Или DTO constructor или JSON naming policy. Handler-от го користи code prefix fallback; UI-от не може да филтрира по тип.
- [x] **P6.14** — **Vector Store OOM root cause** — fixed 2026-04-19. Not embedding/IO-related at all: `DocumentChunkingService.ChunkDocument` spun an infinite loop when the final chunk clamped to `content.Length` (re-emitted same tail forever → `List<string>.set_Capacity` OOM). Patched the loop-exit + forward-progress guard; added `DocumentChunkingUnitTests` with 4 cases (empty, short, 1 050-char boundary, 120 KB Pravilnik-shape). See commit + SESSION_LOG.
- [ ] **P6.15** — Structured logging (Serilog со JSON output) + реал health checks со DB probe (`/health/ready`, `/health/live`).
- [ ] **P6.16** — DataProtection XML encryptor warning (логови: „Key may be persisted to storage in unencrypted form"). Cert-based или DPAPI-like решение.
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

> **>>>** **2026-04-19 — P6.37.14 code-complete.** Legacy flat sidebar removed; `<Navigate>` redirects for all old top-level routes; idempotent `RoleTopUpSeed` with 8 new roles + 8 TEKSPORT test users. Pending VPS deploy + per-role smoke.
>
> **Next session pick one:**
> 1. **P6.37.14 finish** — deploy to VPS, login as each of the 8 new test users, verify sidebar shows only the expected groups for each role, confirm redirects resolve.
> 2. **P6.37 consumer verification** — user spot-checks sidebar wording / grouping before it ossifies.
> 3. **Return to Phase 3/6 backlog** (below).

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
2. ⬅️ **Phase 6 Priority-A** (active): P6.1 `.gitignore` → P6.3-4 OpenAPI→TS → P6.5-7 test harness + 2-3 tests → P6.9 CI gate → P6.17 CLAUDE.md update
3. Phase 2.5 — i18n infrastructure (пред Phase 1 — Tenant CRUD UI е веднаш преведен)
4. Phase 1 — Multi-tenant foundation
5. Phase 2 — First end-to-end flow (TEKSPORT IM 42 00)
6. Phase 6 Priority-B (паралелно со Phase 2+): P6.10 split MasterDataController (combined with TenantId add), P6.11 MediatR migration per-module, P6.13-16 quick bug fixes кога природно ги допираме
7. Phase 3 — Data migration од ELON
8. Phase 4 — Legacy gap coverage
9. Phase 5 — Productivity parity
10. Phase 7 — Flutter mobile

**Phase 6 Priority-A = само ~5 таскови, 1 продуктивен ден. Не е full Phase 6.**

*Оваа секција секогаш покажува еден активен таск. Се ажурира после секој commit.*
