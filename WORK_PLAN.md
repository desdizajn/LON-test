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
- [ ] **P0.3.4** — Fix decimal precision warnings (HasPrecision за 8 properties)

**Phase 2.5 i18n retrofit (паралелно со Phase 2+):**
- [ ] **P2.5.4** — Retrofit на постоечки страници (Dashboard, WMS, Production, Customs, Guarantees, Reports, Advanced, Master Data, Admin) — кога се допираат
- [ ] **P2.5.5** — `Intl.NumberFormat` + `Intl.DateTimeFormat` helpers
- [ ] **P2.5.6** — Backend error codes → `t('errors.<code>')`
- [ ] **P2.5.7** — PDF/Excel/XML i18n (gated on Phase 4)

**Phase 6 Priority-B (паралелно со Phase 2+):**
- [ ] **P6.10** — Split `MasterDataController` (1325 LoC → ~8 domain controllers)
- [ ] **P6.11** — Selective MediatR migration (почни: Items + Partners)
- [ ] **P6.12** — Consistent API response shape `{ data, errorMessage?, errors[]? }`
- [ ] **P6.13** — `LocationDto.Type` drops value during serialization (bug)
- [ ] **P6.14** — Vector Store OOM root cause (startup crash и покрај 3GB memory)
- [ ] **P6.15** — Structured logging (Serilog JSON) + real health checks (`/health/ready`, `/health/live`)
- [ ] **P6.16** — DataProtection XML encryptor warning (cert-based или DPAPI-like)
- [ ] **P6.18** — UTF-8 source encoding bug (Cyrillic literals → CP1251 mojibake в DB; affects Tenants.Address од P1.1)

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
  - [ ] **P0.3.4** — Поправи decimal precision warnings (HasPrecision за 8 properties)
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
- [ ] **P1.6** — User ↔ Tenant assignment + UI за tenant switcher (super-admin)
  - Verify: super-admin може да смени activен tenant; реголни user-и гледаат само свој

**Фаза 1 DONE = ✅ два tenant-а (TEKSPORT + TEST), изолирани**

---

## Фаза 2 — Еден end-to-end flow (увоз за облагородување 42 00)

**Цел:** Комплетен циклус за еден TEKSPORT пример: увоз → магацин → производство → извоз, со гаранција коректно раздолжена.

- [ ] **P2.1** — `CustomsDeclaration` IM 42 00: внес на ставки → MRN генерирање → регистрација
  - Verify: creirana деклараcija во UI, MRN видлив во MRNRegistry
- [ ] **P2.2** — `GuaranteeLedger` auto-debit на declaration event
  - Verify: GuaranteeAccount.Balance се зголемува за износот на декларацијата
- [ ] **P2.3** — `Receipt` + consumes MRN → `InventoryBalance` со batch+MRN
  - Verify: InventoryBalance row со правилни batch/MRN; `MRNRegistry.UsedQuantity` се зголемува
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

- [ ] **P3.1** — Migration tool (конзола app `src/LON.Migration` или Python во `scripts/migration/`)
  - Verify: CLI работи со ELON Windows auth; dry-run мод печати што ќе мигрира
- [ ] **P3.2** — Mapping: `tblArtikli` (TEKSPORT filter) → `Item`
  - Verify: sample check — 10 articles од ELON се идентични во LON (ArtKatBr, naziv, HS code, tip)
- [ ] **P3.3** — Mapping: `tblFirmi` → `Partner`
  - Verify: број на Partners = број на distinct Firmi за TEKSPORT во ELON
- [ ] **P3.4** — Mapping: `Odobrenie` + `Zaklucok` → `LONAuthorization`
  - Verify: за една Zaklucok — сите authorization детали совпаѓаат
- [ ] **P3.5** — Mapping: `FakturiU5` + `FakturiU5Art` → `CustomsDeclaration` + Lines
  - Verify: една U5 faktura мигрирана — сите лини, količini, MRN-ови, davacki матхат
- [ ] **P3.6** — Mapping: `LagerMaterijali` + `LagerGotoviProizvodi` → `InventoryBalance` + `InventoryMovement`
  - Verify: сума на lager за TEKSPORT = сума на InventoryBalance (разлика = 0)
- [ ] **P3.7** — Reconciliation report: една Zaklucok — ELON vs LON side-by-side
  - Verify: HTML/Excel извештај покажува match на сите бројки

**Фаза 3 DONE = ✅ експертот може визуелно да потврди „LON покажува исто како ELON за оваа Zaklucok"**

---

## Фаза 4 — Legacy gap coverage

**Цел:** Недостигачките legacy features имплементирани во новата архитектура (без legacy кварц).

- [ ] **P4.1** — Zaverka workflow (царинска инспекторска сертификација)
- [ ] **P4.2** — PEE010–060 XML generation + submission queue
- [ ] **P4.3** — MozniMinusi — negative stock reconciliation report
- [ ] **P4.4** — Traffic light gauge на Guarantees UI (threshold alerts, configurable)
- [ ] **P4.5** — ECD auto-pull интеграција (ако има test environment)
- [ ] **P4.6** — 4 waste slots + Zaguba во `WasteDeclaration`
- [ ] **P4.7** — Year-indexed тарифни стапки: `TariffCodeRate` со ValidFrom/ValidTo

*(Verify criteria за секој P4.x ќе се детализира кога фазата ќе почне.)*

---

## Фаза 5 — Productivity parity

**Цел:** Функции што го направија ELON usable 30 години. Сè насочено кон **минимум/нула keystrokes** за повторувачки операции.

### 5A — Generic data importer (замена за 26 `frmTransfer<Uvoznik>` форми)

**Еден UI, сите клиенти.** Не 26 custom форми — еден конфигурабилен importer со именовани mapping profiles.

- [ ] **P5.1.1** — File upload (Excel .xlsx/.xls, CSV, TSV, XML, JSON) + format auto-detect + preview на првите 20 редови
- [ ] **P5.1.2** — Column mapping UI: source колоните се влечат/спуштаат на target полиња (item_code, quantity, batch, MRN, ...). Save mapping како **named profile** со **контекст** (tenant + target_entity + **partner/supplier** + optional label). Пример: „TEKSPORT invoice excel — MAGNA supplier". Кога корисник избере partner/supplier во import wizard, системот **авто-предлага само мапирањата сочувани за тој partner** (0 клика ако има само еден) + „Create new mapping" опција. Последно-користеното мапирање е default.
- [ ] **P5.1.3** — **Header-level defaults** — полиња што важат за сите редови (Warehouse, Location, MRN, CustomsDeclaration, Partner, Date). Корисник ги пополнува еднаш; редовите наследуваат + може line-level override
- [ ] **P5.1.4** — Transform rules per column (UPPER, TRIM, decimal comma→dot, date parse со формат, lookup на шифра → id)
- [ ] **P5.1.5** — Target entity селектор: Receipts / Items catalog / Partners / BOMs / CustomsDeclarations. Секој target има своја validation суита пред commit
- [ ] **P5.1.6** — Dry-run mode (preview на валидации + error список) + atomic commit (сè или ништо)
- [ ] **P5.1.7** — XML-specific: customs XML (PEE формати) како посебен target (ако корисникот прима XML од партнер)

### 5B — Bulk workflow actions (zero/min keystroke движења)

**Legacy inspiration:** `frmPodeliBaranjaBrz`, `frmRaspredeliPoProizvoditeliBrz`, template auto-apply.

- [ ] **P5.2.1** — **Issue all materials for Production Order** (1 клик) — систем пики по BOM, FIFO/FEFO алгоритам избира batch, креира N `MaterialIssue` редови во една операција
- [ ] **P5.2.2** — **Move batch across stages** (1 клик) — избери batch → target stage (Production / Shipping / Quarantine); сите inventory balances на тој batch се transfer-ираат
- [ ] **P5.2.3** — **Bulk receipt from invoice** (1 клик) — постоечки CustomsDeclaration + upload-наa faktura → авто-генерирање на Receipt со сите ReceiptLines
- [ ] **P5.2.4** — **Bulk shipment from FG selection** — selektiraj FG редови по item/batch/PO → креира Shipment + EX декларација во еден flow
- [ ] **P5.2.5** — **FIFO/FEFO auto-pick** — кога издаваш количина, системот автоматски го избира најстариот compatible batch/MRN (можно disable per tenant)
- [ ] **P5.2.6** — **Release Production Order** (1 клик) — Draft → Released: резервира материјали, создава Operations по Routing, calculates планирано завршување
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

- [ ] **P6.13** — **LocationDto serialization drops Type** — API враќа `type: null` и покрај MapLocation. Или DTO constructor или JSON naming policy. Handler-от го користи code prefix fallback; UI-от не може да филтрира по тип.
- [ ] **P6.14** — **Vector Store OOM root cause** — `System.OutOfMemoryException` на startup и покрај 3GB container. DocumentSeeder има само 4 hardcoded секции. Истражи `OpenAIEmbeddingService`/`IndexDocumentAsync`; streaming наместо in-memory load.
- [ ] **P6.15** — Structured logging (Serilog со JSON output) + реал health checks со DB probe (`/health/ready`, `/health/live`).
- [ ] **P6.16** — DataProtection XML encryptor warning (логови: „Key may be persisted to storage in unencrypted form"). Cert-based или DPAPI-like решение.
- [ ] **P6.18** — Fix UTF-8 source encoding for Cyrillic string literals. `.cs` files without BOM + compiler codepage guess → Cyrillic strings stored as CP1251→UTF-8 mojibake (confirmed on `Tenants.Address` from P1.1, and would have affected `Warehouses.Address` in P1.2-B3 before we switched to ASCII). Fix options: (a) add `<Features>strict</Features>` or similar MSBuild property forcing UTF-8 source detection, (b) add UTF-8 BOM to source files with Cyrillic, (c) standardize on `\u####` escapes for all non-ASCII literals. Plus one-shot UPDATE backfill for `Tenants.Address` on VPS.

### 6F — Claude self-workflow (вградено во CLAUDE.md)

- [x] **P6.17** — `CLAUDE.md` ажуриран со **Contract Hygiene Protocol** (5 точки: grep frontend при DTO change, regenerate TS на OpenAPI промена, integration test за handler, Preview tools за UI smoke, IApplicationDbContext провера при нов DbSet) ✅. VPS деталите запишани (не повеќе „TBD"). Verification Protocol прошитен со „OpenAPI → TS regenerated" и „Integration test" чекори.

**🎯 Phase 6 Priority-A ГОТОВА.** Следно: Phase 2.5 i18n setup.

---

## Фаза 7 — Flutter mobile (последно)

**Цел:** Scan-first mobile app за магационери (receive, pick, issue, FG receipt) со offline queue.

*(Детални таскови после фази 0-5.)*

---

## Current Active Task

> **>>>** P1.6: User ↔ Tenant provisioning. Currently `admin` is seeded under TEKSPORT and there's no product path to create a user belonging to a second tenant. `TenantsController` CRUD exists; `UsersController` create needs `tenantId` in payload + authorization guard (only super-admin or tenant-admin of target tenant can create).

**Scope:**
- `CreateUserCommand` (MediatR) прима `tenantId`, validates existence and caller authorization.
- `UsersController.Post` изложен под `Authorize(Roles="Administrator")`.
- Seeder: `admin` останува TEKSPORT; no second user seeded (flag issue: integration tests may need one).
- OpenAPI → TS regenerate (нова DTO).
- Integration test: admin creates `tenant-admin@DUP-CODE-TEST` user under 2nd tenant; logs in as new user; sees only DUP-CODE-TEST data.

**Алтернативи:**
- **P6.18** UTF-8 source encoding (~30 мин)
- **P0.3.4** decimal precision warnings (~15 мин)
- **P1.7** (new) — Multi-tenant login UX: decide on username@tenant-code vs subdomain vs tenant picker. Needed before we can remove the `User.Username` global uniqueness constraint.

### Recent context (2026-04-18):

- **P1.5 completed** — composite `(TenantId, Code)` unique indices; positive+negative tests passed on VPS (commit `2a2924d`).
- **P1.4 completed** — global query filter, isolation потврдено на VPS (commit `5cc6f72`).
- **P1.3 completed** — JWT `tenant_id` claim (commit `e723f7e`).
- **P1.2** — 41/~45 entities tenant-scoped; WH-TEK-VN seeded.

### Non-goals за P1.6 (одвоено):

- UI tenant switcher за super-admin — дојде кога има реална потреба.
- Multi-tenant login UX reform — P1.7 (separate decision).

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
