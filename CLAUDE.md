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
- **VPS state:** скоро ништо не работи end-to-end. Фаза 0 = дијагностика пред било каков feature development.
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

---

## 7. Меморија и логирање — што каде оди

| Локација | Што оди тука | Животен век |
|---|---|---|
| [`memory/`](memory/) (persistent Claude memory) | Durable факти: архитектурни одлуки, user preferences, credentials pointers | Преку сесии |
| [`WORK_PLAN.md`](WORK_PLAN.md) | Фази + таскови + verification criteria + status checkboxes | Активен до крајот на проектот |
| [`SESSION_LOG.md`](SESSION_LOG.md) | Хронолошки лог: датум, таск, што е направено, како е верификувано, наод | Append-only, никогаш не се брише |
| Commit messages | Специфично за промените во code base | git history |

**Правило:** Ако ми повторно го бараш истиот контекст (VPS access, SQL credentials, test tenant), тоа значи дека нешто не е зачувано правилно → поправи ја меморијата веднаш.

---

## 8. Рабочна рутина (секоја сесија)

1. **Прочитај на старт:** `MEMORY.md` (автоматски), `CLAUDE.md`, `WORK_PLAN.md`, последни 2-3 записи во `SESSION_LOG.md`.
2. **Избери еден таск** од WORK_PLAN — најраниот `[ ]` или `[/]` во најраната отворена фаза.
3. **Планирај пред да кодираш** — 2-3 реченици: што ќе направам, кои фајлови, како ќе верификувам.
4. **Имплементирај.**
5. **Верификувај** по Verification Protocol (секција 3).
6. **Запиши** SESSION_LOG + WORK_PLAN status + memory updates.
7. **Commit** со `phase-X.Y: ...` формат.

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

*Последна ревизија: 2026-04-18 — иницијална верзија.*
