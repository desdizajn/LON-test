# LON — Оперативен прирачник за Claude

> Овој документ е **правила на игра** за сесиите со Claude. Се почитуваат строго. Ако отстапување е потребно — експлицитно се бара дозвола од корисникот.

---

## 1. Контекст (еден параграф)

**LON** е нова multi-tenant SaaS апликација што ја заменува **ELON** — 30-годишна Access/VBA апликација за **увоз за облагородување** (inward processing) царинска постапка. Тим: **еден корисник + Claude** за развој; **експерт од областа (постои во фирмата што користи ELON во продукција)** за тестирање и валидација. Stack: .NET 8 clean architecture + React/TS + Flutter + SQL Server + Docker + OpenAI RAG. Целосна анализа на legacy во [`docs/ELON_Research/`](docs/ELON_Research/). **Спецификацијата за финалната апликација е во [`BLUEPRINT.md`](BLUEPRINT.md) — single source of truth.** Roadmap до v1 е во [`PLAN.md`](PLAN.md).

> ⚠ Стариот [`ELON_Blueprint.md`](ELON_Blueprint.md) (Март 2026) е **архивиран — не e авторитет**. Користи го само за legacy context.

---

## 2. Принципи (non-negotiable)

1. **Целосно верификуван таск пред да се премине на следниот.** Не „ова треба да работи". Или работи со доказ, или не е готов.
2. **Build + test со реални податоци + верификација на VPS.** Локално работи ≠ готово. Продукциско однесување = VPS.
3. **Без кратенки.** Темелни, интуитивни, корисни, интелигентни решенија. Не mocks наместо вистинска интеграција. Не TODO-коментари наместо код. Не hack-workarounds наместо root cause fix.

---

## 3. Verification Protocol

**Таск НЕ Е ГОТОВ додека сите овие не се checked:**

- [ ] `dotnet build` поминува без warnings за новите фајлови
- [ ] `npm run build` (frontend) поминува без errors
- [ ] EF migration (ако има DB промени) се создава и аплицира локално
- [ ] **OpenAPI → TS types regenerated** ако е допрена DTO/command (`./scripts/gen-api-types.sh`) и commit-ирани
- [ ] **Integration test** за business logic — види Contract Hygiene Protocol
- [ ] Manual smoke test со **реални податоци** (TEKSPORT или мигрирани од ELON)
- [ ] **Deploy на VPS** — git push + rebuild + restart контејнери
- [ ] **Verify на VPS** преку UI или curl/Postman — screencast или конкретен output
- [ ] Запис во [`SESSION_LOG.md`](SESSION_LOG.md) со доказ
- [ ] Status во [`WORK_PLAN.md`](WORK_PLAN.md) обновен од `[/]` во `[x]`

**Ако било кој чекор се прескокнува — мора да се образложи во SESSION_LOG со причина.**

### Contract Hygiene Protocol (вграден после P0.4/P0.6 bug-ови)

Шест plumbing bug-ови во Phase 0 ја изложија следната рупа: Claude тестираше API со СОПСТВЕН curl payload, не со payload-от што form-от реално го испраќа. Правилата се обврзувачки:

1. **Кога допираш DTO/command/handler** — grep frontend за callers пред да кажеш „готово":
   ```bash
   grep -r "createReceipt\|wmsApi\." frontend/web/src
   ```
   Прочитај го **handleSubmit**-от и тренинг што навистина се испраќа.
2. **Кога менуваш ANY OpenAPI-exposed DTO** — изврши `./scripts/gen-api-types.sh` + commit. CI gate fail-а ако не го направиш.
3. **Секој нов/изменет handler** бара integration test во `tests/LON.IntegrationTests/`. Pattern: `POST endpoint` → `GET` → assert DB state (не само HTTP 200). Види `ReceiptFlowTests` како примерок.
4. **UI change** (form/page) — пред deploy, користи Claude Preview tools локално (`preview_start`, `preview_fill`, `preview_click`) за smoke. `npm run build` поминува ≠ UI работи.
5. **Нова ентитет/DbSet** — провери (a) е во `ApplicationDbContext`, (b) е експониран во `IApplicationDbContext`. MediatR handlers не можат да зачувуваат преку интерфејс што не ги изложува.

---

## 4. Средини (Environments)

| Средина | Детали | Како се користи |
|---|---|---|
| **Local dev** | Windows 11, SQL Server express со Windows auth, working dir: `C:\Users\БобанКозаров\Documents\LON-test` | `docker compose up` за integration; `dotnet run` за API-only |
| **VPS (production-test)** | Contabo, `root@173.212.254.216`, домен `elon.elbosoft.click`, app path `/opt/apps/LON/LON-test`, Caddy reverse proxy + auto SSL | Сите промени **секогаш** се deploy-ираат тука. Не се прашува „дали". Passwordless SSH од local `~/.ssh/id_ed25519`. |
| **Local LON DB** | Локален SQL Server, Windows Authentication, база: `LONDB` | LON development DB. Овде се применуваат EF миграции локално (`dotnet ef database update --project src/LON.Infrastructure --startup-project src/LON.API`). Recreate: `sqlcmd -S localhost -E -Q "CREATE DATABASE LONDB;"` потоа `dotnet ef database update`, потоа `cd src/LON.API && ASPNETCORE_ENVIRONMENT=Development dotnet run` еднаш да го triggerа seedот. |
| **Legacy ELON DB** | Локален SQL Server, Windows Authentication, база: `ELON` | Read-only за миграција и споредба. НИКОГАШ не се менува. ⚠ Локалниот ELON DB е TEKSPORT-only slice од 31 табела — не цел legacy schema. Master-data + tariff catalogues (`KnigaNai`, `Aneksi`, `Preferencijal`, `tblFirmi`, `tblKorisnikTEKSPORT`) недостасуваат тука; ќе се бараат export од Teksport prod во Phase 21. |

### Local DB конекции
```
# LON (dev — write)
Server=localhost;Database=LONDB;Trusted_Connection=True;TrustServerCertificate=True;

# Legacy ELON (read-only за миграции и споредба)
Server=localhost;Database=ELON;Trusted_Connection=True;TrustServerCertificate=True;
```

---

## 5. Клучни факти

- **Test tenant:** `TEKSPORT` (мапира на истоимениот Uvoznik во legacy ELON). Локален ELON DB е TEKSPORT-only slice — `Uvoznik` колоната е NULL свугде; tenant discriminator се извлекува „DB-as-a-whole IS the tenant".
- **TEKSPORT legacy quirks:** inflate-for-waste на import (`KolMat * 100/(100-otpad%)`) — реално користен на само 4 articles (од 8,960 materials, max 2%); зачувуваме како feature flag, default OFF. Invoice staging deletion после transfer — мора да се преслика ако сакаме bit-by-bit споредба.
- **Состојба на проектот (May 2026):** Фази 0–15 ги поставија ядрата (~31.8k LoC backend, 122 FE pages, 154 [Fact] integration тестови, 174 BE routes, 85 FE endpoints — 100% покриеност). **Фаза 16 ЗАВРШЕНА** (cleanup + UI foundation — 13/13). **Сега сме во Фаза 17** (ClientOrder hub + flow wiring + AI helper; започнува со **Phase 17.PRE** — migration foundations + Z2779 happy-path) — види секција 11.
- **EF migrations:** 52 applied (последно: Phase 17 §E7.6 `P17_E7_6_AddDeliveryNote` — DeliveryNote entity + per-tenant SQL SEQUENCE). Recreate count со `ls src/LON.Infrastructure/Migrations/*.cs | grep -v Designer | grep -v ModelSnapshot | wc -l`.
- **Multi-tenant од почеток:** секоја нова ентитет мора да има `TenantId`. Секој нов query мора да биде tenant-scoped.

---

## 6. Дефаултни однесувања (без прашање)

Овие се автоматски — **НЕ ПРАШУВАЈ** на секоја сесија:

- **Секоја промена се деплојира на VPS.** Не „дали да деплојам?" — секогаш да.
- **Секоја нова domain entity добива `TenantId`** + query filter.
- **Секоја команда/query** оди преку MediatR (не директно DbContext во контролер).
- **Секоја SQL миграција** се креира преку `dotnet ef migrations add` (не manual SQL scripts).
- **Секој user-facing string во React** оди преку `t('key.path')`. Hardcoded Macedonian/English во JSX = забрането. Ако клучот го нема во `locales/*.json`, додади го во сите 4 јазика.
- **По секој таск**: SESSION_LOG запис + WORK_PLAN status update + релевантна меморија во `memory/`.
- **Commit message:** `phase-X.Y: <краток опис>` — за да може да се проследи по фаза.

### 6.1 UI дефаулти (вградени со Phase 16)

- **Една таблица:** нови табелa-views користат `components/common/DataTable.tsx`. Никаков handcrafted `<table>` за нови страници.
- **Data fetching:** нови страници користат **react-query** (`useQuery`/`useMutation`) преку hooks во `frontend/web/src/hooks/queries/`. Не нов raw `useEffect + fetch` pattern.
- **Стилирање:** нови страници користат **MUI** + design tokens. Никакво нов inline `style={{}}` (>3 reда), никакви нови ad-hoc bootstrap-style CSS класи. Постоечки страници се мигрираат само кога ги допираш за друга причина (не „рerite-everything" rampage).
- **Forms:** нови форми со 3+ полиња користат **react-hook-form** + `components/forms/Form*` wrappers (или MUI еквивалент).
- **Browser storage НЕ Е backend.** `localStorage`/`sessionStorage` смее да зачува UI prefs (selected filter, theme). Никогаш бизнис податоци (escalations, risks, certs). Ако нема BE entity → не имплементирај, отвори таск во Phase 16.
- **navGroups.ts мора да не лаже.** `backendStatus: 'exists'` значи: има handler + endpoint + DB persistence. Ако е `localStorage`-only → `partial`. Ако нема handler → `missing`.

---

## 7. Меморија и логирање — што каде оди

| Локација | Што оди тука | Животен век |
|---|---|---|
| [`memory/`](memory/) (persistent Claude memory) | Durable факти: архитектурни одлуки, user preferences, credentials pointers | Преку сесии |
| [`WORK_PLAN.md`](WORK_PLAN.md) | Активни фази + таскови + verification criteria + status checkboxes | Активен до крајот на проектот |
| [`AGENT-PROMPTS.md`](AGENT-PROMPTS.md) | **Phase 16 task prompts** — копи-пастабилни промптови за Claude Code сесии. Еден промт = еден таск. Самосодржани (Claude Code нема пристап до оваа conversation history). | Активен низ Phase 16 |
| [`VERIFICATION.md`](VERIFICATION.md) | **Phase 16 verification checklists** — точни команди, URLs, SQL queries за докажување дека таскот е готов. Се повикува од секој AGENT-PROMPTS запис. | Активен низ Phase 16 |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | **Roadmap за P7–P13** — детални табели за секоја placeholder страница: ефорт, приоритет, зависности. Single source of traceability за преостанатите ~65 screens. | Активен до крајот на Phase 13 |
| [`SESSION_LOG.md`](SESSION_LOG.md) | Хронолошки лог: датум, таск, што е направено, како е верификувано, наод | Append-only, никогаш не се брише |
| Commit messages | Специфично за промените во code base | git history |

**Правило:** Ако ми повторно го бараш истиот контекст (VPS access, SQL credentials, test tenant), тоа значи дека нешто не е зачувано правилно → поправи ја меморијата веднаш.

---

## 8. Рабочна рутина (секоја сесија)

### 8.1 ПРЕД првата реплика на корисник — задолжителна hydration

Корисникот имал лошо искуство со сесии што почнуваат „како првпат". **НЕ ПРАШУВАЈ** за VPS, креденцијали, тестово окружување, одлуки — сето тоа е запишано. Прочитај задолжително во овој ред:

1. `MEMORY.md` (автоматски loaded) — pointer записи, следи ги сите што се релевантни.
2. Овој `CLAUDE.md` — правила + environments + защо defaults.
3. **`BLUEPRINT.md` §1, §3, §5, §10** — vision, domain model, business flows, roadmap. Single source of truth за *што* апликацијата мора да биде.
4. **`PLAN.md`** — текoven статус + delta per BLUEPRINT секција + tasks per phase. Single source за *како* стасуваме до v1.
5. **`AGENT-PROMPTS.md`** — ако ова е Claude Code сесија: најди го матичниот промпт за тaскот од име/ID. Самосодржани (не реferирaj назад на conversation history).
6. **`VERIFICATION.md`** — за тековниот таск, пред да започнеш и пред да кажеш „готово".
7. Последни 3–5 записи во `SESSION_LOG.md` — последен контекст + докази + discoveries.

Ако корисникот даде нешто специфично, тоа се надополнува врз оваа база. Ако нема, тргни од најраниот `[ ]` таск во `PLAN.md` § активна фаза.

> **Конфликт-резолуција:** ако BLUEPRINT и WORK_PLAN.md (legacy) се разликуваат → BLUEPRINT победува. Ако SESSION_LOG ажурира одлука што не е во BLUEPRINT → се проможегира на BLUEPRINT во следна review сесија (не „on the fly" од Claude Code).

### 8.2 Taskun-циклус

1. **Избери еден таск** од WORK_PLAN — Current Active Task или најраниот `[ ]` во најраната отворена фаза.
2. **Планирај пред да кодираш** — 2-3 реченици: што ќе направам, кои фајлови, како ќе верификувам.
3. **Имплементирај.**
4. **Верификувај** по Verification Protocol (секција 3).
5. **Запиши** SESSION_LOG + WORK_PLAN status + memory updates.
6. **Commit** со `phase-X.Y: ...` формат + push.

---

## 9. Што ако нешто се скрши во продукција / се појави блокер

1. **Не се обидувај да маскираш.** Забележи го блокерот во SESSION_LOG како `[!]` во WORK_PLAN.
2. **Root cause analysis** — не brute-force workarounds.
3. **Ако блокерот е надворешен** (VPS недостапен, ELON DB офлајн): паузирај таскот, отвори нов `P0.X` таск за блокерот.
4. **Никогаш git force push, никогаш `--no-verify`, никогаш `reset --hard`** без експлицитна дозвола.

---

## 10. Комуникација

- **Prose:** Macedonian (Cyrillic). Технички термини: English.
- **Code, comments, commit messages, docs headers:** English.
- **Кратко и конкретно.** Без executive summaries на крајот на секој одговор. Diff-от зборува за себе.
- **Не гласај „ова работи"** без верификација. Секогаш покажи како провери.

---

## 11. Phase 17 — ClientOrder hub + flow wiring + AI helper (тековна фаза)

> Phase 16 ЗАВРШЕНА 2026-05-11 (13/13 sub-tasks; cleanup + UI foundation). Phase 17 е најголемиот path-to-v1 chunk: ClientOrder концептот (top-level „order from customer" entity) + hub-and-spoke UI + AI helper + domain events + SEQUENCE + audit + soft-delete + Playwright E2E. Сите промптови во [`AGENT-PROMPTS.md`](AGENT-PROMPTS.md) §E. Сите verification listи во [`VERIFICATION.md`](VERIFICATION.md) §E.

### 11.1 Phase 17.PRE — Migration foundations + Z2779 happy-path (PRE-фаза; започната 2026-05-12)

PRE phase се додаде врз основа на prep session findings (commit `3fe77c3`): локален ELON DB recon откри 9 контрадикции со BLUEPRINT §9.1; локалниот LON DB не постоеше; mapping doc немаше. Изградба на E0–E15 врз тоа значеше rework во Phase 21. Затоа: фиксирај mapping + локализиран happy-path ПРЕД E0.

**Канонски happy-path candidate:** `Zaklucok 2779` (OdobrenieRBr=1, single producer, 13 import lines → 5-line BOM → 1 Izdatnica → fully razdolzeno, нула orphan refs). Z2802 во резерва за multi-producer stress (Phase 17.E + Phase 21). Z2780 за daily smoke.

| PRE-таск | Опис | Статус |
|---|---|---|
| **PRE.1** | Корекции на CLAUDE.md §4/§5 (local LON DB row restored as `LONDB`; migration count 43→50; Фаза 16→17) | `[x]` (`6e27a88`) |
| **PRE.2** | BLUEPRINT §9.1 mapping update врз основа на 9 откритија + Cowork audit closeout (Izdatnica/Ispratnica fix во §5.7/§5.9/§5.10; inflate-for-waste reality; sticky-defaults reframe; HR caveat) | `[x]` (`6e27a88`+`7e67f1e`) |
| **PRE.3** | 6 user decisions resolved 2026-05-12: D1=wipe approved, D2=env-var admin password, D3=local DB created (this session), D4=new `CommercialInvoice` entity (§3.2.1), D5=new `DeliveryNote` entity (§3.8), D6=Phase 21 prod-export for HR | `[x]` |
| **PRE.4** | `docs/migration/MAPPING.md` — authoritative legacy→LON mapping (table-by-table со колумни + transformations + edge cases + reconciliation queries) | `[x]` (`4847d43`) |
| **PRE.5** | Executed VPS wipe (backup: `LONDB_pre-wipe_20260512T091454Z.bak`; all business tables empty; 50 migrations preserved) — D1 approved | `[x]` (`5f07cb2`+`9b0967b`) |
| **PRE.6** | Env-var admin password infrastructure (`LON_BOOTSTRAP_ADMIN_PASSWORD`) deployed; VPS seed verified (admin login HTTP 200, 30 permissions, 12 roles, 9 users) — D2 satisfied | `[x]` (`4b9170a`) |
| **PRE.7** | LON.Migration discovery + structural-mismatch findings (`docs/migration/PRE7_FINDINGS.md`); full Z2779 happy-path **deferred to new Phase 17 task `E.MIGRATE`** (after E1+E5+E7.6+E8.5) | `[x]` |

✅ **PRE phase CLOSED 2026-05-12.** Phase 17 main starts with §E0.

### 11.2 Phase 17 main — E0–E15 (after PRE)

Сите 16 промптови во `AGENT-PROMPTS.md` §E0–§E16:

| Таск | Опис | Status |
|---|---|---|
| E0 | `useStickyDefaults` hook + `BulkFieldUpdateButton` + bulk-update endpoint pattern | `[x]` (`06e6019`) |
| E1 | `ClientOrder` entity + migration + handlers + endpoints (+ FK to CustomsDeclaration/ProductionOrder/Shipment) | `[x]` (`2d166d8`) |
| E2 | ClientOrder list + hub UI shell (action launcher placeholder) | `[x]` (`792361e`) |
| E3 | Wire IM declaration creation from hub | `[x]` (`6e2add6`) |
| E4 | Wire Receipt from hub | `[x]` (`5ee4785`) |
| E5 | Wire BOM + ProductionOrder from hub | `[x]` (`38f2b93`) |
| E6 | Wire Podelba from hub | `[x]` (`16f8711`) |
| E7 | Wire MaterialIssue + ProductionReceipt from hub | `[x]` (`d47f973`) |
| E7.5 | Department + Position lookup promotion (CodeListItem categories) — Path B: schema + UI shipped Phase 17, backfill defers to **Phase 21.1.1** when prod-ELON export lands | `[x]` (`e50c3dd`) |
| E7.6 | `DeliveryNote` entity + polymorphic auto-gen on commit events (D5; replaces legacy `Propratnici`) | `[x]` (`1c21599`+`607eb9e`) |
| E8 | Wire EX declaration + Shipment + QC from hub | `[ ]` |
| E8.5 | `CommercialInvoice` entity + EX hub chain (D4; replaces legacy `tblIzvozniFakturi`) | `[ ]` |
| E9 | Razdolzuvanje view per ClientOrder | `[ ]` |
| **E.MIGRATE** | **LON.Migration refactor + Z2779 end-to-end + 6 reconciliation queries** (deferred from PRE.7; see `docs/migration/PRE7_FINDINGS.md` §6) | `[ ]` |
| E10 | AI helper service + 3 core recommendations + floating UI | `[ ]` |
| E10.5 | AlertRule + AlertEvent + 6 predefined rules + nightly evaluator | `[ ]` |
| E11 | Domain events infrastructure + handler refactor | `[ ]` |
| E12 | SQL SEQUENCE objects + NumberFormatter | `[ ]` |
| E13 | Audit interceptor + AuditLogEntry writes + /admin/audit-log UI | `[ ]` |
| E14 | Soft-delete global filter + recycle bin UI | `[ ]` |
| E16 | FxRate entity + manual maintenance UI | `[ ]` |
| E15 | Playwright E2E happy-path (scripted Z2779 flow) | `[ ]` |

### 11.3 Phase 17 правила (надополнуваат секција 3 Verification Protocol)

- **Z2779 е канонски fixture.** Секој E-таск треба да го smoke-тестира врз Z2779-state локално + VPS пред да каже „done". Synthetic test data е дозволена само за edge cases (concurrency, tenant isolation).
- **MAPPING.md е single source of truth за legacy→LON.** Ако E-таск открие нова mapping недоречност — append во MAPPING.md веднаш, не „следна сесија".
- **BLUEPRINT.md е авторитет.** Ако PLAN.md и BLUEPRINT.md се разликуваат, BLUEPRINT победува. Ако SESSION_LOG ажурира одлука што не е во BLUEPRINT → се promovира во следна review сесија (не „on the fly").
- **Никаква нова `localStorage` употреба** за бизнис податоци. UI prefs (filter selection) ОК — се означуваат со prefix `lon.ui.*`.
- **`DataTable` + react-query + MUI + react-hook-form** се пресудени стандарди. Нови страници не отвораат custom patterns.

---

## 12. BLUEPRINT.md (active)

[`BLUEPRINT.md`](BLUEPRINT.md) е single source of truth за финалната v1 апликација (2026-05-11 верзија). [`PLAN.md`](PLAN.md) опишува *како* стасуваме до тоа. Стариот `ELON_Blueprint.md` (Март 2026) е архивиран — користи го само за legacy context.

---

*Последна ревизија: 2026-05-13 — Phase 17 §E7.6 закучен (DeliveryNote entity + polymorphic auto-gen on MaterialIssue commit; UI list+detail with Confirm/Cancel/Print; HTML cover-sheet at `/pdf`; VPS-verified end-to-end на CO-2026-000001 → DN-2026-000001 со Draft→Sent transition). EF migration count 51→52. Next: §E8 (EX + Shipment + QC) или §E8.5 (CommercialInvoice).*