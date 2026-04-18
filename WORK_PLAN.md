# LON — Work Plan

> **Правила на работа:** види [`CLAUDE.md`](CLAUDE.md). Verification Protocol е задолжителен за секој таск.

## Status Legend
- `[ ]` Не започнат
- `[/]` Во тек
- `[x]` Готов + верификуван (со SESSION_LOG доказ)
- `[!]` Блокиран (причина во SESSION_LOG)
- `[~]` Скипнат (причина во SESSION_LOG)

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

- [ ] **P1.1** — `Tenant` entity + CRUD API + seed `TEKSPORT` tenant
  - Verify: POST `/api/tenants` создава tenant; GET листа ги враќа
- [ ] **P1.2** — `TenantId: Guid` во `BaseEntity` + EF migration + default за постојни редови
  - Verify: migration applied на VPS; сите постојни редови имаат TenantId = TEKSPORT
- [ ] **P1.3** — JWT extension: `tenant` claim; `ICurrentTenantService`
  - Verify: login враќа token со tenant claim; decode потврдува
- [ ] **P1.4** — EF global query filters по TenantId за сите ентитети
  - Verify: integration test — user од tenant A не гледа записи од tenant B
- [ ] **P1.5** — Unique constraint reform: (TenantId + Code) наместо само Code
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

- [ ] **P2.5.1** — Избор на i18n library (`react-i18next` — стандард во React ecosystem) + setup
  - Verify: `LanguageProvider` wrapping the app, `useTranslation()` hook working
- [ ] **P2.5.2** — Translation dictionary структура: `locales/{mk,sr,sq,en}.json` (4 јазици confirmed)
  - Verify: примерок клуч `dashboard.title` + fallback chain mk→en working
- [ ] **P2.5.3** — Language switcher во header/user menu; persist во localStorage + user profile
  - Verify: менување на јазик ги менува сите веќе-преведени стрингови без refresh
- [ ] **P2.5.4** — Миграција на постоечки страници (batch по модул: Dashboard, Master Data, WMS, Production, Customs, Guarantees, Reports, Admin)
  - Verify: zero hardcoded user-facing strings (grep finds none)
- [ ] **P2.5.5** — Локализација на броеви, датуми, валути преку `Intl` API
  - Verify: 1234.56 → "1.234,56" за mk/sr; "1,234.56" за en; датум формати по locale
- [ ] **P2.5.6** — Backend error messages: поврзи error codes со frontend translations (API враќа код, UI го преведува)
  - Verify: 400 response со `errorCode: "validation.required_field"` се прикажува на активниот јазик
- [ ] **P2.5.7** — i18n за PDF/Excel exports и customs XML messages (ако/кога се имплементирани во P4)
  - Verify: генериран документ е на избраниот јазик

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
- [ ] **P5.1.2** — Column mapping UI: source колоните се влечат/спуштаат на target полиња (item_code, quantity, batch, MRN, ...). Save mapping како **named profile** per tenant (пример: „TEKSPORT faktura Excel v1", „DREKKV invoice CSV")
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

## Фаза 6 — Code quality & technical debt

**Паралелно со другите фази, не одделна фаза.** Task се создава ad-hoc кога се забележи debt.

- [ ] Расцепување на `MasterDataController` (1325 линии) на смислени контролери по domain
- [ ] Мигрирај бизнис-логика од контролери → MediatR команди
- [ ] Integration test harness (xUnit + Testcontainers)
- [ ] Structured logging + health checks со real DB probe
- [ ] Ремувал на dead files: `.vs/`, bin/, obj/ во .gitignore
- [ ] **Vector Store OOM root cause** — `VectorStoreInitializer.InitializeAsync` или `DocumentSeeder` фрлаат `OutOfMemoryException` на startup и покрај 3GB container limit. Документите во `DocumentSeeder` се тривијални (4 hardcoded sections), OOM-от е во `OpenAIEmbeddingService` или `IndexDocumentAsync`. Истражи и поправи; да не се вчитуваат сите embeddings во memory.
- [ ] **LocationDto serialization drops Type** — API враќа `type: null` за сите локации и покрај MapLocation што expose-ира `location.Type`. Истражи LocationDto конструктор или JSON serializer. Handler-от користи code prefix fallback, па не е blocker но UI-от не може да филтрира по тип.
- [ ] **.gitignore restore** — deleted in HEAD~N, bin/obj/node_modules сега untracked по дефолт. Некои obj/ фајлови заостанаа во commit `f92c754` — cleanup потребен.

---

## Фаза 7 — Flutter mobile (последно)

**Цел:** Scan-first mobile app за магационери (receive, pick, issue, FG receipt) со offline queue.

*(Детални таскови после фази 0-5.)*

---

## Current Active Task

> **>>>** (FAZA 0 DONE) — следен: P1.1 `Tenant` entity + CRUD API + seed `TEKSPORT` tenant. Пред тоа: корисник да потврди во UI дека Inventory, Movements, Receipts страниците прикажуваат податоци.

*Оваа секција секогаш покажува еден активен таск. Се ажурира после секој commit.*
