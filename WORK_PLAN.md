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
  - [ ] **P0.3.5** — Поправи `BOM.ItemId1` shadow property (правилен FK config)
  - [x] **P0.3.6** — Тргни `version: '3.8'` од compose file (cleanup) ✅
  - [x] **P0.3.7** — Memory/CPU limits на LON контејнери (shared VPS) ✅
  - Verify: before/after evidence per sub-task во SESSION_LOG
- [ ] **P0.4** — E2E smoke test на VPS: login → create item → create warehouse → create receipt → видливо во UI
  - Verify: screencast или чекор-по-чекор screenshots
- [ ] **P0.5** — Замена на `CreatedBy = "System"` hack со `ICurrentUserService`
  - Verify: нов receipt има реален username во `CreatedBy` колоната (SQL query показува)

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
- [ ] **P2.5.2** — Translation dictionary структура: `locales/mk.json`, `locales/en.json`, и други по избор (Albanian/Serbian TBD)
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

**Цел:** Функции што го направија ELON usable 30 години.

- [ ] **P5.1** — Configurable Excel bulk importer (замена за 26 `frmTransfer<Uvoznik>` форми). Mapping конфигурабилен per tenant.
- [ ] **P5.2** — `BOMTemplate` + auto-apply при ProductionOrder creation (legacy NormativTemplO/S)
- [ ] **P5.3** — Inflate-for-waste опционална калкулација (per-tenant flag)
- [ ] **P5.4** — Article picker со „A"-суфикс варијанти за tariff differences

---

## Фаза 6 — Code quality & technical debt

**Паралелно со другите фази, не одделна фаза.** Task се создава ad-hoc кога се забележи debt.

- [ ] Расцепување на `MasterDataController` (1325 линии) на смислени контролери по domain
- [ ] Мигрирај бизнис-логика од контролери → MediatR команди
- [ ] Integration test harness (xUnit + Testcontainers)
- [ ] Structured logging + health checks со real DB probe
- [ ] Ремувал на dead files: `.vs/`, bin/, obj/ во .gitignore

---

## Фаза 7 — Flutter mobile (последно)

**Цел:** Scan-first mobile app за магационери (receive, pick, issue, FG receipt) со offline queue.

*(Детални таскови после фази 0-5.)*

---

## Current Active Task

> **>>>** P0.3.4 — Поправи decimal precision warnings (HasPrecision за 8 properties во Customs config)

*Оваа секција секогаш покажува еден активен таск. Се ажурира после секој commit.*
