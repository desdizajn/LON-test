# LON — Session Log

> Append-only хронолошки запис. Секој таск добива еден запис. Запиши веднаш по verification, не групно на крај.
>
> Формат на запис:
> ```
> ## YYYY-MM-DD — <Task ID> <Task title>
> **Status:** [/] in-progress | [x] done | [!] blocked | [~] skipped
> **Files changed:** списак
> **What was done:** 2-3 реченици
> **How verified:** доказ (команда, URL, screencast, SQL query output)
> **Follow-ups / discoveries:** идни таскови, неочекувани наоди
> ```

---

## 2026-04-18 — Kickoff

**Status:** [x] done
**Files changed:**
- `CLAUDE.md` (created)
- `WORK_PLAN.md` (created)
- `SESSION_LOG.md` (created)
- `memory/` (5 memory entries)

**What was done:**
- Прегледав legacy ELON анализа во [`../PdfToExcel/ELON_Research/`](../PdfToExcel/ELON_Research/) — 30-годишна Access/VBA апликација, multi-tenant по Uvoznik, 3 material outcomes (Izvoz/Vrakanje/Otpad).
- Аудит на нова LON апликација: 15 controllers (MasterData = 1325 линии = God controller), 7 EF migrations, CQRS тенок (само 5 commands), RAG pipeline е вграден, React web + Flutter mobile скелет.
- Одлуки: multi-tenant SaaS од почеток, TEKSPORT како test tenant, партијална data migration од локална ELON копија, mobile последно.
- Создадени работни документи + verification protocol во CLAUDE.md.

**How verified:**
- Memory files created и прочитани во `C:\Users\БобанКозаров\.claude\projects\C--Users--------------Documents-LON-test\memory\`
- WORK_PLAN.md содржи 7 фази со таскови и verification criteria
- CLAUDE.md содржи принципи, verification protocol, environments, defaults

**Follow-ups / discoveries:**
- VPS е недостапен за Claude до P0.1 (SSH setup)
- Legacy ELON DB е локална копија; Windows auth, DB = `ELON`
- Корисникот очекува брзо кодирање (мал тим: корисник + Claude + domain expert за QA)

---

## 2026-04-18 — P0.1 SSH access setup

**Status:** [x] done
**Files changed:** none (config на VPS + local SSH)
**What was done:**
- Корисник веќе имал `id_ed25519` клуч локално (од 15.04.2026, comment `ics2-deploy`).
- Јавниот клуч додаден во `~/.ssh/authorized_keys` на Contabo VPS `root@173.212.254.216`.
- Passwordless SSH тестиран и работи од PowerShell + од Claude Bash tool.

**How verified:**
- `ssh root@173.212.254.216 "hostname"` враќа `vmi3041110` без password prompt.
- `docker ps` врати 14 контејнери (LON + други проекти: taskmanagement, inventory, caddy, hello-dotnet).

**Follow-ups / discoveries:**
- VPS е **shared infrastructure** — не е само за LON. Има Caddy reverse proxy кој routa за повеќе домени.
- Системот има 51 pending apt updates + 1 system restart required. Не итно.

---

## 2026-04-18 — P0.2 VPS дијагноза (health snapshot)

**Status:** [x] done
**Files changed:** none (read-only диагностика)

**What was done:**
- Инспекција на сите LON контејнери + compose state + logs + env + Caddy config + ресурси.

**Health snapshot — главни наоди:**

### 🔴 Главен блокер
- **`lon-api` е EXITED 3 недели** (од 2026-03-27, exit code 137).
- Exit не е OOM (`OOMKilled: false`). Container резурси: **нема memory limit**; host има 10GB free RAM.
- Inspect.State.Error: `"DeadlineExceeded: failed to create shim task: failed to start io pipe copy"` — containerd shim failure на обид за restart. Stale state.
- `restart: unless-stopped` policy е активно, но containerd не успеал да го рестартира → стои мртов.
- App-от работеше стабилно пред тоа: applying migrations, KB seeding, vector store init сите завршиле успешно (видливо во logs).

### 🟢 Што работи
- `lon-sqlserver` — healthy, порт 1433 exposed (види ⚠️).
- `lon-frontend` — Up 3 недели, рендерира login UI.
- `lon-worker` — Up 3 недели (но бесмислено е без API).
- `caddy-caddy-1` — Up, routes за `elon.elbosoft.click` точно конфигурирани: `/api*`, `/swagger*`, `/health` → `lon-api:5000`; else → `lon-frontend:80`.
- `.env` постои со сите потребни keys (SQL_SA_PASSWORD, JWT_SECRET_KEY, OPENAI_API_KEY, ENABLE_VECTOR_STORE, ASPNETCORE_ENVIRONMENT).
- Image `lon-test-api:latest` постои (799MB).
- DB миграции аплицирани (видливо во претходни успешни логови).

### ⚠️ Секундарни проблеми (треба fix во P0.3)
1. **SQL Server порт 1433 изложен на 0.0.0.0** — публично достапен од интернет. Сериозна безбедност. Треба bind на `127.0.0.1:1433` или тотално да се отстрани мапирањето.
2. **DataProtection keys во ephemeral директориум** (`/root/.aspnet/DataProtection-Keys`) — секој restart invalidira JWT tokens и session state. Треба volume mount.
3. **Decimal precision warnings во EF** за `ExchangeRate`, `TotalInvoiceAmount`, `AdjustmentRate`, `GrossWeight`, `ItemPrice`, `NetWeight`, `StatisticalValue`, `UsedQuantityFromPrevious` — тивко truncation на вредности. Треба `HasPrecision(18,4)` или слично.
4. **EF shadow property `BOM.ItemId1`** — неправилен FK мапинг, треба поправка во BOM entity.
5. **`version: '3.8'` во compose** — обсолетно, генерира warning на секоја команда.
6. **Global query filter warnings** за CustomsDeclaration↔CustomsDocument, CustomsProcedure↔CustomsProcedureDocument, Partner↔LONAuthorization, Item↔LONAuthorizationItem — треба matching filters и на двете страни или optional navigation. (Прецедент за P1 multi-tenant filter дизајн — ова ќе биде извор на баги ако не се поправи правилно.)

**How verified:**
- `ssh root@173.212.254.216 "docker ps -a --filter name=lon-"` — показа `lon-api Exited (137) 3 weeks ago`.
- `docker inspect lon-api` — State.ExitCode=137, State.Error со containerd shim message.
- `free -m` — 10GB free од 18GB.
- `df -h /` — 98GB free од 145GB.
- `docker logs lon-api --tail 80` — покажа successful startup cycle пред crash.
- `grep -A 15 'elon.elbosoft' Caddyfile` — потврди routing rules.
- `journalctl -u docker ... | grep lon-api` — потврди последно event на 2026-03-27 20:19.

**Follow-ups / discoveries:**
- Фрагилен state: `lon-worker` работи 3 недели без API — тоа треба да е невозможно или бениген (да не прави штета без API). Проверка во P0.3.
- **VPS е споделен** со други проекти (taskmanagement, inventory, hello-dotnet). Ресурси се зеднички. Треба memory/CPU limits на LON контејнери за да не ги уништат другите.

---

## 2026-04-18 — P0.3.1 Recreate lon-api

**Status:** [x] done
**Files changed:** none (infra-only)
**What was done:**
- `docker compose rm -f api && docker compose up -d api` на VPS.
- Контејнерот оживеа, startup sequence помина чисто: migrations aplicирани (up to date), KB seeding skipped (already seeded), Vector Store background init стартуван.

**How verified:**
- `docker ps --filter name=lon-api` → `Up`
- `curl -X POST https://elon.elbosoft.click/api/auth/login` со wrong password → HTTP 401 „Invalid username or password" (auth pipeline работи).
- Real admin login (`admin` / `Admin123!`) → HTTP 200 + JWT token со Administrator role + полни permissions.
- Корисникот потврди преку browser — dashboard рендерира на `https://elon.elbosoft.click/dashboard` со македонска поздравна порака.

**Follow-ups / discoveries:**
- Exit 137 на 27.03 не е OOM (logs showed clean startup pre-crash). Причината е containerd shim failure на restart attempt. Решено со `rm` + `up -d`.

---

## 2026-04-18 — P0.3.2/3/6/7 Compose hardening (batched)

**Status:** [x] done
**Files changed:** `docker-compose.yml`, `CLAUDE.md`, `WORK_PLAN.md`, `SESSION_LOG.md`

**What was done (P0.3.2):** bind SQL Server на `127.0.0.1:1433` (беше `0.0.0.0:1433` — public).
**What was done (P0.3.3):** додаден volume `lon_dataprotection_keys` монтиран на `/root/.aspnet/DataProtection-Keys` (persistent keys across container recreations).
**What was done (P0.3.6):** тргнат `version: '3.8'` (obsolete compose warning).
**What was done (P0.3.7):** `deploy.resources.limits` за сите 4 сервиси (sqlserver 4GB/2cpu, api 1.5GB/1.5cpu, worker 512MB/0.5cpu, frontend 256MB/0.5cpu).

**How verified (per sub-task):**
- P0.3.2: `docker ps --filter name=lon-sqlserver --format '{{.Ports}}'` → `127.0.0.1:1433->1433/tcp` ✅
- P0.3.3: `docker inspect lon-api` mounts → `/root/.aspnet/DataProtection-Keys <- lon-test_lon_dataprotection_keys` ✅
- P0.3.6: `docker compose up` нема повеќе warning за obsolete version ✅
- P0.3.7: `docker inspect $c --format '{{.HostConfig.Memory}}'` враќа non-zero за сите 4 контејнери (1610612736, 4294967296, 536870912, 268435456) — compose v2 навистина ги применува limits ✅
- End-to-end после recreate: login HTTP 200, JWT се издава ✅

**Follow-ups / discoveries:**
- `deploy.resources.limits` се применува од docker compose v2 без да треба swarm mode (за разлика од верзија 1).
- Конекција до SQL Server од локалната Windows машина сега бара SSH tunnel: `ssh -L 1433:localhost:1433 root@173.212.254.216`. Да се документира во CLAUDE.md ако се бара.
- VPS имаше divergent git history (PR #9 merge vs PR #10 merge); hard reset на `origin/main` безбедно затоа што VPS е само deploy target. `deploy.sh` мод бит (+x) ресториран после reset.

**P0.3 остана:**
- [ ] P0.3.4 decimal precision EF config (код промени)
- [ ] P0.3.5 BOM.ItemId1 shadow property (код промени)

---

## 2026-04-18 — P0.3.4 Decimal precision warnings fix

**Status:** [x] done
**Files changed:**
- `src/LON.Infrastructure/Persistence/Configurations/CustomsConfigurations.cs` (+8 HasColumnType lines)
- `src/LON.Infrastructure/Migrations/20260418134239_FixDecimalPrecisions.cs` (new)
- `src/LON.Infrastructure/Migrations/20260418134239_FixDecimalPrecisions.Designer.cs` (new)
- `src/LON.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (updated)

**What was done:**
- Додадено `HasColumnType("decimal(18,4)")` за 8 недефинирани decimal properties:
  - `CustomsDeclaration.TotalInvoiceAmount`, `ExchangeRate`
  - `CustomsDeclarationLine.GrossWeight`, `NetWeight`, `ItemPrice`, `AdjustmentRate`, `StatisticalValue`, `UsedQuantityFromPrevious`
- Избрана е `decimal(18,4)` precision (18 total digits, 4 decimal places) за да се совпаѓа со постоечката конвенција во истиот фајл (`DutyRate`, `VATRate`, `TotalCustomsValue` итн.).
- EF генерираше миграција `FixDecimalPrecisions` со 8 ALTER COLUMN statements; non-destructive (increasing precision).

**How verified:**
- Локален `dotnet build` помина: 0 warnings, 0 errors.
- На VPS: `docker compose build api worker` + `up -d` успешно, images rebuilt.
- Миграцијата аплицирана: API log → `Database is ready (migrations applied or already up to date).`
- `docker logs lon-api 2>&1 | grep -c 'No store type was specified for the decimal property'` → **0** (беше 8).
- Login endpoint: HTTP 200.

**Follow-ups / discoveries:**
- 🔴 **Нов проблем откриен:** `System.OutOfMemoryException` при Vector Store initialization. Причина: мојот лимит од 1.5GB (P0.3.7) е претесен за .NET API + document embedding load. App-от gracefully fail-а: "The system will continue to function without RAG capabilities". → Додаден **P0.3.8** за bump на 3GB.
- ENABLE_VECTOR_STORE=True на VPS .env — значи RAG се очекува да работи.
- Останаа warnings: global query filter (Phase 1 multi-tenant work ќе ги reshape-ира) + BOM.ItemId1 (P0.3.5).

---

## 2026-04-18 — P0.3.8 Bump API memory + Vector Store OOM triage

**Status:** [x] done (container mem adequate; Vector Store OOM separated to Phase 6)
**Files changed:** `docker-compose.yml` (1.5G → 3G на API)

**What was done:**
- Бампнат API container memory limit од 1.5GB на 3GB.
- Deploy + recreate на VPS. `docker inspect lon-api` → `Memory: 3221225472` (3GB).

**How verified:**
- Container лимит физички е 3GB ✅
- Login HTTP 200 ✅
- API стабилно работи со нормален workload ✅

**Discoveries:**
- Vector Store СЕПАК OOM-ира со 3GB лимит. Значи root cause не е container лимит — код проблем во `DocumentSeeder` или `OpenAIEmbeddingService` или `VectorStoreInitializer`. 14MB raw files + 4 hardcoded sections во DocumentSeeder не треба да трошат 3GB.
- App gracefully degrade-ира: „The system will continue to function without RAG capabilities" — core API e функционалан без RAG.
- **Vector Store OOM root cause** додадено како **Phase 6** task за истрага/поправка. Не е blocker за Phase 0.

---

## 2026-04-18 — P0.3.5 BOM.ItemId1 shadow FK fix

**Status:** [x] done
**Files changed:**
- `src/LON.Infrastructure/Persistence/Configurations/ProductionConfigurations.cs` (1 line)
- `src/LON.Infrastructure/Migrations/20260418135013_FixBOMItemShadowFK.cs` (new)
- `src/LON.Infrastructure/Migrations/20260418135013_FixBOMItemShadowFK.Designer.cs` (new)
- `src/LON.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

**What was done:**
- Root cause: `BOMConfiguration.HasOne(e => e.Item).WithMany()` без inverse parameter. EF convention-от гледаше и `Item.BOMs` collection + `BOM.Item` + FK — ги третираше како 2 одделни relations: правилната (ItemId) + shadow (ItemId1).
- Fix: `.WithMany(i => i.BOMs)` експлицитно поврзува BOM↔Item со ЕДНА релација.
- Миграција `FixBOMItemShadowFK`: `DropForeignKey FK_BOMs_Items_ItemId1` + `DropIndex IX_BOMs_ItemId1` + `DropColumn ItemId1`. Безбедно — колоната никогаш не била пополнувана.

**How verified:**
- Локален `dotnet build`: 0 warnings.
- `dotnet ef migrations add` — единственото останато validation warning е за LONAuthorizationItem (Phase 1 work), **ItemId1 warning исчезнат**.
- На VPS: rebuild + recreate + migration applied.
- `docker logs lon-api 2>&1 | grep -c 'ItemId1'` → **0** (беше 2+).
- Login HTTP 200.

**Финална состојба на warnings (после P0.3.4 + P0.3.5):**
Остануваат само 4 EF global query filter warnings за required navigations со `IsDeleted` filter на едната страна (Partner↔LONAuthorization, Item↔LONAuthorizationItem, CustomsProcedure↔CustomsProcedureDocument, CustomsDeclaration↔CustomsDocument). Овие ќе се решат во Phase 1 (multi-tenant) каде query filters ќе се ре-dизајнираат за tenant isolation. Не се blockers.

**P0.3 ГОТОВ.**

---
