# VERIFICATION — Phase 16 Per-Task Checklists

> Every Phase 16 prompt in `AGENT-PROMPTS.md` ends with: *"walk through the matching section in `VERIFICATION.md`."* This file is that walkthrough.
>
> Each section is a copy-paste-runnable checklist. Don't paraphrase, run it.
>
> **Universal pre-flight** (every task):
> ```bash
> # In repo root
> git status         # working tree clean before you start? if not, stash or commit
> git pull --rebase  # up to date with main
> ```
>
> **Universal post-task** (every task):
> ```bash
> git log --oneline -3                              # confirm your commit is on top
> git push origin HEAD                              # push to origin
> ssh root@173.212.254.216 "cd /opt/apps/LON/LON-test && git pull && docker compose up -d --build"
> # wait ~60s for containers to come up
> curl -sf https://elon.elbosoft.click/health || echo "VPS health check FAILED"
> ```
>
> `SESSION_LOG.md` entry format (append to bottom):
> ```
> ## 2026-MM-DD — P16.X<N> — <one-line title>
> Plan: <2–3 sentences>
> Files touched: <list>
> Verification:
>   - tsc: <pass/fail>
>   - eslint: <pass/fail>
>   - integration tests: <pass count / fail count>
>   - VPS smoke: <screenshot path or curl output>
> Outcome: [x] done | [/] in-progress | [!] blocked
> Notes: <anything surprising>
> ```

---

## Phase 16.A — Cleanup

### A1 — Remove dead `WarehousesList`

**Before deleting:**
```bash
# 1. Confirm no other reference to the legacy component
grep -rn "WarehousesList" frontend/web/src --include="*.tsx" --include="*.ts" | grep -v App.tsx
# Expected output: empty (only App.tsx should reference it)

grep -rn "warehouses-old" frontend/web/src
# Expected: only the Route in App.tsx
```
**If output is not empty → STOP, investigate, do NOT delete.**

**After deleting:**
```bash
cd frontend/web
node_modules/.bin/tsc --noEmit                       # → 0 errors
node_modules/.bin/eslint src --ext .ts,.tsx          # → 0 errors, ≤1 warning
node_modules/.bin/jest --testPathPattern=filterNav   # → all pass
```

**VPS smoke (must do all four):**
1. `https://elon.elbosoft.click/master-data/warehouses` → list of warehouses renders ✓
2. `https://elon.elbosoft.click/master-data/warehouses-old` → either redirects to current or shows route-not-found ✓ (not a stack trace)
3. Sidebar → MasterData → Warehouses → clicks to /master-data/warehouses ✓
4. Browser devtools Console: no errors ✓

**SESSION_LOG:**
- File deleted (path + line count)
- Routes removed (line numbers in App.tsx)
- grep output proving no orphan references

---

### A2 — Honest `navGroups.ts`

**After edits:**
```bash
# Confirm all 6 items flipped
grep -B1 -A3 "lon.escalations\|lon.risks\|cost-accounting\|payroll-export\|/finance/ap\|/hr/training" frontend/web/src/nav/navGroups.ts | grep "backendStatus"
# Expected: 6 lines reading `backendStatus: 'partial',`
```

```bash
# Confirm locales added (4 of them)
for lang in en mk sq sr; do
  grep -q "localStorageWarning" frontend/web/src/i18n/locales/${lang}.json && echo "$lang OK" || echo "$lang MISSING"
done
# Expected: 4× OK
```

```bash
cd frontend/web
node_modules/.bin/tsc --noEmit                       # → 0 errors
node_modules/.bin/eslint src --ext .ts,.tsx          # → 0 errors
node_modules/.bin/jest --testPathPattern=filterNav   # → all pass
```

**VPS smoke:**
1. Visit `/management/risks` → MUI <Alert severity="warning"> banner is visible at top of page ✓
2. Visit `/management/escalations` → same banner ✓
3. Visit `/finance/cost-accounting` → same banner ✓
4. Visit `/finance/payroll` → same banner ✓
5. Visit `/finance/ap` → same banner ✓
6. Visit `/hr/training` → same banner ✓
7. Sidebar (if expanded) shows nothing visually different — the `partial` status doesn't change the icon/label unless we expose it; that's fine.

**SESSION_LOG:**
- 6 backendStatus changes (one line per)
- 4 locale keys added
- Banner screenshot from VPS

---

### A3 — MasterData duplication audit

**Outputs (no code changes, just doc):**
```bash
# This script seeds the audit table
cat <<'EOF' > /tmp/audit.sh
#!/usr/bin/env bash
cd "$(git rev-parse --show-toplevel)"
for f in $(find frontend/web/src/pages/MasterData -name "*.tsx"); do
  routed=$(grep -c "$(basename $f .tsx)" frontend/web/src/App.tsx)
  lines=$(wc -l < "$f")
  last=$(git log -1 --format="%ad" --date=short -- "$f")
  echo "$f | $routed | $lines | $last"
done
EOF
chmod +x /tmp/audit.sh && /tmp/audit.sh
```

Take the output and fill `docs/PHASE16_AUDIT.md` with columns:
`Component | Path | RoutedRefs | Lines | LastCommit | Verdict (KEEP/DELETE/UNCLEAR) | Reason`

**Verification:**
```bash
test -f docs/PHASE16_AUDIT.md && wc -l docs/PHASE16_AUDIT.md
# Expected: file exists, ≥15 lines (header + rows)
```

No tsc/eslint changes expected (no code edits). No VPS deploy.

**SESSION_LOG:**
- Audit table summary: N KEEP, M DELETE, K UNCLEAR
- List of follow-up tasks filed in WORK_PLAN.md (e.g. `P16.A3.1: delete X`)

---

## Phase 16.B — UI Foundations

### B1 — react-query + Inventory.tsx pilot

**Install verification:**
```bash
cd frontend/web
grep -E '"@tanstack/react-query"' package.json
# Expected: one match with a v4.x or v5.x version
```

**App-level wrap:**
```bash
grep -n "QueryClientProvider" frontend/web/src/App.tsx
# Expected: one match
grep -n "ReactQueryDevtools" frontend/web/src/App.tsx
# Expected: one match (gated on NODE_ENV === 'development')
```

**Hook file:**
```bash
test -f frontend/web/src/hooks/queries/useInventory.ts || echo "MISSING"
grep -cE "^export (function|const) use[A-Z]" frontend/web/src/hooks/queries/useInventory.ts
# Expected: ≥7 exports (1 query + 6+ mutations)
```

**Inventory page:**
```bash
# No raw axios/api.* calls left in Inventory.tsx itself — must go through hooks
grep -E "wmsApi\.|masterDataApi\.|axios" frontend/web/src/pages/Inventory.tsx
# Expected: zero matches OR only via imports of the new hooks
```

```bash
cd frontend/web
node_modules/.bin/tsc --noEmit          # → 0 errors
node_modules/.bin/eslint src            # → 0 errors
```

**VPS smoke (all six):**
1. `https://elon.elbosoft.click/warehouse/receipts` loads list ✓
2. Open DevTools Network tab → reload → exactly one `GET /WMS/inventory` request ✓
3. Change a filter → new request fires, list updates ✓
4. Click "Прими" / "Receipt" → form opens → submit → list refreshes with new row, no manual reload ✓
5. Open another browser tab to the same URL, mutate inventory in tab 1, switch back to tab 2 — list refetches on focus ✓
6. After 30s of idle, switch tabs — query is `stale` and refetches ✓

**SESSION_LOG:**
- Package.json diff
- Hook file size
- Inventory.tsx before/after line count
- Network screenshot showing single request + cache hits

---

### B2 — DataTable hardening

**Gap analysis:**
```bash
test -f docs/PHASE16_DATATABLE_GAPS.md
wc -l docs/PHASE16_DATATABLE_GAPS.md
```

**Implementation:**
```bash
# Test file exists and runs
test -f frontend/web/src/components/common/DataTable.test.tsx
cd frontend/web && node_modules/.bin/jest --testPathPattern=DataTable
# Expected: all tests pass; at minimum 5 tests
```

**Production.tsx migration:**
```bash
# The hand-rolled <table> should be replaced
grep -c "<table" frontend/web/src/pages/Production.tsx
# Expected: 0 (or only inside a child component the page composes, not in the page itself)
grep -c "DataTable" frontend/web/src/pages/Production.tsx
# Expected: ≥1
```

```bash
cd frontend/web
node_modules/.bin/tsc --noEmit && node_modules/.bin/eslint src
```

**VPS smoke:**
1. `https://elon.elbosoft.click/production/orders` → orders grid renders, looks identical to before ✓
2. Click a column header → sorts ✓
3. Pagination (if >25 orders): clicks ✓
4. Empty state: filter to nothing → "No data" not blank table ✓

**SESSION_LOG:**
- Test count
- DataTable feature checklist (sort/paginate/select/etc.)
- Production.tsx screenshot

---

### B3 — Layout shell + theme

**Files:**
```bash
test -f frontend/web/src/components/layout/PageShell.tsx
test -f frontend/web/src/theme.ts
grep -c "ThemeProvider" frontend/web/src/App.tsx     # ≥1
grep -c "PageShell" frontend/web/src/pages/Dashboard.tsx     # ≥1
grep -c "PageShell" frontend/web/src/pages/Inventory.tsx     # ≥1
grep -c "PageShell" frontend/web/src/pages/Production.tsx    # ≥1
```

```bash
cd frontend/web
node_modules/.bin/tsc --noEmit && node_modules/.bin/eslint src
```

**VPS smoke — desktop (≥1280px):**
1. /dashboard, /warehouse/receipts, /production/orders → page header same height + color across all three ✓
2. Action button row right-aligned in same spot ✓
3. Content padding consistent ✓

**VPS smoke — mobile (375px DevTools):**
1. Same three pages → no horizontal scroll ✓
2. Sidebar collapses to hamburger ✓
3. Action button row stacks or wraps cleanly ✓

**SESSION_LOG:**
- 2 screenshots per page (desktop + mobile), or one composite

---

## Phase 16.C — localStorage → backend

### C1 — RiskRegisterItem

**Backend present:**
```bash
test -f src/LON.Domain/Entities/Management/RiskRegisterItem.cs
grep -q "DbSet<RiskRegisterItem>" src/LON.Infrastructure/Persistence/ApplicationDbContext.cs
grep -q "DbSet<RiskRegisterItem>" src/LON.Application/Common/Interfaces/IApplicationDbContext.cs
ls src/LON.Infrastructure/Migrations/*P16_C1*.cs   # migration file present
```

**Handlers (5):**
```bash
ls src/LON.Application/Management/Risks/
# Expected: CreateRiskRegisterItemCommand.cs, UpdateRiskRegisterItemCommand.cs,
#           DeleteRiskRegisterItemCommand.cs, GetRiskRegisterItemsQuery.cs,
#           GetRiskRegisterItemByIdQuery.cs (or aggregated)
```

**Endpoints (5):**
```bash
grep -nE 'Http(Post|Put|Delete|Get)\("risks' src/LON.API/Controllers/ManagementController.cs
# Expected: ≥5 lines
```

**Tests:**
```bash
test -f tests/LON.IntegrationTests/RiskRegisterTests.cs
# Locally run if dotnet available:
dotnet test tests/LON.IntegrationTests/ --filter "FullyQualifiedName~RiskRegister" 2>&1 | tail -10
# Expected: all green; ≥5 tests
```

**OpenAPI types:**
```bash
./scripts/gen-api-types.sh
git diff --stat frontend/web/src/api/schema.d.ts
# Expected: change present, committed
```

**Frontend:**
```bash
# localStorage gone
grep -E "localStorage\.(set|get)Item" frontend/web/src/pages/Management/OpenRisks.tsx
grep -E "localStorage\.(set|get)Item" frontend/web/src/pages/Management/Escalations.tsx
# Expected: ZERO matches (UI prefs OK, but business data must be gone)

# react-query hooks present
grep -c "useQuery\|useMutation" frontend/web/src/pages/Management/OpenRisks.tsx        # ≥2
grep -c "useQuery\|useMutation" frontend/web/src/pages/Management/Escalations.tsx      # ≥2

# A2-installed warning banner gone
grep -c "common.localStorageWarning" frontend/web/src/pages/Management/OpenRisks.tsx        # 0
grep -c "common.localStorageWarning" frontend/web/src/pages/Management/Escalations.tsx      # 0

# navGroups flipped back
grep -B1 -A3 "management-risks\|management-escalations" frontend/web/src/nav/navGroups.ts | grep "backendStatus"
# Expected: both say 'exists' again
```

**Compile + test:**
```bash
cd frontend/web
node_modules/.bin/tsc --noEmit && node_modules/.bin/eslint src
cd .. && dotnet build src/LON.API/LON.API.csproj 2>&1 | tail -5
# Expected: 0 warnings 0 errors
```

**VPS smoke:**
1. Apply EF migration on VPS (deploy script handles this).
2. `/management/risks` — create a risk → reload page → still there ✓ (proves DB persistence)
3. Edit → reload → edit persisted ✓
4. Delete → gone ✓
5. SSH to VPS, `SELECT TOP 5 * FROM RiskRegisterItems` against the SQL Server container — your record visible ✓
6. Same for `/management/escalations` ✓
7. Visit the page as a different-tenant user → none of the data visible ✓

**Migration doc:**
```bash
test -f docs/PHASE16_C1_LOCAL_TO_BE_MIGRATION.md
```

**SESSION_LOG:**
- Migration file name
- Test count (added)
- DB-level row count screenshot from SSMS or `docker exec ... sqlcmd`
- Tenant isolation evidence

---

### C2 — EmployeeCertification

Mirrors C1 structure. Substitute:
- Entity name → `EmployeeCertification`
- Endpoints → `/api/Hr/certifications` (+ `/expiring`)
- Page → `pages/Hr/Training.tsx`
- Tests → `EmployeeCertificationTests.cs`
- Migration prefix → `*P16_C2*`
- Migration doc → `docs/PHASE16_C2_TRAINING_MIGRATION.md`

Add one extra check:
```bash
# Expiring-soon traffic light works
curl -fsSL -H "Authorization: Bearer <tek-hr token>" \
  "https://elon.elbosoft.click/api/Hr/certifications/expiring?withinDays=30" | jq length
# Expected: integer, ≥0
```

---

### C3 — Finance localStorage-three

Each of C3.a/b/c follows C1/C2 structure. Per sub-task substitutions:

| Sub | Entity | Endpoints | Page | Tests |
|---|---|---|---|---|
| C3.a | `CostRate` | `/api/Finance/cost-rates` | `CostAccounting.tsx` | `CostRateTests.cs` |
| C3.b | `PayrollPeriod` + `PayrollLine` | `/api/Finance/payroll-periods` (+ `/finalize`, `/export`) | `PayrollAggregate.tsx` | `PayrollPeriodTests.cs` |
| C3.c | `SupplierInvoice` | `/api/Finance/supplier-invoices` | `SupplierInvoices.tsx` | `SupplierInvoiceTests.cs` |

**Extra check for C3.b — must source hours from existing tables:**
```bash
# PayrollLine creation handler must NOT take RegularHours as input —
# it computes them from AttendanceRecord + Absence
grep -A 30 "class CreatePayrollPeriodCommandHandler" src/LON.Application/Finance/*.cs | grep -E "Attendance|Absence"
# Expected: ≥2 matches (proves it queries the source tables)
```

**Extra check for C3.c — overdue computation:**
```bash
# Status='Overdue' must be derived, not stored
curl -fsSL "https://elon.elbosoft.click/api/Finance/supplier-invoices?status=Overdue" -H "Authorization: Bearer ..."
# Then manipulate a record's DueDate < today in DB and refetch — should return as Overdue
```

---

## Phase 16.D — Test coverage gap fill

### D1 — WMSController tests

```bash
test -f tests/LON.IntegrationTests/WMSControllerTests.cs
dotnet test tests/LON.IntegrationTests/ --filter "FullyQualifiedName~WMSController" 2>&1 | tail -10
# Expected: all green; ≥10 tests
```

```bash
# Confirm no regression in existing tests
dotnet test tests/LON.IntegrationTests/ 2>&1 | grep -E "Total tests:|Passed:|Failed:"
# Expected: Failed: 0; Total ≥ 164 (154 baseline + 10 new)
```

**No VPS deploy required** (test-only). SESSION_LOG: total test count before/after, coverage rationale per endpoint.

---

### D2 — Role × permission matrix

```bash
test -f tests/LON.IntegrationTests/RolePermissionTests.cs
dotnet test tests/LON.IntegrationTests/ --filter "FullyQualifiedName~RolePermission" 2>&1 | tail -10
# Expected: green; one [Theory] producing ~90 cases (9 roles × ~10 endpoints)
```

**Cross-check vs nav matrix:**
```bash
# The test data must match nav/filterNavGroups.test.ts expectations
grep -E "Customs Officer.*\['customs', 'finished-goods'\]" frontend/web/src/nav/filterNavGroups.test.ts
# Expected: present. Test in D2 must mirror this — Customs Officer 200s on /api/Customs/* but 403s on /api/Production/*.
```

SESSION_LOG: matrix table (role × allowed module ranges).

---

### D3 — MasterData CRUD smoke

```bash
test -f tests/LON.IntegrationTests/MasterDataCrudTests.cs
dotnet test tests/LON.IntegrationTests/ --filter "FullyQualifiedName~MasterDataCrud" 2>&1 | tail -10
# Expected: green; one [Theory] producing ≥40 cases (8 resources × 5 ops)
```

```bash
# Total test count check
dotnet test tests/LON.IntegrationTests/ 2>&1 | grep -E "Total tests:"
# After D1+D2+D3: ≥ 200 tests
```

SESSION_LOG: per-resource pass/fail; any 500s discovered (fix in same task or file P16.D3.followup).

---

## Phase 17 — ClientOrder hub + flow wiring + AI helper

### §E0 — Sticky-defaults hook + bulk field-update endpoint pattern

> **Reframe (2026-05-12 PREP):** TEKSPORT ELON has 99.998% EUR lines — currency-specific bulk-change is a degenerate use case. Treat the hook + endpoint as **generic infrastructure** exercised primarily on UoM / CountryOfOrigin / TariffCode (where there IS variance), with currency a free side-effect.

```bash
# Files present
test -f frontend/web/src/hooks/useStickyDefaults.ts
test -f frontend/web/src/components/common/BulkFieldUpdateButton.tsx
test -f frontend/web/src/hooks/useStickyDefaults.test.tsx
test -f frontend/web/src/components/common/BulkFieldUpdateButton.test.tsx

# Unit tests pass
cd frontend/web
node_modules/.bin/jest --testPathPattern="useStickyDefaults|BulkFieldUpdate"
# Expected: ≥5 tests green

# Locale keys
for lang in en mk; do
  grep -q "bulkUpdate.title\|stickyDefaults.tooltip" frontend/web/src/i18n/locales/${lang}.json && echo "$lang OK" || echo "$lang MISSING"
done

# Compile + lint
node_modules/.bin/tsc --noEmit
node_modules/.bin/eslint src
```

**Server-side bulk update endpoint pattern:**
```bash
# Expect at least one handler implementing bulk-update with Reason
grep -rE "BulkUpdateLinesCommand|BulkFieldUpdateRequest" src/LON.Application --include="*.cs"
# Expected: ≥1 match (foundation; consumed by later tasks)
```

**No VPS smoke required (foundation only; visible when E3 consumes it).**

**SESSION_LOG:**
- Hook signature + scope keys defined
- Unit test count
- Locale keys added (count)

---

### §E1 — ClientOrder entity + endpoints

```bash
# Entity present
test -f src/LON.Domain/Entities/Customs/ClientOrder.cs
grep -q "ClientOrderStatus" src/LON.Domain/Enums/*.cs
grep -q "DbSet<ClientOrder>" src/LON.Infrastructure/Persistence/ApplicationDbContext.cs
grep -q "DbSet<ClientOrder>" src/LON.Application/Common/Interfaces/IApplicationDbContext.cs

# Migration applied locally
ls src/LON.Infrastructure/Migrations/*phase-17*ClientOrder*.cs
dotnet ef database update --project src/LON.Infrastructure --startup-project src/LON.API

# Handlers
ls src/LON.Application/Customs/ClientOrders/
# Expected: Create / Update / Cancel / GetById / GetList + Handler files

# Endpoints
grep -E '\[Http(Get|Post|Put|Delete)' src/LON.API/Controllers/ClientOrdersController.cs | wc -l
# Expected: ≥6

# Number formatter
grep -q "CO-{year:0000}-{seq:D6}\|CO-{0}-{1:D6}" src/LON.Domain/Common/NumberFormatter.cs

# Tests
test -f tests/LON.IntegrationTests/ClientOrderTests.cs
dotnet test tests/LON.IntegrationTests/ --filter "FullyQualifiedName~ClientOrder" 2>&1 | tail -10
# Expected: green; ≥6 tests including concurrency + tenant isolation
```

**SQL SEQUENCE check:**
```bash
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT name FROM sys.sequences WHERE name LIKE 'seq_ClientOrder%'
"
# Expected: 1 row (per-tenant); current_value increments after 1 CreateClientOrder POST
```

**OpenAPI types:**
```bash
./scripts/gen-api-types.sh
git diff --stat frontend/web/src/api/schema.d.ts
# Expected: change present, committed
```

**VPS smoke:**
1. Deploy. Verify `https://elon.elbosoft.click/api/ClientOrders` (with auth) returns `[]`.
2. POST one via Postman → 201, returns `OrderNumber: "CO-2026-000001"`.
3. GET it back → matches.

---

### §E2 — ClientOrder list + hub UI shell

```bash
test -f frontend/web/src/pages/Orders/OrderList.tsx
test -f frontend/web/src/pages/Orders/OrderHub.tsx
grep -q "/orders" frontend/web/src/App.tsx
grep -q "/orders/:id" frontend/web/src/App.tsx
grep -q "orders" frontend/web/src/nav/navGroups.ts

cd frontend/web
node_modules/.bin/tsc --noEmit
node_modules/.bin/eslint src
```

**Locales:**
```bash
for lang in en mk; do
  grep -q "orders.hub" frontend/web/src/i18n/locales/${lang}.json && echo "$lang OK" || echo "$lang MISSING orders keys"
done
```

**VPS smoke:**
1. Login as Manager → /orders renders.
2. Click „Нов налог" → dialog opens, can create.
3. Created → navigates to /orders/:id; hub renders with 3 widgets + action launcher (buttons disabled with tooltip).
4. Sidebar shows „📋 Налози" group; click navigates to /orders.

---

### §E3 — Wire IM declaration from hub

```bash
grep -q "Креирај увозна декларација" frontend/web/src/pages/Orders/OrderHub.tsx
grep -q "actions.imDeclaration" frontend/web/src/i18n/locales/mk.json

# Concurrency test:
# Open 2 browser tabs to /orders/X simultaneously, both create IM declarations
# → expect 2 distinct DeclarationNumbers (IM-2026-XXXXX1 + IM-2026-XXXXX2),
# both pointed at same ClientOrder.
```

**VPS smoke:**
1. From hub action → fill dialog → submit → Declarations tab on hub updates with new entry.
2. Verify ClientOrder.Status transitioned Draft → Active via re-fetch (or hub reload).

---

### §E4–§E9 (continued business flow wiring)

Each follows pattern of §E3:
- File presence: action button in OrderHub.tsx with t() key
- Locale keys MK + EN
- tsc + eslint clean
- VPS smoke: from real ClientOrder, execute the action, verify side-effect on hub

Specific extras:

**§E4 — Receipt:**
- After receive: InventoryBalance count via API increases by N where N = number of lines received.
- Variance: receive 95 of 100 declared → status of line on hub shows „Partially received".

**§E5 — BOM + ProductionOrder:**
- ClientOrderFinishedGood rows added; ProductionOrder created with FK clientOrderId; bom assigned.
- Smart suggestion: previously-used BOM for same Item shows in dropdown highlighted.

**§E6 — Podelba:**
- After podelba: InventoryBalance rows have AssignedProducerId set; Materials tab on hub filters by producer.

**§E7 — Issue + ProductionReceipt:**
- After issue: ProductionOrder.IssuedMaterials list populated.
- After receipt: ProductionOrder.ProducedQuantity increases; %Produced widget on hub updates.

**§E8 — EX + Shipment + QC:**
- After EX submission: Shipments tab + Declarations tab on hub show new entries.
- QC pass: FG.QualityStatus = OK; eligible for shipment.

**§E9 — Razdolzuvanje:**
- /orders/:id/razdolzuvanje renders; balance shown.
- Snapshot button: POST /api/Guarantee/snapshots → GuaranteeBalanceSnapshot row created.
- If balance reconciled + all lines flagged → ClientOrder.Status auto-transitions to Closed.

---

### §E10 — AI helper

```bash
test -f src/LON.Application/Ai/AiAssistantService.cs
grep -q "GetRecommendations" src/LON.Application/Ai/AiAssistantService.cs
grep -E '\[HttpPost\("recommendations' src/LON.API/Controllers/AiController.cs

# Integration tests
test -f tests/LON.IntegrationTests/AiHelperTests.cs
dotnet test --filter "FullyQualifiedName~AiHelper" 2>&1 | tail -10

# Frontend
test -f frontend/web/src/components/common/AiHelperButton.tsx
grep -q "AiHelperButton" frontend/web/src/App.tsx

cd frontend/web
node_modules/.bin/tsc --noEmit
```

**AiSuggestionLog table:**
```bash
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT TOP 5 EntityType, RecommendationTitle, UserActedOn FROM AiSuggestionLog ORDER BY GeneratedAt DESC
"
```

**VPS smoke (3 scenarios):**
1. Create ClientOrder in Draft, no FGs → open hub → AI helper button → recommendations panel shows „Внеси готови производи (BOM)" with action link.
2. Create Receipt with received qty 90/100 (10% variance) → AI helper recommends „Просечен variance ... провери packaging".
3. Approach Razdolzuvanje on order with unconsumed materials → AI helper recommends „Има N линии IM без EX consumption".

---

### §E11 — Domain events + handler refactor

```bash
# Interfaces + concrete events
test -f src/LON.Domain/Common/IDomainEvent.cs
grep -E "class (ClientOrderCreated|CustomsDeclarationApproved|ReceiptCommitted|ShipmentCommitted)Event" src/LON.Domain --include="*.cs" -r | wc -l
# Expected: ≥8

# Dispatcher
grep -q "Publish.*_events\|MediatR.Publish" src/LON.Infrastructure/Persistence/ApplicationDbContext.cs

# Audit log table
ls src/LON.Infrastructure/Migrations/*DomainEventLog*.cs

# Handler refactor evidence: handlers no longer directly insert
# GuaranteeLedgerEntry from CustomsDeclaration approval handler
grep -A 20 "ApproveCustomsDeclarationCommandHandler" src/LON.Application/Customs/**/*.cs | grep -E "GuaranteeLedgerEntry|GuaranteeAccount"
# Expected: ZERO matches (handler emits event; separate event handler creates ledger)

# All integration tests still pass
dotnet test 2>&1 | grep -E "Total tests:|Passed:|Failed:"
# Expected: Failed: 0
```

---

### §E12 — SQL SEQUENCE objects

```bash
# Migration applied
ls src/LON.Infrastructure/Migrations/*NumberSequences*.cs

# All expected sequences exist
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT name FROM sys.sequences ORDER BY name
"
# Expected: seq_ClientOrder_*, seq_CustomsDeclarationIM_*, seq_CustomsDeclarationEX_*,
#           seq_Receipt_*, seq_MaterialIssue_*, seq_Shipment_*, seq_ProductionOrder_*,
#           seq_GuaranteeLedger_*

# No DMax+1 in new handler code
grep -rn "DMax\|\.Max(.*)+ 1\|MAX.*\+.*1" src/LON.Application --include="*.cs" | grep -v "/* legacy"
# Expected: empty or only commented-legacy

# Concurrency test: parallel creates produce unique numbers
test -f tests/LON.IntegrationTests/NumberingConcurrencyTests.cs
dotnet test --filter "FullyQualifiedName~NumberingConcurrency" 2>&1 | tail -5
```

---

### §E13 — Audit interceptor

```bash
test -f src/LON.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs
grep -q "AddInterceptors.*AuditInterceptor" src/LON.Infrastructure/

# AuditLogEntry rows after entity modification:
# (Run an integration test that updates a ClientOrder, then query)
dotnet test --filter "FullyQualifiedName~AuditInterceptorTests" 2>&1 | tail -5

# UI
test -f frontend/web/src/pages/Admin/AuditLog.tsx
grep -q "/admin/audit-log" frontend/web/src/App.tsx
```

**VPS smoke:**
1. Login as Administrator → /admin/audit-log → DataTable loads.
2. Modify a ClientOrder field → reload /admin/audit-log → new entry visible with ChangedFields JSON expanded.

---

### §E14 — Soft-delete + recycle bin

```bash
# Interface applied
grep -rln "ISoftDeletable" src/LON.Domain/Entities/ --include="*.cs" | wc -l
# Expected: ≥10 (per BLUEPRINT §3.7 list)

# Global query filter
grep -q "WHERE !.*IsDeleted\|.IsDeleted == false" src/LON.Infrastructure/Persistence/Configurations/*.cs

# Recycle bin UI
test -f frontend/web/src/pages/Admin/RecycleBin.tsx
grep -q "/admin/recycle-bin" frontend/web/src/App.tsx

# Retention worker
grep -q "SoftDeleteRetentionJob\|HardDeleteAfter90Days" src/LON.Worker/
```

---

### §E7.5 — Department + Position lookup promotion

> **D6 decided 2026-05-12: prod-export at Phase 21 cutover.** Two execution paths:
> - **Path A (recommended): Defer entire task to Phase 21.1.1.** Skip this verification block in Phase 17.
> - **Path B: Land schema in Phase 17, backfill in Phase 21.1.1.** Verify schema present + categories created with 0 rows; the backfill check below is trivially 0/0. Backfill verification re-runs in Phase 21.1.1 after prod-export staging is loaded.

```bash
# Migration applied
ls src/LON.Infrastructure/Migrations/*DeptPosition*.cs

# Schema check
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('Employees') AND name IN ('DepartmentId','PositionId')
"
# Expected: 2 rows

# CodeListItem rows seeded (D6=fresh-start: 2 rows expected, both count=0; D6=prod-export: skip this task)
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT Category, COUNT(*) FROM CodeListItems
  WHERE Category IN ('EmployeeDepartment','EmployeePosition') GROUP BY Category
"
# Expected: 2 categories rendered (count may be 0 in fresh-start mode)

# Backfill verified (D6=fresh-start: trivially 0 since no legacy Department strings)
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT COUNT(*) FROM Employees WHERE Department IS NOT NULL AND DepartmentId IS NULL
"
# Expected: 0 (all mapped — or trivially zero in fresh-start)

# UI
grep -q "DepartmentId\|departmentId" frontend/web/src/pages/EmployeeManagement.tsx
grep -q "CodeListItem" frontend/web/src/pages/EmployeeManagement.tsx

cd frontend/web
node_modules/.bin/tsc --noEmit
node_modules/.bin/eslint src
dotnet test --filter "FullyQualifiedName~Employee" 2>&1 | tail -5
```

**VPS smoke:** open EmployeeForm → Department dropdown loads existing values + accepts inline „Add new" → new entry appears in dropdown immediately.

---

### §E7.6 — `DeliveryNote` entity + polymorphic auto-gen

```bash
# Entities + migration
test -f src/LON.Domain/Entities/Logistics/DeliveryNote.cs
test -f src/LON.Domain/Entities/Logistics/DeliveryNoteLine.cs
ls src/LON.Infrastructure/Migrations/*phase-17*DeliveryNote*.cs

# DbSet exposed
grep -q "DbSet<DeliveryNote>" src/LON.Infrastructure/Persistence/ApplicationDbContext.cs
grep -q "DbSet<DeliveryNote>" src/LON.Application/Common/Interfaces/IApplicationDbContext.cs

# SQL SEQUENCE exists
sqlcmd -S localhost -E -d LONDB -h -1 -W -Q "
  SELECT name FROM sys.sequences WHERE name LIKE 'seq_DeliveryNote%'
"
# Expected: ≥1 row

# Handlers
ls src/LON.Application/Logistics/DeliveryNotes/
# Expected: Get + Update + Confirm + Cancel handlers

# Endpoints
grep -E '\[Http(Get|Post|Put|Delete)' src/LON.API/Controllers/LogisticsController.cs | wc -l
# Expected: ≥5 endpoints (list / by-id / update / confirm / cancel / pdf)

# Auto-gen wiring verified via integration tests
test -f tests/LON.IntegrationTests/DeliveryNoteTests.cs
dotnet test --filter "FullyQualifiedName~DeliveryNote" 2>&1 | tail -10
# Expected: green; ≥6 tests (3 auto-gen types + status transitions + tenant isolation + PDF smoke)

# UI
test -f frontend/web/src/pages/Logistics/DeliveryNoteList.tsx
test -f frontend/web/src/pages/Logistics/DeliveryNoteDetail.tsx
grep -q "/warehouse/delivery-notes" frontend/web/src/App.tsx

# OpenAPI types regenerated
./scripts/gen-api-types.sh
git diff --stat frontend/web/src/api/schema.d.ts
```

**VPS smoke (3 scenarios):**
1. From hub: trigger MaterialIssue (E7 flow) → toast appears „Создаден DN-YYYY-NNNNNN" → click toast → DN detail loads with line(s) from MaterialIssue.
2. From hub: commit Shipment Export (E8) → DN(Type=CustomerShipment) auto-created.
3. From hub: commit FinishedGoodReceipt (E8 sub-flow) → DN(Type=ProducerReturn) auto-created.
4. Confirm one DN → status flips to `Sent`; PDF link works; PDF contains the line data.

**Z2779 fixture check:**
After PRE.7 Z2779 import re-runs (or via dry-run on existing data), expect exactly 1 DeliveryNote(Type=ProducerDispatch) for Z2779's single Izdatnica, with line count matching IssuedMaterials.

---

### §E8.5 — `CommercialInvoice` entity + EX hub chain

```bash
# Entities + migration
test -f src/LON.Domain/Entities/Customs/CommercialInvoice.cs
test -f src/LON.Domain/Entities/Customs/CommercialInvoiceLine.cs
ls src/LON.Infrastructure/Migrations/*phase-17*CommercialInvoice*.cs

# DbSet exposed
grep -q "DbSet<CommercialInvoice>" src/LON.Infrastructure/Persistence/ApplicationDbContext.cs
grep -q "DbSet<CommercialInvoice>" src/LON.Application/Common/Interfaces/IApplicationDbContext.cs

# SQL SEQUENCE
sqlcmd -S localhost -E -d LONDB -h -1 -W -Q "
  SELECT name FROM sys.sequences WHERE name LIKE 'seq_CommercialInvoice%'
"
# Expected: ≥1 row per tenant

# Handlers
ls src/LON.Application/Customs/CommercialInvoices/
# Expected: Create + Update + Issue + Cancel + GetList + GetById + Suggest handlers

# Endpoints
grep -E '\[Http(Get|Post|Put|Delete)' src/LON.API/Controllers/CommercialInvoicesController.cs | wc -l
# Expected: ≥7 endpoints

# Suggestion service
test -f src/LON.Application/Customs/CommercialInvoices/CommercialInvoiceSuggestionService.cs
grep -q "SuggestFromShipment" src/LON.Application/Customs/CommercialInvoices/CommercialInvoiceSuggestionService.cs

# Tests
test -f tests/LON.IntegrationTests/CommercialInvoiceTests.cs
dotnet test --filter "FullyQualifiedName~CommercialInvoice" 2>&1 | tail -10
# Expected: green; ≥8 tests (CRUD + tenant + numbering concurrency + suggest + status transitions + PDF)

# UI
test -f frontend/web/src/pages/Customs/CommercialInvoiceList.tsx
test -f frontend/web/src/pages/Customs/CommercialInvoiceDetail.tsx
grep -q "/customs/commercial-invoices" frontend/web/src/App.tsx

# Hub integration: tab on /orders/:id
grep -q "commercial-invoices\|CommercialInvoice" frontend/web/src/pages/Orders/OrderHub.tsx
```

**VPS smoke:**
1. Create EX declaration from hub (E8) → after submit → toast „EX поднесен. Креирај commercial invoice?" → click → form opens with line draft from Shipment.
2. Fill consignee/consignor/incoterms (or accept defaults) → Save Draft → list shows new CI.
3. Issue → status flips to `Issued`; lines locked; PDF download works; PDF contains correct lines/values.
4. Visit ClientOrder hub → „Commercial invoices" tab → CI is listed.

**Z2779 fixture check:** Z2779 has no `tblIzvozniFakturi` correlation in legacy snapshot (fully-inward processing cycle), so PRE.7 does NOT produce a CommercialInvoice for Z2779. Phase 21 dry-run on broader Zaklucoci is when this entity gets meaningful migration data.

---

### §E10.5 — AlertRule + AlertEvent + worker

```bash
# Entities + migration
test -f src/LON.Domain/Entities/Management/AlertRule.cs
test -f src/LON.Domain/Entities/Management/AlertEvent.cs
ls src/LON.Infrastructure/Migrations/*AlertRulesAndEvents*.cs

# Seeded 6 rules
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT COUNT(*) FROM AlertRules WHERE IsActive=1
"
# Expected: 6

# Worker
test -f src/LON.Worker/Jobs/AlertEvaluatorJob.cs
grep -q "AddHostedService.*AlertEvaluatorJob" src/LON.Worker/Program.cs

# Endpoints
grep -E '\[Http(Get|Post)\("alerts' src/LON.API/Controllers/ManagementController.cs
# Expected: ≥3 entries (GET list, POST acknowledge, POST resolve)

# Tests
test -f tests/LON.IntegrationTests/AlertRulesTests.cs
dotnet test --filter "FullyQualifiedName~AlertRules" 2>&1 | tail -10
# Expected: green; ≥7 tests (one per rule + acknowledge + dedupe)

# UI not localStorage anymore
grep -c "localStorage.setItem\|localStorage.getItem" frontend/web/src/pages/Management/Alerts.tsx
# Expected: 0
```

**VPS smoke:**
1. Wait for worker to run (10 min after deploy).
2. Force a condition: set GuaranteeAccount.CurrentBalance via SQL to >95% of ceiling → restart worker → reload `/management/alerts` → new alert row visible.
3. Click „Acknowledge" → row moves to Acknowledged state; audit log entry present.
4. Dashboard card „Open alerts" shows updated count.

---

### §E16 — FxRate entity + maintenance UI

```bash
# Entity + migration
test -f src/LON.Domain/Entities/Finance/FxRate.cs
ls src/LON.Infrastructure/Migrations/*FxRate*.cs

# Seeded rates
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT FromCurrency, ToCurrency, Rate FROM FxRates WHERE EffectiveDate <= GETDATE() ORDER BY FromCurrency
"
# Expected: ≥3 rows (EUR/MKD, USD/MKD, USD/EUR)

# Service
test -f src/LON.Application/Finance/FxRateService.cs
grep -q "Task<decimal> GetRate" src/LON.Application/Finance/FxRateService.cs

# Handlers
ls src/LON.Application/Finance/FxRates/

# Endpoints
grep -E '\[Http(Get|Post|Put|Delete)\(' src/LON.API/Controllers/FinanceController.cs | grep -i fx
# OR new dedicated controller
test -f src/LON.API/Controllers/FxRatesController.cs || grep -q "fx-rates" src/LON.API/Controllers/FinanceController.cs

# UI
test -f frontend/web/src/pages/Finance/FxRates.tsx
grep -q "/finance/fx-rates" frontend/web/src/App.tsx

# Tests
test -f tests/LON.IntegrationTests/FxRateTests.cs
dotnet test --filter "FullyQualifiedName~FxRate" 2>&1 | tail -10
# Expected: green; ≥5 tests (CRUD + GetRate + cross-rate fallback + exception on missing)
```

**VPS smoke:**
1. Create FX rate EUR/MKD = 61.50 effective today via UI.
2. Create test ClientOrder in EUR with €100 line.
3. Generate Invoice → verify margin calc uses 61.50.
4. Update rate to 62.00 effective tomorrow; re-fetch margin (today's value) → still 61.50.

---

### §E15 — Playwright E2E happy-path

```bash
test -f tests/playwright/playwright.config.ts
test -f tests/playwright/tests/happy-path.spec.ts

cd tests/playwright
npm install
npx playwright test --reporter=line
# Expected: 1 test passed (happy-path)

# Run against VPS too
BASE_URL=https://elon.elbosoft.click API_URL=https://elon.elbosoft.click/api npx playwright test
# Expected: 1 test passed
```

**Screenshot evidence:** Playwright auto-records video on failure; on success, attach `playwright-report/index.html` to SESSION_LOG entry.

---

## Phase 18 — Subcontractor (§F)

### §F1 — Role + JWT claim

```bash
# Role seeded
grep -q "Subcontractor" src/LON.Migration/Seed/
# OR check via API
curl -fsSL -u admin "https://elon.elbosoft.click/api/Roles" | jq '.data[] | select(.name=="Subcontractor")'
# Expected: 1 result

# JWT contains claim (decode token of a Subcontractor user; check external_partner_id)
```

### §F2 — Server-side filter + RBAC

```bash
test -f tests/LON.IntegrationTests/SubcontractorIsolationTests.cs
dotnet test --filter "FullyQualifiedName~SubcontractorIsolation" 2>&1 | tail -10
# Expected: green; tests that subcontractor sees only their POs + 403 on forbidden endpoints
```

### §F3 — Subcontractor dashboard

```bash
test -f frontend/web/src/pages/Producer/Dashboard.tsx
grep -q "/producer/dashboard" frontend/web/src/App.tsx
```

**VPS smoke:** Login as subcontractor, see only their POs.

### §F5 — Playwright

```bash
test -f tests/playwright/tests/subcontractor-isolation.spec.ts
cd tests/playwright && npx playwright test subcontractor-isolation
# Expected: green
```

---

## Phase 19 — Speditor (§G)

Mirror Phase 18 (auth/filter/dashboard/E2E pattern).

```bash
test -f tests/playwright/tests/speditor.spec.ts
cd tests/playwright && npx playwright test speditor
```

---

## Phase 20 — RLS + tenant security (§H)

### §H1–H2 — RLS policy + middleware

```bash
# Policy exists
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  SELECT name FROM sys.security_policies WHERE name='TenantIsolationPolicy'
"
# Expected: 1 row, is_enabled=1

# SQL-level isolation test
docker exec lon-sqlserver /opt/mssql-tools/bin/sqlcmd -U sa -P "$SA_PASS" -d Teksport -Q "
  EXEC sp_set_session_context 'TenantId', N'00000000-0000-0000-0000-000000000000';
  SELECT COUNT(*) FROM ClientOrders
"
# Expected: 0 (no rows match the fake tenant)
```

### §H3 — Pen test

```bash
test -f docs/security/PHASE20_PENTEST.md
# Contains: 3+ tampered JWT scenarios + their blocked-result evidence
```

### §H4 — Security audit document

```bash
test -f docs/security/PHASE20_AUDIT.md
# Contains: sign-off section, all 8 audit topics
```

### §H5 — Backup + restore

```bash
ls /opt/apps/LON/backups/ | tail -10
# Expected: 7+ days of daily backups
# Run restore drill on staging container; document in SESSION_LOG
```

---

## Phase 21 — Migration + launch (§I)

### §I1 — Migration reconciliation

```bash
# Reconciliation queries return clean
dotnet run --project src/LON.Migration --verify
# Expected: all 6 reconciliation checks pass (variance within tolerance)
```

### §I2 — Cutover plan

```bash
test -f docs/launch/PHASE21_CUTOVER_PLAN.md
# Contains: T-7/T-1/T-0 timeline, rollback procedure, user sign-off
```

### §I3 — USER_MANUAL

```bash
# Updated for hub UX
grep -q "ClientOrder hub\|/orders/" docs/USER_MANUAL.md
```

### §I4 — Final E2E sweep

```bash
cd tests/playwright
BASE_URL=https://elon.elbosoft.click npx playwright test --reporter=html
# Expected: 4+ E2E spec files all green
```

### §I5 — Go-live

Final ceremony:
- Cutover plan executed.
- Live tail of logs for 48h.
- Issues documented in SESSION_LOG.
- Phase 22+ post-v1 backlog filed.

---

## §J — Playwright E2E patterns (shared reference)

### §J1 — Local test run

```bash
cd tests/playwright
npm ci
npx playwright install --with-deps chromium
npx playwright test --headed   # see browser
npx playwright test            # headless (CI)
npx playwright test --debug    # step-through
npx playwright test --ui       # UI mode
```

### §J2 — Generate test report

```bash
npx playwright show-report
# Opens browser to HTML report
```

### §J3 — Test data hygiene

- Each test creates own tenant via API (no shared mutable state).
- Teardown deletes tenant.
- If a test fails mid-flow, teardown still runs (Playwright `afterEach`).

### §J4 — Flake mitigation

- Use `await expect(locator).toBeVisible()` instead of `waitFor` literals.
- Avoid arbitrary `sleep`. Use Playwright's auto-wait.
- Stable selectors: `data-testid="..."` attributes preferred over text (i18n changes).
- Retry once on CI (not locally): `playwright.config.ts: retries: process.env.CI ? 1 : 0`.

### §J5 — Visual regression (post-v1)

If we add: `await expect(page).toHaveScreenshot('hub-empty.png', { maxDiffPixelRatio: 0.01 });`
Stored in `tests/playwright/__screenshots__/`. Re-baseline with `--update-snapshots`.

### §J6 — CI configuration sample

```yaml
# .github/workflows/e2e.yml
name: E2E
on: [pull_request, schedule]
jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker compose -f docker-compose.yml up -d --build
      - run: ./scripts/wait-for-api.sh
      - uses: actions/setup-node@v4
        with: { node-version: 20 }
      - run: cd tests/playwright && npm ci && npx playwright install --with-deps chromium
      - run: cd tests/playwright && npx playwright test
      - if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: tests/playwright/playwright-report/
          retention-days: 14
```

---

## Cross-cutting rules

**Never:**
- Skip the OpenAPI type regen step when you touched a DTO. CI gate will catch you, but the symptom is a frontend that compiles locally then breaks runtime on VPS.
- Commit with `--no-verify`. The pre-commit hook is the only thing keeping tsc honest right now.
- Merge to main without a SESSION_LOG entry.
- Skip Playwright run before declaring Phase 17.E15 / 18.F5 / 19.G4 / 20.H3 / 21.I4 done.

**Always:**
- Run the universal pre-flight + post-task blocks.
- Take a screenshot of the VPS smoke step OR Playwright HTML report (evidence — text claims of "I tested it" don't count).
- Update PLAN.md → relevant phase section → mark `[x]` after evidence is in SESSION_LOG.

**If blocked:**
- File the blocker as a new `P{N}.X.followup` row in PLAN.md with `[!]` marker.
- Note in SESSION_LOG what specifically is blocking (DB connection? VPS down? Test flake? Missing decision Q11.X?).
- Do NOT move to a later prompt to "make progress" — the chaos in Phase 0–15 came from exactly this habit.
