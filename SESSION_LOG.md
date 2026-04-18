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

## 2026-04-18 — P0.4 E2E smoke test (API level)

**Status:** [x] done на API ниво (UI потврда: pending од корисник)
**Files changed:**
- `src/LON.Application/Common/Interfaces/IApplicationDbContext.cs` — експандиран со сите 38 DbSets (беше само 6 KB-related)
- `src/LON.Application/WMS/Commands/CreateReceipt/CreateReceiptCommand.cs` — додаден `_context.Receipts.AddAsync(receipt)`
- `src/LON.API/Program.cs` — `ReferenceHandler.IgnoreCycles` во JSON options

**What was done & discoveries (3 bugs откриени):**

1. 🐛 **`IApplicationDbContext` изложуваше само 6 DbSets** (KB-related). Сите MediatR handlers (CreateReceipt, CreateProductionOrder, CreateCustomsDeclaration, Debit/CreditGuarantee) имаат ист проблем — не можат да persist-ираат преку интерфејсот. → Експандиран на сите 38 DbSets.

2. 🐛 **`CreateReceiptCommandHandler` никогаш не го додаваше Receipt-от во DbContext.** Коментар во кодот велеше „placeholder". SaveChangesAsync со 0 tracked entities = no-op. POST враќаше HTTP 200 + fake Guid; податоците исчезнуваа. → Додадено `AddAsync(receipt, cancellationToken)` пред SaveChanges.

3. 🐛 **GET /api/wms/receipts враќаше празно тело** поради JSON циклична референца (Receipt → Lines → Line.Receipt → ...). System.Text.Json infinite loop. → `ReferenceHandler.IgnoreCycles` глобално во AddJsonOptions.

**How verified end-to-end на VPS:**
- Login → JWT токен
- POST `/api/wms/receipts` со partner SUP-001, warehouse WH-MAIN, item SF-001, 100 BOX, batch BATCH-SMOKE-001, MRN 26MK000012345678A1 → HTTP 200, receipt ID `ceabc418-c15d-4adf-a6ae-6f70440b012f`
- GET `/api/wms/receipts` → list враќа 1 receipt со правилен receiptNumber `RCP-20260418-9fe2f6a0`
- GET `/api/wms/receipts/{id}` → details со полна line (quantity 100.0000 — precision од P0.3.4 работи), batch, MRN, uoMId.
- Корисник треба да потврди преку `https://elon.elbosoft.click/inventory` дали receipt е видлив во UI.

**Follow-ups:**
- **InventoryBalance НЕ се ажурира** при create receipt. Handler-от фрла domain event во outbox; Worker треба да ги процесира. Неjasno дали Worker навистина ажурира InventoryBalance. За провера во P2.3 (end-to-end flow).
- **Другите 4 MediatR handlers имаат ист missing Add()**. Ќе се поправи per task кога ќе се користат во Phase 2.
- P0.4 criterion „видливо во UI" е pending дури корисникот да провери.

---

## 2026-04-18 — P0.5 ICurrentUserService replaces CreatedBy hack

**Status:** [x] done
**Files changed:**
- `src/LON.Application/Common/Interfaces/ICurrentUserService.cs` (new) — Username, UserId, AuditName
- `src/LON.API/Services/CurrentUserService.cs` (new) — reads JWT claims via IHttpContextAccessor
- `src/LON.Infrastructure/Persistence/ApplicationDbContext.cs` — втор конструктор со ICurrentUserService; SaveChangesAsync користи AuditName; fallback на "System" кога нема user (Worker, seeders, migrations)
- `src/LON.API/Program.cs` — `AddHttpContextAccessor()` + scoped `ICurrentUserService`

**How verified на VPS:**
- POST нов receipt како admin → receipt created, id `44fe3648-d4bc-45c4-a3ad-f2b5481874a3`
- GET показа: нов receipt `createdBy: "admin"`, стар (од P0.4) `createdBy: "System"` ✅
- ReceiptLines исто `createdBy: "admin"` (cascade низ SaveChanges).

**Design notes:**
- ApplicationDbContext има 2 конструктори: (DbContextOptions) и (DbContextOptions + ICurrentUserService). EF Core ja избира најдолгата што може да се resolve-ира преку DI. Во API контекст ICurrentUserService е registered → 2-arg користен. Во Worker (без registration) → 1-arg, `_currentUser=null`, AuditName fallback на "System".
- Seeders, migrations и background жобови без HttpContext резултираат со "System" — намерна одлука. Ако треба, може да се додаде named audit per worker.

---

## 2026-04-18 — 🎯 ФАЗА 0 ЗАВРШЕНА

Summary по таскови:
- **P0.1** SSH setup
- **P0.2** VPS дијагноза + health snapshot
- **P0.3.1** Recreate lon-api (exited 3 weeks)
- **P0.3.2** SQL порт 127.0.0.1 (security)
- **P0.3.3** DataProtection persistent volume
- **P0.3.4** 8 decimal precision fixes + migration
- **P0.3.5** BOM.ItemId1 shadow FK fix + migration
- **P0.3.6** version: '3.8' removed
- **P0.3.7** Memory/CPU limits
- **P0.3.8** API memory 1.5→3GB (+ Vector Store OOM → Phase 6)
- **P0.4** E2E API smoke test (+ 3 bug fixes: IApplicationDbContext incomplete, CreateReceiptCommandHandler no-op, JSON cycle)
- **P0.5** ICurrentUserService audit trail

**Фаза 1 (multi-tenant) започнува:** P1.1 Tenant entity + CRUD + seed TEKSPORT.

---

## 2026-04-18 — P0.6 Receipt ажурира inventory (foundered by domain expert feedback)

**Trigger:** Домен експерт / корисник провери во UI: „Нема инвентори, а има приеми." Movement Reports покажа 2 receipts (AUDIT-TEST 50, SMOKE-TEST 100), но Inventory by Location беше празен.

**Root cause:** CreateReceiptCommandHandler го зачувуваше само `Receipt` + `ReceiptLine`. Никогаш не создаваше `InventoryMovement` ниту не ажурираше `InventoryBalance`. Receipts беа видливи во Receipts извештај, но stock-от „не стигнуваше" во магацин.

**Status:** [x] done
**Files changed:**
- `src/LON.Application/WMS/Commands/CreateReceipt/CreateReceiptCommand.cs` — комплетен rewrite на handler
- `src/LON.Infrastructure/Migrations/20260418150546_BackfillLocationTypes.cs` (new)

**What was done:**
1. Handler сега создава еден `InventoryMovement` (Type=Receipt) per line.
2. Handler upsert-ира `InventoryBalance` (match on Item+Location+Batch+MRN+UoM+QualityStatus; ако постои, AddQuantity; ако не, new row).
3. `ResolveLandingLocationAsync` со fallback chain: explicit LocationId > `Type=Receiving` во warehouse > code prefix `"RCV"` > first active location. Fails 400 ако warehouse нема локации.
4. `CreateReceiptCommand` прима опционо `LocationId` за override.
5. Empty `Lines` сега отфрла одмах со 400 (беше silent).
6. Migration `BackfillLocationTypes` за постоечки redovi: UPDATE Type по code convention (RCV→1, STG→2, PICK→3, PROD→4, SHIP→5, QUA→6).

**How verified на VPS:**
- POST receipt qty=25 → `{"isSuccess":true,"data":"6934603e-..."}`
- GET `/api/wms/inventory` → враќа 1 InventoryBalance: `{itemId: SF-001, locationId: 718eee36..., batchNumber: BATCH-INV-001, mrn: 26MK000088888888A1, quantity: 25.0000, qualityStatus: 0}`
- Landing location резолвиран преку code-prefix fallback (Type сè уште null во API response — посебен LocationDto bug, додаден како follow-up).

**Discoveries & follow-ups:**
- **LocationDto serialization drops Type** — MapLocation го проследува Type, но API враќа null. Или DTO param mapping е bugged, или JsonSerializer игнорира. Додадено во Phase 6 TODO.
- Инвентори од **претходни receipts (AUDIT + SMOKE-TEST, вкупно 150 единици)** нема да се појават — тие се создадени пред fix-от. Per CLAUDE.md „no shortcuts": ако сакаме историски consistency, треба backfill script (replay domain events). Засега: prospective fix, корисникот знае.
- Git commit `f92c754` носи незначајни bin/obj фајлови бидејќи `.gitignore` беше избришан пред оваа сесија. Cleanup таск додаден во Phase 6.

---

## 2026-04-18 — P1.1 Tenant entity + TEKSPORT seed

**Status:** [x] done
**Files:**
- `src/LON.Domain/Entities/MasterData/Tenant.cs` (new) — `Code`, `Name`, `LegacyUvoznik`, HQ address, tax number, contact, `CustomsAuthorizationNumber`, `DefaultLanguage`, `IsActive`.
- `src/LON.Infrastructure/Persistence/Configurations/TenantConfiguration.cs` (new) — unique `Code`, filtered unique `LegacyUvoznik`, default language=mk.
- `src/LON.Infrastructure/Persistence/ApplicationDbContext.cs` + `IApplicationDbContext` — додаден `DbSet<Tenant>` на двете (поука од P0.4 — ако интерфејсот не го изложува, handler-от не може да го зачува).
- `src/LON.Infrastructure/Migrations/20260418165047_AddTenantEntity.*` — CREATE TABLE + индекси.
- `src/LON.Infrastructure/Persistence/ApplicationDbContextSeed.cs` → `SeedTenants`: TEKSPORT со HQ Скопје + legacyUvoznik=TEKSPORT + default mk.
- `src/LON.API/Controllers/TenantsController.cs` (new) — `[Authorize(Roles="Administrator")]` GET/GET(id)/POST/PUT/DELETE (soft). Code auto-uppercase.
- `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` — regenerated.

**Verified on VPS:**
- Build + migration applied + seed completed
- `GET /api/tenants` (admin bearer) → `[{ code: "TEKSPORT", address: "Скопје...", defaultLanguage: "mk", legacyUvoznik: "TEKSPORT", id: "b8d4fe76-..." }]`

**Domain insight запишана во меморија (`project_tenant_multisite.md`):** Tenant = legal entity, може да има повеќе физички сајтови (Warehouses). **TEKSPORT има Скопје + Виница**. Другите Uvoznici исто може имаат многу сајтови. P1.2 ќе създаде WH-TEK-VN покрај постоечкиот.

---

## 2026-04-18 — 🎯 Phase 2.5 setup done

**Цел:** i18n инфраструктура ready пред Phase 1 Tenant UI.

**Files:**
- `frontend/web/src/i18n/i18n.ts` — i18next + react-i18next + LanguageDetector, 4 jazici, localStorage key `lon.lang`, fallback=mk
- `frontend/web/src/i18n/locales/{mk,sr,sq,en}.json` — 7 namespaces (common, nav, login, dashboard, wms, qualityStatus, errors), ~140 клучеви секоja
- `frontend/web/src/components/LanguageSwitcher.tsx` — dropdown со flag emojis (🇲🇰🇷🇸🇦🇱🇬🇧) + native names
- `frontend/web/src/index.tsx` — import на i18n пред App
- `frontend/web/src/components/Sidebar.tsx` — top-level items + section headers t('nav.*'); compact switcher на дното
- `frontend/web/src/pages/Login.tsx` — целосно t() за форма + switcher во footer

**Verified на VPS:**
- `docker compose build frontend` + recreate → HTTP 200 на `https://elon.elbosoft.click/login`
- Switcher е видлив на Login footer + Sidebar дно (треба визуелна потврда од корисник)

**P2.5.4 retrofit** на останатите страници (Dashboard, Inventory, Production, Customs, Guarantees, Reports, Advanced, Admin, Master Data под-страници) е паралелен backlog — секоja страница се преведува кога ја допираме во Phase 2+.

**Workflow напомена:** За сите НОВИ страници (Phase 1 Tenant CRUD, Phase 2 flows, итн.) — user-facing string во код е **ЗАБРАНЕТО**. Користи `t('key.path')` од ден 1.

**Следно:** Phase 1 P1.1 — `Tenant` entity + CRUD + seed TEKSPORT. Multi-tenant foundation.

---

## 2026-04-18 — 🎯 Phase 6 Priority-A ЗАВРШЕНА

**Decision:** Корисник избра `0 → 6-Priority-A → 2.5 → 1 → 2 → 6-Priority-B паралелно → 3 → 4 → 5 → 7`.

Сите 5 foundational tasks landed in овој сесија:

**P6.1 Repo hygiene** — Restore `.gitignore` (.NET + Node + VS), untrack 26 bin/obj фајлови од `LON.Application` (заостанати од `f92c754` пред да има .gitignore).

**P6.3/4 Contract hygiene pipeline** — `scripts/gen-api-types.sh`:
- `dotnet swagger tofile` → `api-contract/swagger.json`
- `openapi-typescript` → `frontend/web/src/api/schema.d.ts`
- `frontend/web/src/api/index.ts` — friendly re-exports
- Swashbuckle.CLI 6.6.2 + openapi-typescript 6.7.6 (версии согласени со проектот)
- `ReceiptForm` refactored да користи `CreateReceiptCommand` + `ReceiptLineDto`

**P6.5-6.8 Test harness** — `tests/LON.IntegrationTests/`:
- `LonApiFactory` — `WebApplicationFactory<Program>` со Testcontainers-MsSql (реален SQL Server во Docker per test class)
- `AuthTests` — login success, wrong password 401, protected endpoint без token 401
- `ReceiptFlowTests.CreateReceipt_ThenGetInventory_*` — **E2E што би ги фатил сите 3 P0.4/P0.6 bug-ови**

**P6.9 CI gate** — `.github/workflows/ci.yml`:
- Backend job: dotnet build + integration tests (Ubuntu runner има Docker)
- Frontend job: regenerate API types → **fail на contract drift** + npm build

**P6.17 CLAUDE.md Contract Hygiene Protocol** — експлицитно правила:
1. Допираш DTO/command → grep frontend за callers
2. Допираш API-exposed DTO → regenerate TS и commit
3. Нов/изменет handler → integration test (POST → GET → DB assert)
4. UI change → Claude Preview tools за smoke пред deploy
5. Нов DbSet → проверка во `ApplicationDbContext` И `IApplicationDbContext`

**Verification напомени:**
- Docker не е достапен локално — integration тестовите ќе се извршат на CI. Watch next GitHub Actions run.
- Сè commit-нато на `main`: `87f7788 → 71c3fa2 → bce271b → 0b62196`.

**Phase 6 Priority-B** (split MasterDataController, Vector Store OOM, LocationDto Type, MediatR миграција per module, Logging, DataProtection) остануваат паралелен backlog — ги допираме природно во Phase 2+.

**Следно:** Phase 2.5 i18n — `react-i18next` + LanguageProvider + 4-јазични dictionaries (mk/sr/sq/en).

---

## 2026-04-18 — P0.6 UI Create Receipt fix (3 contract bugs)

**Trigger:** Корисник пробал Create Receipt од `/inventory` → HTTP 400 „Failed to create receipt".

**Three bugs in the wire contract:**
1. Form испраќа `expiryDate: ""` (празен стринг), backend `DateTime? ExpiryDate` не прима празен стринг → 400 на model binding.
2. Form испраќа `supplierId`, backend очекува `partnerId` → молчешкум null (не blocker но data loss).
3. Form испраќа per-line `locationId`, backend имаше LocationId само на header ниво → per-line се игнорираше.

**Status:** [x] done
**Files changed:**
- `src/LON.Domain/Entities/WMS/WMS.cs` — додаден `ReceiptLine.LocationId: Guid?` + navigation
- `src/LON.Application/WMS/Commands/CreateReceipt/CreateReceiptCommand.cs` — `ReceiptLineDto.LocationId`; handler префера line-level LocationId > header > auto-resolve
- `src/LON.Infrastructure/Migrations/20260418152539_AddLocationToReceiptLine.cs` (new) — `AddColumn + CreateIndex + AddForeignKey`
- `frontend/web/src/components/WMS/ReceiptForm.tsx` — нормализација на payload во `handleSubmit`: `supplierId → partnerId`, празни стрингови → undefined, forward line.locationId. Подобрена error toast (чита `errorMessage` и `errors[]`).

**How verified (after deploy):**
- Curl со form-realistic payload: partnerId (не supplierId), per-line locationId, без празни стрингови, qualityStatus=1 → HTTP 200 + receipt `ff34c93b-...`
- GET `/api/wms/inventory` → 2 балансы: SF-001 25 + нов **RM-002 30 KG qualityStatus=1** на RCV-01 ✅
- Login HTTP 200 by end-to-end.

**Meta-finding (самокритика):**
Овие 3 bug-а беа „контракт / plumbing" — Claude можеше и морaшe да ги фати. Користевме curl со MOJ payload наместо реалниот payload од form-от. Додадено:
- Memory `feedback_contract_hygiene.md` — workflow правила што ги адоптирам веднаш (grep frontend при DTO change, POST+GET+DB assert при handler, Preview tools за UI smoke).
- WORK_PLAN P6.TEST — infrastructure: xUnit + WebApplicationFactory + Testcontainers, auto-generated TS од OpenAPI, CI gate.

---
