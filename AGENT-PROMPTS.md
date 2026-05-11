# AGENT-PROMPTS — Phase 16 (Cleanup + UI Foundation)

> Self-contained prompts for Claude Code sessions. Each prompt assumes the agent starts with **zero context** beyond `CLAUDE.md`, `VERIFICATION.md`, and the codebase. Copy a prompt verbatim into Claude Code.
>
> **Order matters.** A1 → A2 → A3, then B1 → B2 → B3, then C1 → C2 → C3 (parallel D allowed). Don't skip ahead.
>
> Every prompt ends with: *"Before declaring done, walk through the matching section in `VERIFICATION.md`. Paste evidence into `SESSION_LOG.md`."*

---

## Phase 16.A — Cleanup (no business logic changes)

### A1 — Remove dead `WarehousesList` and `/master-data/warehouses-old` route

```
Read CLAUDE.md (Section 11) and VERIFICATION.md (Section A1) before starting.

CONTEXT
We have two warehouse list pages, both wired in App.tsx:
- pages/MasterData/Warehouses/WarehousesList.tsx (148 lines, legacy, route /master-data/warehouses-old)
- pages/MasterData/WarehouseList.tsx (227 lines, current, route /master-data/warehouses)

The "old" route is not referenced from navGroups.ts, sidebar, or any other page.
It exists only because nobody deleted it after the rewrite.

TASK
1. Confirm via grep that nothing else references the old component:
     grep -rn "WarehousesList" frontend/web/src --include="*.tsx" --include="*.ts"
     grep -rn "warehouses-old" frontend/web/src
   If anything outside App.tsx references it, STOP and report.
2. Delete pages/MasterData/Warehouses/WarehousesList.tsx.
3. If pages/MasterData/Warehouses/ is now empty, delete the directory.
4. Remove the import line + Route line for WarehousesList from App.tsx.
5. Run tsc + eslint (see VERIFICATION.md A1).
6. Run filterNavGroups.test.ts — must still pass.
7. Commit: `phase-16.a1: remove dead WarehousesList + /warehouses-old route`
8. Deploy to VPS. Open /master-data/warehouses on VPS and verify the live list still loads.

Before declaring done, walk through VERIFICATION.md Section A1. Paste evidence into SESSION_LOG.md.
```

### A2 — Honest `navGroups.ts`: flag the 6 localStorage-only pages

```
Read CLAUDE.md (Section 11.3) and VERIFICATION.md (Section A2) before starting.

CONTEXT
6 pages persist business data to localStorage instead of a backend:
  pages/Management/Escalations.tsx       (key: 'lon.escalations.<tenant>')
  pages/Management/OpenRisks.tsx         (key: 'lon.risks.<tenant>')
  pages/Finance/CostAccounting.tsx
  pages/Finance/PayrollAggregate.tsx
  pages/Finance/SupplierInvoices.tsx
  pages/Hr/Training.tsx

All 6 are marked `backendStatus: 'exists'` in frontend/web/src/nav/navGroups.ts.
This lies. CLAUDE.md Section 6.1 forbids it.

TASK
1. For each of the 6 nav items above:
   a. Change `backendStatus: 'exists'` to `backendStatus: 'partial'`.
   b. Add `workPlanRef: 'P16.C1'` (Escalations + OpenRisks share P16.C1),
      `'P16.C2'` (Training), `'P16.C3'` (CostAccounting, PayrollAggregate, SupplierInvoices).
   c. Append to `existingDataHint` the literal string:
        ' ⚠ Тековно се чува во browser localStorage — се губи при cache clear. P16.C ќе го замени со BE entity.'
2. In each of the 6 page files, add a top-of-page banner component (use MUI <Alert severity="warning">) reading:
     t('common.localStorageWarning')
   and add the key to all 4 locales/*.json:
     EN: "This view is browser-local until backend lands. Data may be lost on cache clear."
     MK: "Овие податоци се чуваат само во browserот. Може да се изгубат при чистење на cache."
     SQ: "Këto të dhëna ruhen vetëm në shfletues. Mund të humbasin gjatë pastrimit të cache-it."
     SR: "Ови подаци се чувају само у прегледачу. Могу се изгубити при чишћењу кеша."
3. Run filterNavGroups.test.ts — must still pass (it only checks group structure, not item status).
4. tsc + eslint clean.
5. Commit: `phase-16.a2: honest navGroups status for 6 localStorage-only pages`
6. Deploy to VPS. Visit /management/risks — verify the warning banner appears.

Before declaring done, walk through VERIFICATION.md Section A2. Paste evidence into SESSION_LOG.md.
```

### A3 — Reconcile `Location*` / `Warehouse*` duplication audit

```
Read CLAUDE.md (Section 11) and VERIFICATION.md (Section A3) before starting.

CONTEXT
Beyond A1's known dupe, MasterData has several pages that look like they may
have legacy/new copies:
  pages/MasterData/LocationList.tsx
  pages/MasterData/LocationForm.tsx
  pages/MasterData/WarehouseList.tsx
  pages/MasterData/WarehouseForm.tsx
vs subfolders:
  pages/MasterData/Partners/{PartnersList,PartnerDetail,PartnerForm}.tsx
  pages/MasterData/Items/{ItemsList,ItemDetail,ItemForm}.tsx
  pages/MasterData/Routings/{RoutingsList,RoutingDetail}.tsx
  pages/MasterData/BOMs/{BOMsList,BOMDetail}.tsx
  pages/MasterData/UoM/{UoMList,UoMForm}.tsx
  pages/MasterData/TariffCodes/TariffBrowser.tsx
  pages/MasterData/Machines/MachineList.tsx
  pages/MasterData/WorkCenters/WorkCenterList.tsx

The flat Location*/Warehouse* files at MasterData/ root may be the canonical
ones (App.tsx wires them) or stale copies.

TASK (audit-only, no deletions without proof)
1. For each flat file (LocationList, LocationForm, WarehouseList, WarehouseForm),
   verify via grep it is the only candidate at its route. If a duplicate exists
   in a subfolder, compare line counts and identify last commit per `git log`.
2. Produce a written audit table in docs/PHASE16_AUDIT.md with columns:
   Component | Path | Routed? | Lines | LastCommit | KeepOrDelete | Reason
3. Do NOT delete in this task. If anything is unambiguously dead, file an
   A3-followup task in WORK_PLAN.md and stop.
4. Also list pages in pages/ root (Customs.tsx, Production.tsx, Inventory.tsx,
   etc.) — note which are wired and which are dead.
5. Commit: `phase-16.a3: MasterData duplication audit (no deletes)`
6. No VPS deploy needed (docs only).

Before declaring done, walk through VERIFICATION.md Section A3. Paste evidence into SESSION_LOG.md.
```

---

## Phase 16.B — UI Foundations

### B1 — Install react-query + migrate `pages/Inventory.tsx` as pilot

```
Read CLAUDE.md (Section 6.1, 11) and VERIFICATION.md (Section B1) before starting.

CONTEXT
122 frontend pages, each with hand-rolled `useState + useEffect + axios`. No
caching, no refetch-on-focus, no invalidation. We will adopt @tanstack/react-query
as the single data-fetching primitive going forward. pages/Inventory.tsx (660
lines, /warehouse/receipts) is the highest-traffic page and the right pilot.

TASK
1. Install @tanstack/react-query (latest v5 compatible with TS 4.9.5 — if v5 needs
   TS 5+, install v4):
     cd frontend/web && npm install @tanstack/react-query @tanstack/react-query-devtools
2. Wrap App.tsx in <QueryClientProvider> with a sane default
   (staleTime 30s, refetchOnWindowFocus true). Add ReactQueryDevtools only when
   process.env.NODE_ENV === 'development'.
3. Create frontend/web/src/hooks/queries/ directory. First file:
   useInventory.ts — exports useInventoryQuery (GET /WMS/inventory with filters)
   plus mutations: useReceiptCreate, useTransferCreate, useShipmentCreate,
   useCycleCountCreate, useAdjustmentCreate, useQualityStatusChange, useMoveBatch.
4. Rewrite pages/Inventory.tsx to use these hooks. Behaviour MUST be identical
   from the user's POV — same filters, same columns, same actions. Refactor
   only data-fetching plumbing.
5. After mutations, invalidate the inventory query (no manual reload calls).
6. Pre-commit checks: tsc --noEmit + eslint clean.
7. Commit: `phase-16.b1: react-query + pilot migration of Inventory.tsx`
8. Deploy to VPS. Open /warehouse/receipts. Verify:
   - List loads
   - Filter change refreshes data
   - Create a receipt → list updates without manual reload
   - Open another tab, mutate inventory, switch back — data refetches on focus

Before declaring done, walk through VERIFICATION.md Section B1. Paste evidence into SESSION_LOG.md.
```

### B2 — Standardize on `components/common/DataTable.tsx`

```
Read CLAUDE.md (Section 6.1) and VERIFICATION.md (Section B2) before starting.

CONTEXT
We have components/common/DataTable.tsx (used by 6 pages). The other 116 pages
hand-roll <table> markup. Goal in this task is NOT to migrate all 116. Goal is
to make DataTable production-ready so it can absorb new pages and migrations.

TASK
1. Audit current DataTable.tsx capabilities. List what it has and what's missing
   for production warehouse/customs/finance tables:
     - Sortable columns?
     - Pagination?
     - Row selection (multi)?
     - Custom cell renderers?
     - Sticky header?
     - Loading + empty states?
     - Mobile responsive?
   Produce gap list in docs/PHASE16_DATATABLE_GAPS.md.
2. Implement the gaps. Test by writing components/common/DataTable.test.tsx
   covering: render with empty data, render with rows, sort click, pagination
   click, row selection. Use @testing-library/react.
3. Pick 1 secondary pilot: pages/Production.tsx (the orders table). Migrate its
   hand-rolled table to DataTable. (Don't touch data fetching here — that's B1's
   pattern for the next pilot.)
4. tsc + eslint clean. New DataTable tests pass.
5. Commit: `phase-16.b2: harden DataTable + migrate Production.tsx orders grid`
6. Deploy. Verify /production/orders renders identically.

Before declaring done, walk through VERIFICATION.md Section B2. Paste evidence into SESSION_LOG.md.
```

### B3 — Layout shell + theme tokens

```
Read CLAUDE.md (Section 6.1) and VERIFICATION.md (Section B3) before starting.

CONTEXT
components/layout/ has 1 file (55 lines). The Sidebar + TopBar are at
components/Sidebar.tsx and components/TopBar.tsx. Pages render content in 91
different layouts (some with inline padding, some with className, some bare).
This is a frequent source of "looks broken on mobile" bugs.

TASK
1. Create components/layout/PageShell.tsx — receives `title`, `actions` (right-aligned
   button slot), `breadcrumbs?`, and `children`. Renders a consistent page header
   + content padding + responsive max-width.
2. Define MUI theme in frontend/web/src/theme.ts with the LON palette
   (read Taris_LON_management_logo.png if a color spec exists; otherwise use
   conservative defaults: primary #1e88e5, secondary #7b1fa2). Wrap App.tsx in
   <ThemeProvider>.
3. Migrate 3 pages as visible proof: pages/Dashboard.tsx, pages/Inventory.tsx,
   pages/Production.tsx. Behaviour identical, padding consistent, header
   consistent.
4. tsc + eslint clean.
5. Commit: `phase-16.b3: PageShell + MUI theme + migrate 3 pilot pages`
6. Deploy. Visit the 3 pages on both desktop (1920px) and a mobile-width
   viewport (375px) — header, sidebar collapse, actions row must all look
   intentional.

Before declaring done, walk through VERIFICATION.md Section B3. Paste evidence into SESSION_LOG.md.
```

---

## Phase 16.C — Replace localStorage-only pages with real backend

### C1 — `RiskRegisterItem` entity (covers Escalations + OpenRisks)

```
Read CLAUDE.md (Sections 6, 11.3) and VERIFICATION.md (Section C1) before starting.

CONTEXT
pages/Management/Escalations.tsx and pages/Management/OpenRisks.tsx use
localStorage. Both have nearly-identical shape (title, category/party, severity,
status, owner, mitigation/resolution, date fields). Unifying them under one
entity is correct.

TASK
1. New entity `RiskRegisterItem` in src/LON.Domain/Entities/Management/RiskRegisterItem.cs:
     Id (Guid, PK), TenantId (Guid, FK), Kind (enum: Risk | Escalation),
     Title (nvarchar 200), Category (nvarchar 60, nullable),
     Severity (enum: Low | Medium | High | Critical),
     Status (enum: Open | InReview | Mitigating | Resolved | Deferred | Closed),
     Owner (nvarchar 120, nullable), Mitigation (nvarchar max, nullable),
     Resolution (nvarchar max, nullable), DueDate (datetime, nullable),
     ReviewDate (datetime, nullable), CreatedAt (datetime, default now),
     UpdatedAt (datetime, default now).
2. Add DbSet<RiskRegisterItem> to ApplicationDbContext and IApplicationDbContext.
3. EF Configuration in src/LON.Infrastructure/Persistence/Configurations/
   with TenantId query filter (see existing CodeListItemConfiguration for pattern).
4. Create EF migration: phase-16.c1 add risk_register_item.
5. MediatR handlers in src/LON.Application/Management/Risks/:
   - CreateRiskRegisterItemCommand
   - UpdateRiskRegisterItemCommand
   - DeleteRiskRegisterItemCommand
   - GetRiskRegisterItemsQuery (filter by Kind)
   - GetRiskRegisterItemByIdQuery
6. Controller endpoints in ManagementController:
   - POST /api/Management/risks
   - PUT  /api/Management/risks/{id}
   - DELETE /api/Management/risks/{id}
   - GET  /api/Management/risks?kind=Risk|Escalation
   - GET  /api/Management/risks/{id}
7. Integration tests in tests/LON.IntegrationTests/RiskRegisterTests.cs:
   - Create → Get returns it
   - Update changes status
   - Delete removes it
   - Tenant isolation: tenant A can't see tenant B's risks
   - Query filter by Kind works
8. Run scripts/gen-api-types.sh and commit the schema diff.
9. Rewrite pages/Management/OpenRisks.tsx and pages/Management/Escalations.tsx
   to use react-query hooks (per B1 pattern). DELETE all localStorage usage.
10. One-time migration script docs/PHASE16_C1_LOCAL_TO_BE_MIGRATION.md:
    JS snippet a user can paste in browser console that reads localStorage
    keys 'lon.risks.<tenant>' and 'lon.escalations.<tenant>', POSTs them
    to the new endpoints, then clears localStorage. Keep manual — auto
    migration is not worth the risk.
11. Remove the A2-installed warning banners from these 2 pages.
12. Update navGroups.ts: flip both to `backendStatus: 'exists'`.
13. Commit: `phase-16.c1: RiskRegisterItem entity + migrate Risks/Escalations off localStorage`
14. Deploy. Manually test create/edit/delete on /management/risks and /management/escalations on VPS.

Before declaring done, walk through VERIFICATION.md Section C1. Paste evidence into SESSION_LOG.md.
```

### C2 — `EmployeeCertification` entity (covers HR Training)

```
Read CLAUDE.md (Sections 6, 11.3) and VERIFICATION.md (Section C2) before starting.

CONTEXT
pages/Hr/Training.tsx tracks employee certifications via localStorage. HR module
already has solid Employee/Attendance/Absence tables — this fits in cleanly.

TASK
1. Entity src/LON.Domain/Entities/MasterData/EmployeeCertification.cs:
     Id, TenantId, EmployeeId (FK), CertificationName (nvarchar 120),
     IssuedDate, ExpiryDate (nullable), IssuingAuthority (nvarchar 120),
     CertificateNumber (nvarchar 60, nullable), Notes, CreatedAt, UpdatedAt.
2. DbSet + Configuration + migration.
3. Handlers in src/LON.Application/Hr/Certifications/:
   - CreateEmployeeCertificationCommand
   - UpdateEmployeeCertificationCommand
   - DeleteEmployeeCertificationCommand
   - GetEmployeeCertificationsQuery (by employeeId optional; expiringWithinDays optional for traffic-light)
4. Endpoints under HrOperationsController:
   - POST/PUT/DELETE/GET /api/Hr/certifications
   - GET /api/Hr/certifications/expiring?withinDays=30
5. Integration tests covering tenant isolation + expiring filter logic.
6. Rewrite pages/Hr/Training.tsx to use react-query. Delete localStorage usage.
7. Browser-console migration snippet in docs/PHASE16_C2_TRAINING_MIGRATION.md.
8. Remove warning banner. Flip navGroups status.
9. Commit: `phase-16.c2: EmployeeCertification entity + migrate Training off localStorage`
10. Deploy + manual test.

Before declaring done, walk through VERIFICATION.md Section C2. Paste evidence into SESSION_LOG.md.
```

### C3 — Finance localStorage-three (CostAccounting, PayrollAggregate, SupplierInvoices)

```
Read CLAUDE.md (Sections 6, 11.3) and VERIFICATION.md (Section C3) before starting.

CONTEXT
Three Finance pages use localStorage:
  CostAccounting    — cost per machine/operator/shift
  PayrollAggregate  — aggregated hours for payroll export
  SupplierInvoices  — accounts-payable register

These have richer domain logic than C1/C2. Treat each as its own sub-task.
Do them in order: C3.a (CostRate), C3.b (PayrollPeriod), C3.c (SupplierInvoice).

TASK (overview only — full per-sub-task prompts below)
For each sub-task:
- Define entity + handlers + endpoints + integration tests.
- Rewrite the page off localStorage.
- Migration snippet.
- Remove banner + flip navGroups.
- Deploy + manual test.

C3.a — CostRate entity
  Fields: Id, TenantId, Scope (Machine|Operator|Shift|Operation), ScopeId (Guid?),
  CostPerHour, CostPerUnit, Currency, ValidFrom, ValidTo, Notes, CreatedAt.
  Endpoints under /api/Finance/cost-rates.
  Rewrite pages/Finance/CostAccounting.tsx.

C3.b — PayrollPeriod entity + PayrollLine child
  Period: Id, TenantId, PeriodStart, PeriodEnd, Status (Draft|Finalized|Exported),
  ExportedAt (nullable), Notes, CreatedAt.
  Line: Id, PeriodId, EmployeeId, RegularHours, OvertimeHours, AbsenceHours,
  BonusAmount, DeductionAmount, NetAmount, Currency.
  Endpoints under /api/Finance/payroll-periods + /payroll-periods/{id}/finalize +
  /payroll-periods/{id}/export.
  Rewrite pages/Finance/PayrollAggregate.tsx.
  Note: must read from existing AttendanceRecord/Absence tables to populate hours;
  do not duplicate.

C3.c — SupplierInvoice entity
  Fields: Id, TenantId, Number, SupplierPartnerId, InvoiceDate, DueDate,
  Amount, Currency, Status (Open|Paid|Overdue|Cancelled), PaidDate (nullable),
  Notes, CreatedAt, UpdatedAt.
  Endpoints under /api/Finance/supplier-invoices.
  Rewrite pages/Finance/SupplierInvoices.tsx.

Commit per sub-task: `phase-16.c3.a: CostRate entity`, etc.

Before declaring done on each sub-task, walk through VERIFICATION.md Section C3.[a|b|c]. Paste evidence into SESSION_LOG.md.
```

---

## Phase 16.D — Test coverage gap fill (parallelizable)

### D1 — `WMSController` integration tests

```
Read CLAUDE.md (Section 3 Contract Hygiene) and VERIFICATION.md (Section D1) before starting.

CONTEXT
WMSController has 25 endpoints. There's no WMSControllerTests file. Coverage
exists indirectly through sub-flow tests (Podelba, MoveBatch, BulkReceipt, etc.)
but not for the bread-and-butter GET routes.

TASK
1. Create tests/LON.IntegrationTests/WMSControllerTests.cs.
2. Cover at minimum: GET /WMS/inventory, GET /WMS/receipts, GET /WMS/shipments,
   GET /WMS/transfers, GET /WMS/cycle-counts, GET /WMS/pick-tasks, GET /WMS/skart,
   GET /WMS/inventory/mozni-minusi, POST /WMS/adjustments.
3. Each test: seed a minimal tenant + entity, hit endpoint, assert (a) HTTP 200,
   (b) response shape matches OpenAPI, (c) tenant isolation (other tenant doesn't
   see this data).
4. Run all integration tests: dotnet test tests/LON.IntegrationTests/.
   New tests + all 154 existing must pass.
5. Commit: `phase-16.d1: WMSController integration tests (10 endpoints)`
6. No deploy needed — test-only.

Before declaring done, walk through VERIFICATION.md Section D1. Paste evidence into SESSION_LOG.md.
```

### D2 — Auth + Roles + Permissions tests

```
Read CLAUDE.md (Section 3) and VERIFICATION.md (Section D2) before starting.

CONTEXT
AuthTests.cs and TenantIsolationTests.cs cover basics. But Roles/Permissions
boundary enforcement has no dedicated test. nav/filterNavGroups.test.ts asserts
sidebar filtering, but it's purely client-side — a malicious caller can still
hit /api/MasterData/items as a Warehouse Operator.

TASK
1. tests/LON.IntegrationTests/RolePermissionTests.cs:
   - For each seeded role (Warehouse Operator, Customs Officer, Production
     Operator, Quality Controller, HR Manager, Maintenance Tech, Finance Clerk,
     Manager, Administrator):
     - Authenticate as a user with only that role.
     - For each of ~10 representative endpoints across modules (one per
       controller), assert either 200 (allowed) or 403 (denied) matching the
       sidebar IA matrix in nav/navGroups.ts.
   - Test admin → all 200.
   - Test no-role user → all 403/401.
2. Run dotnet test. Add to CI gate.
3. Commit: `phase-16.d2: role × endpoint permission matrix tests`

Before declaring done, walk through VERIFICATION.md Section D2. Paste evidence into SESSION_LOG.md.
```

### D3 — MasterData CRUD smoke tests

```
Read CLAUDE.md (Section 3) and VERIFICATION.md (Section D3) before starting.

CONTEXT
Boms, Employees, Locations, Partners, Routings, UoMs, Warehouses, WorkCenters
controllers have no dedicated integration tests. Items has ItemsMediatrTests.cs
but doesn't cover the controller route directly.

TASK
1. tests/LON.IntegrationTests/MasterDataCrudTests.cs (one file, [Theory]-driven):
   For each (resource, sample-payload) pair:
     - POST /api/MasterData/{resource} → assert 201 + id
     - GET  /api/MasterData/{resource}/{id} → assert echo
     - PUT  /api/MasterData/{resource}/{id} → assert update
     - DELETE /api/MasterData/{resource}/{id} → assert gone
     - Tenant isolation: tenant A can't GET tenant B's resource
2. Resources to cover: boms, employees, locations, partners, routings, uom,
   warehouses, work-centers.
3. Run dotnet test.
4. Commit: `phase-16.d3: MasterData CRUD + tenant isolation smoke tests (8 resources)`

Before declaring done, walk through VERIFICATION.md Section D3. Paste evidence into SESSION_LOG.md.
```

---

## How to use this file

1. Pick the lowest-letter, lowest-number prompt not yet done.
2. Open a fresh Claude Code session in the repo root.
3. Copy the prompt block (the fenced `\`\`\`` content) verbatim.
4. Let Claude Code execute. Stay in that session until VERIFICATION.md checks pass.
5. SESSION_LOG entry written, commit pushed, status updated in WORK_PLAN.md under "Phase 16".
6. Move to next prompt.

If a prompt fails midway: don't try to "patch" it from a different angle — fix the underlying issue, then resume that same prompt. Cross-prompt entanglement is how we got to the May 2026 chaos in the first place.
