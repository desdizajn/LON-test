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

## Cross-cutting rules

**Never:**
- Skip the OpenAPI type regen step when you touched a DTO. CI gate will catch you, but the symptom is a frontend that compiles locally then breaks runtime on VPS.
- Commit with `--no-verify`. The pre-commit hook is the only thing keeping tsc honest right now.
- Merge to main without a SESSION_LOG entry. The audit trail is how we'll write the new BLUEPRINT.md.

**Always:**
- Run the universal pre-flight + post-task blocks.
- Take a screenshot of the VPS smoke step (the screenshot IS the evidence — text claims of "I tested it" don't count).
- Update WORK_PLAN.md → "Phase 16" section → mark `[x]` after evidence is in SESSION_LOG.

**If blocked:**
- File the blocker as a new `P16.X.followup` row in WORK_PLAN.md with `[!]` marker.
- Note in SESSION_LOG what specifically is blocking (DB connection? VPS down? Test flake?).
- Do NOT move to a later prompt to "make progress" — the chaos in Phase 0–15 came from exactly this habit.
