# LON — Оперативен прирачник за Claude

> Овој документ е **правила на игра** за сесиите со Claude. Се почитуваат строго. Ако отстапување е потребно — експлицитно се бара дозвола од корисникот.

---

## 1. Контекст (еден параграф)

**LON** е нова multi-tenant SaaS апликација што ја заменува **ELON** — 30-годишна Access/VBA апликација за **увоз за облагородување** (inward processing) царинска постапка. Тим: **еден корисник + Claude** за развој; **експерт од областа (постои во фирмата што користи ELON во продукција)** за тестирање и валидација. Stack: .NET 8 clean architecture + React/TS + Flutter + SQL Server + Docker + OpenAI RAG. Целосна анализа на legacy во [`../PdfToExcel/ELON_Research/`](../PdfToExcel/ELON_Research/). Blueprint на новата апликација во [`ELON_Blueprint.md`](ELON_Blueprint.md).

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
| **Legacy ELON DB** | Локален SQL Server, Windows Authentication, база: `ELON` | Read-only за миграција и споредба. НИКОГАШ не се менува. |

### Legacy DB конекција (за миграции и споредба)
```
Server=localhost;Database=ELON;Trusted_Connection=True;TrustServerCertificate=True;
```

---

## 5. Клучни факти

- **Test tenant:** `TEKSPORT` (мапира на истоимениот Uvoznik во ELON, чии табели се `tblKorisnikTEKSPORT`, `InvoiceTEKSPORT`, итн.).
- **TEKSPORT legacy quirks:** inflate-for-waste на import (`KolMat * 100/(100-otpad%)`), deletes Invoice staging после transfer — мора да се преслика ако сакаме bit-by-bit споредба.
- **Состојба на проектот (May 2026):** Фази 0–15 ги поставија ядрата (~31.8k LoC backend, 122 FE pages, 43 EF migrations, 154 [Fact] integration тестови, 174 BE routes, 85 FE endpoints — 100% покриеност). Сега сме во **Фаза 16 (cleanup + UI foundation)** — види секција 11.
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

Корисникот имал лошо искуство со сесии што почнуваат „како првпат". **НЕ ПРАШУВАЈ** за VPS, креденцијали, тестово окружување, одлуки — сето тоа е запишано. Прочитај задолжително:

1. `MEMORY.md` (автоматски loaded) — 14 pointer записи, следи ги сите што се релевантни.
2. Овој `CLAUDE.md` — правила + environments + защо defaults.
3. `WORK_PLAN.md` — најмалку прво 40 линии (состојба на фази) + **Current Active Task** на дното.
4. **`docs/ROADMAP.md`** — single source за преостанатите фази P7–P13; секоја placeholder-to-real конверзија има стабилен ID (`P7.1`, `P10.3`, ...), ефорт, приоритет, зависности. Провери што е следно на Sprint редоследот.
5. Последни 3–5 записи во `SESSION_LOG.md` — последен контекст + докази + discoveries.

Ако корисникот даде нешто специфично, тоа се надополнува врз оваа база. Ако нема, тргни од **Current Active Task** во WORK_PLAN.

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

## 11. Phase 16 — Cleanup + UI Foundation (тековна фаза)

> Phase 16 е реакција на искрена ревизија на кодот (May 2026): backend е здрав, frontend има хаос. Целта е **да исчистиме лажното и да поставиме UI стандард** пред да продолжиме со нови фичери. Сите промптови за таскови во оваа фаза живеат во [`AGENT-PROMPTS.md`](AGENT-PROMPTS.md). Сите verification listи живеат во [`VERIFICATION.md`](VERIFICATION.md).

### 11.1 Што нашовме (искрено)

- **Backend:** солиден. 23 controllers, 174 routes, 57 MediatR handlers, 76 DbSets, 154 [Fact]/[Theory] integration тестови, 43 EF migrations.
- **API contract:** 85 FE-called endpoints, 100% покриеност во BE routes (по case-correct match).
- **Frontend компилира clean:** 0 TS errors, 0 ESLint errors, 1 unused-import warning.
- **6 страници го лажат корисникот:** користат `localStorage` како „backend" (Escalations, OpenRisks, CostAccounting, PayrollAggregate, SupplierInvoices, Training) — `navGroups.ts` ги означи како `backendStatus: 'exists'`. Tenant cache clear = губиток на податоци.
- **Дупликат wired-страници:** `WarehousesList` (стара) и `WarehouseList` (нова) се двете во `App.tsx` под различни патишта. Стариот е dead route.
- **UI хаос:** 91 страници со inline стилови, 82 со bootstrap-y className, 20 со MUI, 6 со `DataTable`, 8 со react-hook-form — нема консистентен систем.
- **Test coverage gaps:** WMSController (25 endpoints), Analytics, Traceability, сите MasterData CRUD controllers, Users/Roles/Permissions немаат dedicated тест фајл.

### 11.2 Phase 16 sub-фази (по редослед)

| Sub-фаза | Опсег | Време |
|---|---|---|
| **A. Cleanup** | Бриши dead routes, поправи `navGroups.ts` лажни statuses, реши дупликат MasterData страници | 1–2 дена |
| **B. UI foundations** | Инсталирај react-query, мигрирај една pilot страница (`Inventory.tsx`), стандардизирај на `DataTable`, дефинирај layout shell | 2–3 дена |
| **C. localStorage → backend** | Замени 6-те лажни страници со реални BE entities + handlers + миграции | 3–5 дена |
| **D. Test gap fill** | Integration тестови за WMSController, Auth/Roles, MasterData CRUD smoke | паралелно со А-Ц |

Конкретни промптови за секоj sub-таск (А1, А2, А3, Б1, Б2, Б3, В1, В2, В3, Г1, Г2, Г3) се во `AGENT-PROMPTS.md`. Не пробувај да измислуваш sub-таскови — ако појавиш потреба, додај нов до `AGENT-PROMPTS.md` пред да го почнеш.

### 11.3 Phase 16 правила (надополнуваат Section 3 Verification Protocol)

- **Никаква нова страница** во Phase 16. Чистиме, не градиме нови фичери.
- **Никаква нова `localStorage` употреба** за бизнис податоци. UI prefs (filter selection) ОК — се означуваат со prefix `lon.ui.*`.
- **Pre-commit `tsc --noEmit` за frontend** — задолжителен (CRA build толерира warnings, ние не).
- **Кога допираш страница за refactor**: задолжителен screencast / VPS screenshot пред и потоа, во SESSION_LOG.
- **Brand-new entity во Phase 16.C** — мора да добие integration test веднаш (не „следна сесија").

---

## 12. BLUEPRINT.md (forthcoming)

Постоечкиот `ELON_Blueprint.md` (Март 2026) опишува визија која не одговара со тековниот код. Откако ќе го завршиме Phase 16, ќе се напише нов `BLUEPRINT.md` врз основа на **она што навистина постои** + **она што останува до v1**. Дотогаш не цитирај го `ELON_Blueprint.md` како авторитет за scope — користи го само за legacy context.

---

*Последна ревизија: 2026-05-11 — Phase 16 (cleanup + UI foundation).*
