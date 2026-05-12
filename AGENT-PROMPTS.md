# AGENT-PROMPTS — Phase 16 → 21 (path to v1)

> Self-contained prompts for Claude Code sessions. Each prompt assumes the agent starts with **zero context** beyond `CLAUDE.md`, `BLUEPRINT.md`, `PLAN.md`, `VERIFICATION.md`, and the codebase. Copy a prompt verbatim into Claude Code.
>
> **Order matters.** Follow PLAN.md §3 phase sequence. Within a phase, follow numbered sub-tasks.
>
> Every prompt ends with: *"Before declaring done, walk through the matching section in `VERIFICATION.md`. Paste evidence into `SESSION_LOG.md`."*
>
> **Sections:**
> - §A — Phase 16.A Cleanup
> - §B — Phase 16.B UI foundations
> - §C — Phase 16.C localStorage → backend
> - §D — Phase 16.D Test gap fill
> - §E — Phase 17 ClientOrder hub + flow wiring + AI helper
> - §F — Phase 18 Subcontractor login
> - §G — Phase 19 Speditor role
> - §H — Phase 20 RLS + tenant security
> - §I — Phase 21 Migration + launch
> - §J — Playwright E2E patterns (used across phases)

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

---

## §E — Phase 17: ClientOrder hub + flow wiring + AI helper

### E0 — Sticky-defaults hook + bulk field-update endpoint pattern (foundation for E3/E5/E8)

```
Read BLUEPRINT.md §7.3.1 (Sticky values pattern) + §5.2 (UX detail) and VERIFICATION.md §E0 before starting.

CONTEXT
Q11.3 resolution: per-line sticky-prefill from last-entered row for
high-variance fields (UoM, CountryOfOrigin, TariffCode), plus a generic
bulk field-update toolbar action для any line-table column.

REALITY CHECK (2026-05-12 PREP recon): TEKSPORT ELON has 43,223 EUR rows /
43,224 total (99.998% EUR). Currency is effectively single-value historically,
so the original Q11.3 framing („per-line currency change") is over-engineered.
Reframe: the **infrastructure** is the goal — sticky-defaults + generic bulk
update — and we exercise it on UoM/Country/TariffCode (where there IS variance),
plus currency as a degenerate case that still happens to work.

Tasks E3 (IM lines), E5 (BOM lines), E8 (EX lines) all need this — implement
once as a shared hook + component before wiring the line-tables.

TASK
1. Hook: frontend/web/src/hooks/useStickyDefaults.ts
   API:
     const { defaults, captureFrom, reset } = useStickyDefaults<TLine>(
       scopeKey: string,           // e.g. 'declaration-{id}-lines'
       initial: Partial<TLine>     // initial defaults (from Partner.PrimaryCurrency etc.)
     );
   Behavior:
     - State held in React Context per scope (NOT in localStorage — per-document,
       not cross-session).
     - `captureFrom(line)` updates defaults from a just-saved line (Currency,
       UoM, CountryOfOrigin, TariffCode).
     - Reset clears back to `initial`.
   Unit tests: tests/hooks/useStickyDefaults.test.tsx
     - Initial defaults used for first line.
     - After captureFrom on line1, line2 prefills from line1.
     - Reset works.
     - Two scopes don't bleed values.

2. Component: frontend/web/src/components/common/BulkFieldUpdateButton.tsx
   Props: { fieldName, currentValue, onConfirm, label, recalcWarning? }
   Behavior:
     - Renders a toolbar button (icon + label).
     - On click: ConfirmDialog with optional `recalcWarning` text
       (e.g. „Промена на валута ке ja recalculate-ира Vrednost според FX rate. Продолжи?").
     - On confirm: calls onConfirm; parent issues API call + audit reason.

3. Server side: handler must accept a bulk update with `Reason` field;
   record AuditLogEntry with Action='BulkUpdate' for each affected row.
   Pattern: POST /api/{Resource}/{parentId}/lines/bulk-update with body:
     { field: 'UoM' | 'CountryOfOrigin' | 'TariffCode' | 'Currency', value: '...', reason: '...' }
   Returns affected count + new line snapshot.
   Field whitelist enforced server-side (whitelist via FluentValidation).

4. Locale keys (en + mk only per v1 scope):
   - common.bulkUpdate.title / .confirm / .recalcWarning (generic — used for all fields)
   - common.stickyDefaults.tooltip („Стандарденте вредности се копираат од претходниот ред")

5. Pre-commit checks: tsc + eslint + jest.
6. Commit: `phase-17.0: useStickyDefaults hook + BulkFieldUpdateButton + bulk-update endpoint pattern`
7. No VPS deploy needed (foundation only — visible after E3 consumes it).

Before declaring done, walk through VERIFICATION.md Section E0. Paste evidence into SESSION_LOG.md.
```

### E1 — `ClientOrder` entity + migration + handlers + endpoints

```
Read BLUEPRINT.md §3.1 + §5.1 + §6.6 (numbering) + §6.7 (soft-delete), CLAUDE.md, and VERIFICATION.md §E1 before starting.

CONTEXT
The single biggest gap to v1 (PLAN.md §1 status: ClientOrder concept = Missing).
ClientOrder is the hub that ties together: customer order intent →
CustomsDeclaration(s) → ProductionOrder(s) → Shipment(s) → Razdolzuvanje.

TASK
1. Create entity src/LON.Domain/Entities/Customs/ClientOrder.cs per BLUEPRINT §3.1
   (TenantId, OrderNumber, CustomerPartnerId, LONAuthorizationId NOT NULL,
   CustomerOrderReference, OrderDate, RequestedShipDate, Status enum,
   audit fields, soft-delete fields).
2. Create entity ClientOrderFinishedGood (per BLUEPRINT §3.1).
3. ClientOrderStatus enum: Draft, Active, Producing, Shipped, Closed, Cancelled.
4. Add `ClientOrderId` nullable FK to existing CustomsDeclaration entity.
   Migration: add column + index; backfill: leave NULL on existing rows.
5. Add ClientOrderId nullable FK to ProductionOrder + Shipment.
6. ApplicationDbContext: DbSet<ClientOrder> + DbSet<ClientOrderFinishedGood>.
   IApplicationDbContext: expose both.
7. EF Configurations: TenantId query filter; IsDeleted filter; unique
   constraint on (TenantId, OrderNumber).
8. EF migration: `phase-17.1_ClientOrder`.
9. SQL SEQUENCE `seq_ClientOrder_<tenantId>` — generate in migration via
   `CREATE SEQUENCE` on tenant provisioning. For Teksport, add to seed
   data. (BLUEPRINT §6.6)
10. NumberFormatter helper in LON.Domain/Common/NumberFormatter.cs with
    `ClientOrder(int year, long seq)` returning `CO-{year}-{seq:D6}`.
11. MediatR handlers in LON.Application/Customs/ClientOrders/:
    - CreateClientOrderCommand (uses NumberFormatter + SEQUENCE)
    - UpdateClientOrderCommand (status auto-computed; only Cancel is user-set)
    - CancelClientOrderCommand
    - GetClientOrdersQuery (filter by status, customer, dateRange)
    - GetClientOrderByIdQuery (includes FinishedGoods + counts of linked entities)
12. Controller endpoints under /api/ClientOrders:
    - POST/PUT/DELETE/GET (with id and as list)
    - GET /api/ClientOrders/{id}/summary returns hub-card data (counts, balances)
13. Integration tests:
    - Create → Get returns it with auto-generated OrderNumber
    - Tenant isolation: tenant A can't GET tenant B's orders
    - Numbering: 5 parallel creates produce 5 distinct sequential numbers
    - Soft-delete: deleted order doesn't appear in default list, appears
      in IgnoreSoftDelete query
    - Authorization required: cannot create ClientOrder without valid
      LONAuthorizationId
14. Run scripts/gen-api-types.sh and commit schema diff.
15. Commit: `phase-17.1: ClientOrder entity + handlers + endpoints + tests`
16. Deploy to VPS.

Before declaring done, walk through VERIFICATION.md Section E1. Paste evidence into SESSION_LOG.md.
```

### E2 — ClientOrder list + hub UI

```
Read BLUEPRINT.md §5.1 + §7.1 (Hub-and-spoke) + §7.2 (Contextual actions), and VERIFICATION.md §E2.

CONTEXT
E1 created the entity + endpoints. Now build the user-facing hub. This is
the central UI shift — from per-page-island navigation to hub-and-spoke.

TASK
1. New route /orders (list page) — MUI DataTable, columns: OrderNumber,
   Customer, Status, OrderDate, RequestedShipDate, %Produced (progress bar),
   GuaranteeUtilization, Actions. Filters: status, customer, dateRange.
   Top-right „Нов налог" button → opens dialog (FormDialog + react-hook-form).
   Use react-query hook useClientOrders.
2. New route /orders/:id (hub page) per BLUEPRINT §5.1 layout:
   - Header: order number, status badge, customer link, authorization link,
     dates, manager note (optional).
   - Left vertical timeline: chronological events from /api/DomainEvents
     filtered by clientOrderId (placeholder events for now; real wiring
     in E11). Just stub three events: Created, FirstDeclarationLinked,
     LastShipped.
   - Center: 3 progress widgets (produced %, guarantee utilization %,
     days-to-ship).
   - Right sticky action launcher panel with action buttons (placeholders
     for now; E3–E9 wire each):
       • Внеси готови производи (BOM)
       • Креирај увозна декларација (IM)
       • Прими во магацин
       • Распредели подизведувач
       • Издади материјал
       • Креирај извозна декларација (EX)
       • Razdolzuvanje
       • Аудит / историја
       • 💡 AI препораки  ← (stub for E10)
   - Tabs below: Declarations | Production Orders | Shipments | Materials
     in stock. Each tab: small DataTable filtered by clientOrderId.
3. React Router add both routes.
4. Sidebar: add „📋 Налози" group with single item linking to /orders.
   Allowed roles: Administrator, Manager, ProductionPlanner; read-only
   visible for WhMgr, Customs, QC, Finance.
5. i18n keys EN + MK only (per BLUEPRINT §6.8 v1 scope):
   nav.orders.*, orders.list.*, orders.hub.*, orders.actions.*.
6. tsc + eslint clean.
7. Commit: `phase-17.2: ClientOrder list + hub UI shell`
8. Deploy. Verify on VPS at /orders that:
   - List loads (empty initially, then create 1 via API/UI)
   - Clicking row navigates to hub
   - All action buttons render disabled with tooltip „Coming in E3–E9"

Before declaring done, walk through VERIFICATION.md Section E2. Paste evidence into SESSION_LOG.md.
```

### E3 — Wire IM declaration creation from hub

```
Read BLUEPRINT.md §5.2 and VERIFICATION.md §E3.

CONTEXT
The hub's „Креирај увозна декларација" action launcher button must open
an inline-dialog (no navigation away) that creates a CustomsDeclaration
(DeclarationType=IM) linked to current ClientOrder.

TASK
1. Inline dialog in ClientOrderHub.tsx — opens on action click.
   Form fields (react-hook-form + Zod):
   - DeclarationNumber (auto-suggested from SEQUENCE; user can override)
   - DeclarationDate
   - CustomsProcedure (combo, defaults to LON's procedure 51 00 or 42 00)
   - Partner (sender) — autocomplete from /api/MasterData/partners
   - SenderName/Address/Country (auto-populate from Partner)
   - LONAuthorization — pre-filled from ClientOrder.LONAuthorizationId
2. „Преглед" tab in same dialog: lines editor (DataTable inline editing,
   or open separate routed line-edit page if cleaner).
3. „Зачувај како Draft" / „Поднеси (Submitted)" buttons.
4. On Create: hits POST /api/Customs/declarations with clientOrderId set;
   close dialog; refetch ClientOrder summary + Declarations tab.
5. Verify SEQUENCE: open 2 parallel browser tabs, create simultaneously,
   different numbers result (regression test for §6.6).
6. Commit: `phase-17.3: wire IM declaration creation from ClientOrder hub`
7. Deploy. VPS smoke: open /orders/{realId} → click action → fill in →
   submit → see in Declarations tab.

Before declaring done, walk through VERIFICATION.md Section E3. Paste evidence into SESSION_LOG.md.
```

### E4 — Wire Receipt from hub

```
Read BLUEPRINT.md §5.3 and VERIFICATION.md §E4.

CONTEXT
Hub action „Прими во магацин" must show approved IM declarations for this
ClientOrder, allow selecting one, and create a Receipt for it.

TASK
1. Action button on hub → dialog shows list of approved IM declarations
   for the ClientOrder (DataTable, columns: DeclarationNumber, Date,
   Sender, TotalLines, ReceivedLines).
2. Select declaration → next step: receive lines (one row per
   CustomsDeclarationLine):
   - Expected qty (from declaration)
   - Received qty (default = expected; user can override)
   - Skart qty (default 0; if >0 prompt reason)
   - Location (autocomplete from /api/MasterData/locations)
   - Batch number (optional auto-generate)
   - MRN (defaults to declaration MRN)
   - QualityStatus (default OK)
3. „Прими" button: POST /api/WMS/receipts with all line data.
4. Verify side effects via UI refetch: Inventory tab now shows new
   InventoryBalance rows; Declaration line marked as „Received".
5. Variance handling: if received <> expected, show AI helper hint
   inline (stub for now: „Препорака: проверете packaging").
6. Commit: `phase-17.4: wire Receipt creation from ClientOrder hub`
7. Deploy + VPS smoke (real declaration approved + receive flow).

Before declaring done, walk through VERIFICATION.md Section E4. Paste evidence into SESSION_LOG.md.
```

### E5 — Wire BOM + ProductionOrder from hub

```
Read BLUEPRINT.md §5.4, §7.3 (smart prefill) and VERIFICATION.md §E5.

TASK
1. Hub action „Внеси готови производи" → dialog with two-tab layout:
   Tab 1: ClientOrderFinishedGood rows editor (Item picker + qty + UoM).
   Tab 2: BOM picker per FG row — show existing BOMs for Item; or
   „Create new BOM" inline.
2. New BOM creation: BOMLine editor (Material picker + Normativ +
   WasteSlots per BOMLineWasteOverrides). Smart suggestion: most-used
   BOM for this Item (call /api/Suggestions/bom?itemId=X). If found,
   prefill BOMLines and let user adjust.
3. „Создај налог за производство" button per FG row → POST
   /api/Production/orders with clientOrderId + bomId + qty.
4. On save: refetch ProductionOrders tab; show count badge update.
5. Per-size variants (NormativiVelicini): if Item has size variants,
   show size-quantity grid inline; creates child POs with
   ProductionOrderMaterialSize entries.
6. Commit: `phase-17.5: wire BOM + ProductionOrder creation from hub`
7. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E5. Paste evidence into SESSION_LOG.md.
```

### E6 — Wire Podelba from hub

```
Read BLUEPRINT.md §5.6 + §7.4 (AI producer suggestion) and VERIFICATION.md §E6.

TASK
1. Hub action „Распредели подизведувач" → dialog:
   - Source: pick warehouse (default = HQ).
   - Target: pick producer (autocomplete /api/MasterData/partners
     ?type=Producer; show capacity hint inline if known).
   - Material rows: show available InventoryBalance for materials
     linked to ClientOrder POs; user picks qty per material.
2. Smart helper inline panel: „💡 препорачан подизведувач" — call
   /api/Suggestions/producer?clientOrderId=X (stub for now returning
   most-used producer in past 3 months; full impl in E10).
3. „Подели" button → POST /api/WMS/inventory/bulk-move-balances with
   producerId set.
4. Side effect: InventoryBalance rows update with AssignedProducerId;
   Materials tab on hub refreshes.
5. Commit: `phase-17.6: wire Podelba from hub + producer suggestion stub`
6. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E6. Paste evidence into SESSION_LOG.md.
```

### E7 — Wire MaterialIssue + ProductionReceipt from hub

```
Read BLUEPRINT.md §5.7, §5.8 and VERIFICATION.md §E7.

TASK
1. Hub action „Издади материјал" → dialog: list of ProductionOrders
   in Released status for this ClientOrder, with required materials
   from BOM. Select PO → review materials → „Issue all" button.
2. POST /api/Production/orders/{id}/issues/bulk.
3. Hub action „Запиши производство" → dialog: PO picker, qty produced
   (default = remaining), scrap qty, batch number. POST
   /api/Production/orders/{id}/receipts.
4. Both actions update hub progress widget („%Produced" recalculates).
5. Commit: `phase-17.7: wire MaterialIssue + ProductionReceipt from hub`
6. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E7. Paste evidence into SESSION_LOG.md.
```

### E7.6 — `DeliveryNote` entity + polymorphic auto-gen on commit events

```
Read BLUEPRINT.md §3.8 (DeliveryNote) + §5.6 (Podelba) + §5.7 (MaterialIssue) + VERIFICATION.md §E7.6.

CONTEXT (D5 decision 2026-05-12)
Legacy ELON's `Propratnici` + `PropratniciStavki` (1,658 + 295,918 rows) carry
delivery-note paperwork that physically accompanies goods. BLUEPRINT §5.6
historically mentioned „Generate Propratnica PDF" as an ad-hoc rendering;
D5 elevated it to a first-class polymorphic entity (3 DocumentTypes:
ProducerDispatch / ProducerReturn / CustomerShipment).

TASK
1. Entities (`src/LON.Domain/Entities/Logistics/DeliveryNote.cs` — new folder):
   - `DeliveryNoteType` enum: ProducerDispatch=1 | ProducerReturn=2 | CustomerShipment=3.
   - `DeliveryNote` (ITenantScoped + IAuditable + ISoftDeletable) per BLUEPRINT §3.8.
   - `DeliveryNoteLine` (ITenantScoped).
2. DbSet + IApplicationDbContext exposure.
3. EF configurations + tenant query filter + IsDeleted filter + unique
   constraint on (TenantId, Number).
4. EF migration: `phase-17.7.6_DeliveryNote` includes:
   - Schema creation.
   - SQL SEQUENCE `seq_DeliveryNote_<tenantId>` per BLUEPRINT §6.6 pattern.
5. NumberFormatter extended: `DeliveryNote(int year, long seq) → DN-{year}-{seq:D6}`.
6. Auto-gen: subscribe domain event handlers (foundation depends on E11
   domain-events infra; if E11 not yet shipped, use direct call in
   MaterialIssue/Shipment/FinishedGoodReceipt command handlers as bridge):
   - `MaterialIssueCommittedEvent` → create DeliveryNote(Type=ProducerDispatch,
     RelatedDocumentId=MaterialIssue.Id, Lines from ProductionOrderMaterial.IssuedQuantity).
   - `ShipmentCommittedEvent` with Type=ProducerReturn → create DeliveryNote(Type=ProducerReturn).
   - `ShipmentCommittedEvent` with Type=Export → create DeliveryNote(Type=CustomerShipment).
   - Status=Draft on creation; user reviews/confirms.
7. MediatR handlers in `src/LON.Application/Logistics/DeliveryNotes/`:
   - GetDeliveryNotesQuery (filter by type, dateRange, partnerId).
   - GetDeliveryNoteByIdQuery (includes lines).
   - UpdateDeliveryNoteCommand (driver, vehicle, remarks; only when Status=Draft).
   - ConfirmDeliveryNoteCommand (Status: Draft → Sent; sets ConfirmedAt/By; generates PDF).
   - CancelDeliveryNoteCommand (Status: Draft → Cancelled).
8. Controller endpoints under `/api/Logistics/delivery-notes`:
   - GET (list + by id), PUT (update Draft), POST /{id}/confirm, POST /{id}/cancel.
   - GET /{id}/pdf — generates standardized cover-sheet PDF (QuestPDF or similar).
9. UI:
   - new route `/warehouse/delivery-notes` — DataTable with filters; click row → detail.
   - DeliveryNote detail page: header + lines DataTable + confirm/cancel/download PDF buttons.
   - Toast notification on auto-creation: „Создаден Propratnica DN-YYYY-NNNNNN" with deep-link.
10. Integration tests (`DeliveryNoteTests.cs`):
    - Auto-gen on MaterialIssue commit creates ProducerDispatch DeliveryNote.
    - Auto-gen on Shipment Export creates CustomerShipment DeliveryNote.
    - Auto-gen on FinishedGoodReceipt creates ProducerReturn DeliveryNote.
    - Tenant isolation.
    - Status transitions (Draft→Sent→Confirmed; Cancel only from Draft).
    - PDF endpoint returns 200 + correct content-type.
11. Regenerate OpenAPI → TS schema.
12. Commit: `phase-17.7.6: DeliveryNote entity + polymorphic auto-gen + UI`
13. Deploy + smoke: trigger MaterialIssue via E7 flow → verify DeliveryNote appears in list.

Z2779 verification check (per PRE.7 fixture): the single Izdatnica in Z2779 should produce 1 DeliveryNote(Type=ProducerDispatch) after re-running migration; line count matches issued materials.

Before declaring done, walk through VERIFICATION.md §E7.6. SESSION_LOG evidence.
```

### E8 — Wire EX declaration + Shipment + QC from hub

```
Read BLUEPRINT.md §5.9, §5.10 and VERIFICATION.md §E8.

TASK
1. Hub action „Креирај извозна декларација" → wizard:
   Step 1: pick which FGs to export (DataTable of ClientOrderFinishedGood
   rows with available qty at HQ — non-zero only).
   Step 2: shipment details (consignee, country, transport,
   incoterm, scheduled date).
   Step 3: declaration metadata (DeclarationType=EX, related IM
   declarations auto-suggested from same ClientOrder).
   Step 4: review computed exit duties + pre-flight guarantee credit
   estimate. Show inline AI helper warning if discrepancies.
   Submit: POST /api/Customs/declarations (EX) + POST /api/WMS/shipments
   (atomic — Saga or single command).
2. Hub action „QC + Пакување" → list of FGs from HQ inventory with
   QualityStatus=Quarantine. Quick-action „Pass QC" sets OK; „Reject"
   prompts for reason + creates rework PO or waste declaration.
3. Commit: `phase-17.8: wire EX declaration + Shipment + QC from hub`
4. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E8. Paste evidence into SESSION_LOG.md.
```

### E8.5 — `CommercialInvoice` entity + wire from EX hub action

```
Read BLUEPRINT.md §3.2.1 (CommercialInvoice) + §5.10 (EX flow) + §9.1 (D4 mapping) + VERIFICATION.md §E8.5.

CONTEXT (D4 decision 2026-05-12)
Legacy ELON's `tblIzvozniFakturi` + `tblIzvozniFakturiStavki` (3,239 + 57,857 rows)
carry the commercial export invoice that accompanies each EX customs declaration.
This is distinct from sales `Invoice` (§5.14.2 — Teksport billing customer for
processing). CommercialInvoice = customs document showing trade value of FG at border.
Built as first-class v1 entity; finance integration deferred to Phase 27.

TASK
1. Entities (`src/LON.Domain/Entities/Customs/CommercialInvoice.cs`):
   - `CommercialInvoice` (ITenantScoped + IAuditable + ISoftDeletable) per BLUEPRINT §3.2.1.
   - `CommercialInvoiceLine` (ITenantScoped).
2. DbSet + IApplicationDbContext exposure.
3. EF configurations + tenant filter + soft-delete filter + unique
   constraint on (TenantId, Number).
4. EF migration: `phase-17.8.5_CommercialInvoice` includes:
   - Schema creation.
   - SQL SEQUENCE `seq_CommercialInvoice_<tenantId>`.
5. NumberFormatter extended: `CommercialInvoice(int year, long seq) → CI-{year}-{seq:D6}`.
6. MediatR handlers in `src/LON.Application/Customs/CommercialInvoices/`:
   - CreateCommercialInvoiceCommand (accepts ShipmentId; auto-suggests lines from
     Shipment lines via service `CommercialInvoiceSuggestionService.SuggestFromShipment`).
   - UpdateCommercialInvoiceCommand (lines edit, consignee/consignor/incoterms;
     only when Status=Draft).
   - IssueCommercialInvoiceCommand (Status: Draft → Issued; locks; generates PDF).
   - CancelCommercialInvoiceCommand (Status: Draft|Issued → Cancelled with reason).
   - GetCommercialInvoicesQuery (filter by clientOrderId, dateRange, status, consigneeId).
   - GetCommercialInvoiceByIdQuery (includes lines + linked Shipment/Declaration).
7. Controller endpoints under `/api/Customs/commercial-invoices`:
   - POST / PUT / DELETE (soft) / GET (list + by id).
   - POST /{id}/issue / POST /{id}/cancel.
   - GET /{id}/pdf — generates standardized export-invoice PDF.
   - POST /suggest-from-shipment?shipmentId={id} — returns line draft.
8. Hub action wiring: extend EX hub action (§E8) to optionally chain
   „Креирај commercial invoice" after EX submit (toast: „EX поднесен. Креирај commercial invoice сега?").
9. UI:
   - new route `/customs/commercial-invoices` — list page.
   - CommercialInvoice detail page: header + line DataTable + issue/cancel/download PDF buttons.
   - ClientOrder hub → new tab „Commercial invoices" listing CIs for this order.
   - EX CustomsDeclaration detail → „Commercial invoice" link card if exists, else „Create" button.
10. Integration tests (`CommercialInvoiceTests.cs`):
    - CRUD smoke + tenant isolation.
    - Numbering: 5 parallel creates → 5 distinct CI-YYYY-NNNNNN sequence values.
    - Suggest-from-shipment: shipment with 3 lines → returns 3 line drafts with correct quantities.
    - Status transitions (Draft → Issued; Issued → Cancelled; cannot edit lines after Issued).
    - PDF endpoint smoke.
11. Regenerate OpenAPI → TS schema.
12. Commit: `phase-17.8.5: CommercialInvoice entity + EX hub wiring + UI`
13. Deploy + smoke: create EX from hub → chain into commercial invoice → issue → download PDF.

Z2779 verification: Z2779 doesn't have a tblIzvozniFakturi correlation (single-cycle
inward-processing fully razdolzheno; no commercial export invoice raised). Phase 21
dry-run is when this entity gets meaningful migration data.

Finance integration (out of scope for §E8.5; tracked for Phase 27): margin
reconciliation per ClientOrder = (CommercialInvoice.TotalAmount) − (cost of
production) − (Invoice §5.14.2 to customer). Phase 27 adds the dashboard widget.

Before declaring done, walk through VERIFICATION.md §E8.5. SESSION_LOG evidence.
```

### E9 — Razdolzuvanje view per ClientOrder

```
Read BLUEPRINT.md §5.11 and VERIFICATION.md §E9.

TASK
1. Hub action „Razdolzuvanje" → opens /orders/:id/razdolzuvanje route.
2. Page renders RazdolzuvanjeReport: aggregates IM duty charged vs.
   EX/Waste/Return duty credited. Side-by-side columns. Variance row
   at bottom (must be < €0.50 tolerance).
3. Per CustomsDeclarationLine: „RazdolzenaDaNe" checkbox.
4. Buttons: Download PDF, Download PEE060 XML, Take Snapshot.
5. Snapshot button → POST /api/Guarantee/snapshots → creates
   GuaranteeBalanceSnapshot row + locks ClientOrder Status to Closed
   if all lines have RazdolzenaDaNe + balance reconciled.
6. Commit: `phase-17.9: Razdolzuvanje view per ClientOrder`
7. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E9. Paste evidence into SESSION_LOG.md.
```

### E.MIGRATE — LON.Migration refactor + Z2779 happy-path end-to-end (deferred from PRE.7)

```
Read docs/migration/PRE7_FINDINGS.md (especially §6 task list) + docs/migration/MAPPING.md + BLUEPRINT.md §9.1 + VERIFICATION.md §E.MIGRATE before starting.

CONTEXT
PRE.7 attempted to import Z2779 end-to-end but uncovered structural mismatches
between `src/LON.Migration/` and BLUEPRINT-correct mapping:
- AuthorizationMapper conflates `Zaklucok` with `LONAuthorization` (BLUEPRINT
  splits them: `Odobrenija → LONAuthorization`, `Zaklucok → ClientOrder`).
- DeclarationMapper expects `INW-PROC` (legacy abbreviation; now should be
  `4051/1041/6121/4200`).
- InventoryMapper doesn't honor DocumentSource resolver (MAPPING.md §11.1).
- 7 mappers missing: ClientOrder, BOM, FinishedGood, MaterialIssue,
  WasteDeclaration, DeliveryNote, CommercialInvoice.
- No `--zaklucok` filter.

This task runs AFTER §E1 (ClientOrder), §E5 (BOM wiring), §E7.6 (DeliveryNote),
§E8.5 (CommercialInvoice) have all landed so the v1 schema is complete.

TASK
1. Refactor `AuthorizationMapper` → split into:
   - `OdobrenijaMapper` (Odobrenija → LONAuthorization, 1:1 per BLUEPRINT §3.3).
   - `ClientOrderMapper` (Zaklucoci → ClientOrder per BLUEPRINT §3.1; FK to
     LONAuthorization via OdobrenieRBr resolution).
2. Refactor `DeclarationMapper`:
   - Resolve CustomsProcedure FK from `FakturiU5Z.VidUIS` (4051/1041/6121/4200).
   - Default to `4051` if VidUIS empty.
   - Add `ClientOrderId` FK assignment via composite Zaklucok lookup.
3. Refactor `InventoryMapper`:
   - Apply `ResolveExitDocument(Proces)` switch per MAPPING.md §11.1.
   - Emit `InventoryMovement` with correct `MovementType` + `RelatedDocumentId`.
   - Recompute `InventoryBalance` post-pass via FIFO replay (see MAPPING.md §4.1).
4. Add new mappers:
   - `BOMMapper` (Normativi → BOM + BOMLine + BOMLineWasteOverrides; dedupe per
     MAPPING.md §5.2).
   - `FinishedGoodMapper` (GotoviProizvodi → ClientOrderFinishedGood; ItemId via
     Item.Code lookup).
   - `MaterialIssueMapper` (Izdatnici → MaterialIssue; chain DeliveryNote auto-
     gen per BLUEPRINT §3.8 + §E7.6 wiring).
   - `WasteDeclarationMapper` (Ispratnici → WasteDeclaration per MAPPING.md §6.2).
   - `CommercialInvoiceMapper` (tblIzvozniFakturi → CommercialInvoice per §3.2.1;
     stub-mapping for now if not all columns identified — Z2779 has 0 rows so
     not exercised by happy-path).
   - `PartnerCatalogBuilder` (build Partner type=Producer catalog from union of
     numeric FK columns across LagerMaterijali, Izdatnici, Ispratnici per
     MAPPING.md §3.1 partner catalog build).
5. Add `--zaklucok <number>` CLI flag to `Program.cs`. Every mapper filters
   `WHERE ZaklucokBroj=@zb` (and joins as needed).
6. Refresh `ReconciliationReporter`:
   - R1 InventoryBalance/Movement count by MovementType vs LagerMaterijali by Proces.
   - R2 GuaranteeAccount.CurrentBalance per LONAuthorization vs Odobrenija.GarancijaIznos.
   - R3 Declaration totals (10 random spot-checks; tolerance EUR ±0.01).
   - R4 ClientOrder count vs Zaklucoci (non-staging) — exact.
   - R5 BOMLine count vs Normativi (LON ≤ legacy after dedupe; log collapsed).
   - R6 Re-aggregate CustomsDeclarationLine grouped by (TariffCodeId, UoMId, CountryOfOrigin) → SUM-match legacy NaimU5 (tolerance EUR ±0.01).
7. Run `dotnet run --project src/LON.Migration -- all --legacy ELON --lon LONDB --tenant TEKSPORT --zaklucok 2779`.
8. Assertions (Z2779-specific):
   - LONAuthorization count = 1 (OdobrenieRBr=1).
   - ClientOrder count = 1 (Z2779 itself).
   - CustomsDeclaration count = 1 (IM).
   - CustomsDeclarationLine count = 13.
   - InventoryMovement count: 13 Receipt (Proces=1) + 5 IssueToProducer (Proces=7) + 3 WasteDestroyed (Proces=9) ≈ 21 rows.
   - BOM count = 1; BOMLine count = 5.
   - MaterialIssue count = 1 (Izdatnica 8232/2025); DeliveryNote(ProducerDispatch) count = 1.
   - WasteDeclaration count = 3 (Ispratnici for 3 waste rows).
   - CommercialInvoice count = 0 (Z2779 has no tblIzvozniFakturi rows).
   - All 6 reconciliation queries pass within tolerance.
9. Document timing (Z2779 should complete in <30s; future --zaklucok runs benchmark vs this).
10. Commit: `phase-17.E.MIGRATE: LON.Migration refactor + Z2779 happy-path end-to-end + 6 reconciliation queries passing`
11. Deploy to VPS (optional for this task — local validation is the primary goal; VPS migration is Phase 21.1 dry-run for ALL 269 Zaklucoci).

Before declaring done, walk through VERIFICATION.md §E.MIGRATE. SESSION_LOG evidence.
```

### E10 — AI helper service + 3 core recommendations + floating UI

```
Read BLUEPRINT.md §7.4 + §6.11 (RAG) and VERIFICATION.md §E10.

CONTEXT
KnowledgeBase RAG endpoints exist but never surfaced to business users.
This task connects RAG + structured DB queries → contextual recommendations.

TASK
1. New service LON.Application.Ai.AiAssistantService with method
   GetRecommendations(string entityType, Guid entityId, string? mode).
   Returns List<Recommendation> { title, body, confidence, actionLink?,
   structuredData? }.
2. Endpoint POST /api/Ai/recommendations with body
   { entityType, entityId, mode? }.
3. Implement 3 core recommendations:
   a. ClientOrder hub: detect blocked next step.
      Logic (NOT LLM-call; structured query):
      - Status=Draft AND no FGs → „Внеси готови производи".
      - Status=Active AND no IM declarations approved → „Креирај IM".
      - Receipt qty > 0 AND not distributed → „Распредели подизведувач".
      - Production complete AND no EX → „Креирај извоз".
      - Sum unbilled EX duty > 0 AND no Razdolzuvanje snapshot → „Razdolzuvanje".
   b. Receipt creation: variance flag.
      Logic: compute variance% from declaration line vs received;
      if >5%, return recommendation „Просечен variance од овој снабдувач
      е X%; вашиот Y% — провери packaging".
      Use AVG over last 10 receipts from same Partner.
   c. Razdolzuvanje pre-flight: reconciliation check.
      Logic: enumerate IM lines without matching EX consumption; if
      any, return list as recommendation „Има N линии IM без EX
      consumption — провери".
4. Floating button component <AiHelperButton /> bottom-right on every
   route. Click → side drawer with 2 tabs: „Препораки" (calls
   POST /api/Ai/recommendations with current page context auto-detected)
   and „Прашај" (chat box, calls /api/KnowledgeBase/ask — existing RAG).
5. AiHelperButton receives context via React Context that each page
   sets on mount: { entityType, entityId }.
6. Recommendations should render with action buttons that navigate
   to the relevant flow (e.g. „Внеси готови производи" button =
   open E5 dialog).
7. Audit: every recommendation generated is logged to AiSuggestionLog
   table (id, tenantId, entityType, entityId, recommendationTitle,
   userActedOn bool, generatedAt). User-dismiss = userActedOn=false;
   user-clicked-action = userActedOn=true.
8. Integration tests for each of the 3 recommendation logics.
9. Commit: `phase-17.10: AI helper service + 3 core recommendations + UI`
10. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E10. Paste evidence into SESSION_LOG.md.
```

### E11 — Domain events + handler refactor

```
Read BLUEPRINT.md §3.6 + §6.1 (guarantee lifecycle) + §6.2 (inventory state machine) and VERIFICATION.md §E11.

CONTEXT
Currently handlers directly write to GuaranteeAccount, InventoryBalance,
etc. inline. Move to domain-event pattern: aggregate root emits event,
handler in App layer consumes. This isolates side-effects + enables
event-replay + audit + AI helper triggers.

TASK
1. LON.Domain.Common.IDomainEvent interface with `DateTime OccurredAt`.
2. Concrete events:
   - ClientOrderCreatedEvent, ClientOrderStatusChangedEvent
   - CustomsDeclarationCreatedEvent, CustomsDeclarationApprovedEvent
   - ReceiptCommittedEvent
   - MaterialIssueCommittedEvent
   - ProductionReceiptCommittedEvent
   - PodelbaCommittedEvent
   - ShipmentCommittedEvent
   - GuaranteeThresholdReachedEvent
3. Each aggregate root has `private readonly List<IDomainEvent> _events`
   and `public IReadOnlyList<IDomainEvent> Events => _events`. Methods
   that change state call `_events.Add(new XEvent(...))`.
4. After SaveChangesAsync in ApplicationDbContext, dispatch events
   via MediatR.Publish (or custom dispatcher) and clear list.
5. DomainEventLog table (append-only) for audit + replay.
6. Refactor existing handlers:
   - CustomsDeclarationApproved → handler creates GuaranteeLedgerEntry
     (Debit). Remove inline GuaranteeAccount update from
     ApproveDeclarationCommand handler.
   - ReceiptCommitted → handler updates InventoryBalance state.
   - ShipmentCommitted → handler computes EX guarantee credit pro-rata.
   - ProductionReceiptCommitted → handler decrements materials,
     emits LonProcessState transitions.
7. Integration tests must still pass (use BeforeAll seed; assert post-event state).
8. Add tests for event dispatch order + idempotency.
9. Commit: `phase-17.11: domain events infrastructure + handler refactor`
10. Deploy + run full integration test suite + smoke on VPS.

Before declaring done, walk through VERIFICATION.md Section E11. Paste evidence into SESSION_LOG.md.
```

### E12 — SQL SEQUENCE objects + NumberFormatter

```
Read BLUEPRINT.md §6.6 and VERIFICATION.md §E12.

TASK
1. EF migration `phase-17.12_NumberSequences`:
   - For each numbered entity (ClientOrder, IM Declaration, EX Declaration,
     Receipt, MaterialIssue, Shipment, ProductionOrder, GuaranteeLedger),
     create SQL SEQUENCE per tenant (initially Teksport only).
   - Set initial value based on MAX existing in that table for tenant
     (so new numbers continue from where DMax left off).
2. NumberFormatter helper (started in E1.10) extended to cover all
   entity types.
3. All handlers that previously did `DMax+1` (or any in-memory MAX
   logic) refactored to:
   `var seq = await ctx.Database.ExecuteSqlRawAsync(
     "SELECT NEXT VALUE FOR seq_<table>_<tenantId>");`
   Or via stored proc / wrapper service.
4. Concurrency test: 10 parallel CreateClientOrder calls → 10 unique
   sequential OrderNumbers (no duplicates, no gaps that aren't from
   rolled-back transactions).
5. Commit: `phase-17.12: SQL SEQUENCE for all numbered entities`
6. Deploy + run integration tests.

Before declaring done, walk through VERIFICATION.md Section E12. Paste evidence into SESSION_LOG.md.
```

### E13 — Audit interceptor + AuditLogEntry writes + /admin/audit-log UI

```
Read BLUEPRINT.md §3.7 + §6.5 and VERIFICATION.md §E13.

TASK
1. EF SaveChangesInterceptor in LON.Infrastructure/Persistence/Interceptors/
   AuditInterceptor.cs:
   - For every Modified IAuditable entity: capture pre + post property
     values for tracked fields.
   - Insert AuditLogEntry row with EntityType, EntityId, Action="Update",
     ChangedFields=JSON array, Actor=currentUserId from IUserContext,
     OccurredAt, Reason=null (or from special API header).
   - For Added: Action="Create", ChangedFields=full snapshot.
   - For Deleted (soft via IsDeleted=true): Action="SoftDelete".
   - For hard Deleted (admin-only): Action="HardDelete".
2. Register interceptor in DI.
3. Filter AuditLog entity types to per BLUEPRINT §3.7 list (avoid noise
   from every Item edit).
4. UI: activate /admin/audit-log page:
   - DataTable: EntityType, EntityId (link to that entity's detail),
     Action, Actor, OccurredAt, ChangedFields (expandable).
   - Filters: entityType, actor, dateRange, action.
   - Per-entity „Audit history" tab on hub + detail pages (last 20
     entries; link to full history).
5. Permission: only Administrator + Manager can access full /admin/audit-log;
   regular users see only audit on entities they own.
6. Integration tests: modify entity → assert AuditLogEntry row present
   with correct ChangedFields JSON.
7. Commit: `phase-17.13: audit interceptor + AuditLogEntry + UI`
8. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E13. Paste evidence into SESSION_LOG.md.
```

### E14 — Soft-delete global filter + recycle bin UI

```
Read BLUEPRINT.md §6.7 and VERIFICATION.md §E14.

TASK
1. ISoftDeletable interface (IsDeleted, DeletedAt, DeletedBy).
2. Apply interface to entities listed in BLUEPRINT §3.7.
3. EF migration: add IsDeleted (bool, default false, indexed) + DeletedAt +
   DeletedBy columns.
4. Global query filter on ApplicationDbContext: `WHERE !IsDeleted` for
   ISoftDeletable; expose IgnoreSoftDelete() extension.
5. Cascade rules: soft-delete ClientOrder → cascade soft-delete linked
   CustomsDeclarations, ProductionOrders, Shipments (with audit log per).
6. UI: /admin/recycle-bin page with tabs per entity type, each showing
   soft-deleted records. „Restore" action sets IsDeleted=false (with audit).
   „Permanent delete" admin-only action (with confirmation).
7. Retention job in LON.Worker: weekly, hard-delete records with
   DeletedAt > 90 days old.
8. Integration tests for soft-delete + restore + retention.
9. Commit: `phase-17.14: soft-delete global filter + recycle bin`
10. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section E14. Paste evidence into SESSION_LOG.md.
```

### E7.5 — Department + Position lookup promotion

```
Read BLUEPRINT.md §5.12.1 + VERIFICATION.md §E7.5.

PREREQUISITE / DATA SOURCE NOTE (2026-05-12 PREP recon + D6 decision):
Local ELON DB slice has NO employee table (`tblKorisnikTEKSPORT` absent — see §9.1).
**D6 decided 2026-05-12: prod-export at Phase 21 cutover.**

This task is therefore **DEFERRED to Phase 21.1.1** (a new sub-task after
prod ELON export arrives carrying real `tblKorisnikTEKSPORT` rows with
Department/Position string values).

Phase 17 placeholder: backend handlers + schema (DepartmentId/PositionId
columns + CodeListItem categories) can land in Phase 17 §E7.5 (this task) but
backfill query runs against the prod-export staging DB at Phase 21, not against
local. If you run §E7.5 in Phase 17:
- Add the migration (schema + 2 categories) but with 0 seed rows.
- UI inline „+Add new" creates first entries for net-new employees.
- Phase 21.1.1 backfill query: SELECT DISTINCT Department FROM prod-staging
  Employees → INSERT CodeListItem; UPDATE Employee.DepartmentId by string match.

Alternative: leave the entire task to Phase 21 as a single bundle. Recommended for
schedule predictability. If chosen, mark §E7.5 here as „deferred to 21.1.1" and
do NOT execute in Phase 17.

CONTEXT
Employee entity has `Department` and `Position` as free-text `string?` fields.
This causes inconsistency (typos, no list-of-values, harder reporting).
Reuse existing `CodeListItem` entity with categories.

TASK
1. Add 2 CodeListItem categories: `EmployeeDepartment` and `EmployeePosition`.
   No new entity needed — CodeListItem already supports categorized lookups.
2. Migration `phase-17.7.5_DeptPosition_AsLookups`:
   a. Add nullable Guid columns `Employee.DepartmentId` (FK CodeListItem) and
      `Employee.PositionId` (FK CodeListItem).
   b. Backfill: SELECT DISTINCT Department FROM Employees → insert as
      CodeListItem rows in category 'EmployeeDepartment'; same for Position.
      For each Employee row: set DepartmentId/PositionId to matching CodeListItem.
      Special-case NULL/empty Department/Position values (leave null).
   c. Keep the old `Department` and `Position` string columns for 1 release
      (deprecation period); mark in code via [Obsolete]. Final cleanup in Phase 18.
3. UI update:
   - EmployeeForm: replace Department + Position text inputs with autocomplete-
     style dropdowns reading CodeListItems by category.
   - Inline „+ Add new" option opens lightweight create dialog (creates new
     CodeListItem on the fly with proper category).
4. Admin: `/master-data/code-lists` (постоен) gets two new categories visible
   in dropdown filter.
5. Integration tests:
   - Migration backfill produces expected count of distinct values.
   - New Employee with DepartmentId/PositionId saves correctly.
   - Old `Department`/`Position` strings remain populated during deprecation.
6. tsc + eslint + dotnet test green.
7. Commit: `phase-17.7.5: Department + Position promoted to CodeListItem lookups`
8. Deploy + smoke: open EmployeeForm, verify dropdown loads existing values + autocomplete works.

Before declaring done, walk through VERIFICATION.md §E7.5. SESSION_LOG evidence.
```

### E10.5 — AlertRule + AlertEvent + 6 predefined v1 rules + nightly evaluator

```
Read BLUEPRINT.md §5.13.4 + VERIFICATION.md §E10.5.

CONTEXT
Management dashboard (§5.13.1) shows „Open alerts" card. Backing entities
+ evaluator do not exist. Phase 26 will add UI editor for rule definition;
Phase 17 provides the foundation + predefined v1 rules.

TASK
1. Entities (src/LON.Domain/Entities/Management/AlertRule.cs):
   - AlertRule { Id, TenantId, Code (unique), Name (mk+en via labels?),
     Severity (Low|Medium|High|Critical), IsActive, TriggerKind (enum: one
     of the 6 predefined v1 rules), Threshold (decimal?), Recipients
     (JSON role list), DeliveryChannels (flags: Dashboard, Email — v1 only
     Dashboard active), CreatedAt, CreatedBy }.
   - AlertEvent { Id, TenantId, AlertRuleId, OccurredAt, EntityType,
     EntityId, Severity, Title, Body, AcknowledgedBy, AcknowledgedAt,
     ResolvedAt }.
2. DbSet exposures + EF configurations + tenant filter + soft-delete.
3. Migration `phase-17.10.5_AlertRulesAndEvents` includes:
   - Schema creation.
   - SEED 6 predefined rules for current Teksport tenant (and template for
     future tenants — handle via tenant-provisioning seed):
     a. GuaranteeUtilization > 90% (severity=High, eval daily)
     b. ClientOrder due in <7 days with <50% produced (High, eval hourly)
     c. Machine down >2 hours (Medium, eval every 15min)
     d. Certification expiring in <30 days (Medium, eval daily)
     e. Receipt variance >5% on single event (Medium, eval on event)
     f. Subcontractor late on PO milestone (High, eval daily — milestone
        defined as 50% of planned date)
4. Background worker LON.Worker.AlertEvaluatorJob:
   - Implements IHostedService running every 5 min.
   - For each Active AlertRule, evaluates per its TriggerKind:
     - Queries DB.
     - For each new condition match (not already an unresolved AlertEvent
       for same entity), inserts AlertEvent row.
   - Optional: dispatches DomainEvent `AlertRaisedEvent` (for AI helper +
     future email integration in Phase 26).
5. Endpoints under /api/Management/alerts (extend existing route):
   - GET /alerts?status=Open|Acknowledged|Resolved (paginated)
   - POST /alerts/{id}/acknowledge { reason? }
   - POST /alerts/{id}/resolve { reason? }
6. UI: existing `/management/alerts` (currently localStorage per Phase 16
   audit) — rewire to backend. List with severity badges, acknowledge
   button, drill-down to entity.
7. Dashboard card „Open alerts" (§5.13.1 card 7): count badge, click → list.
8. Integration tests:
   - Each of 6 rules: seed condition → run evaluator → AlertEvent created.
   - Duplicate suppression: re-run evaluator → no new AlertEvent if existing
     unresolved one matches.
   - Acknowledge + resolve flows + audit.
9. tsc + eslint + dotnet test green.
10. Commit: `phase-17.10.5: AlertRule + AlertEvent + 6 predefined rules + nightly evaluator`
11. Deploy. Wait for worker to run once (10 min); verify AlertEvent rows in DB.

Before declaring done, walk through VERIFICATION.md §E10.5. SESSION_LOG evidence.
```

### E16 — FxRate entity + manual maintenance UI

```
Read BLUEPRINT.md §5.14.8 + §5.14.10 + VERIFICATION.md §E16.

CONTEXT
Currency conversion is needed for: CustomsDeclarationLine valuation,
Invoice currency totals, Margin reports (aggregate to Tenant.PrimaryCurrency).
v1 has no FxRate entity — values currently use hard-coded 1.0 or per-line
rate fields. Manual maintenance в v1; auto-import is Phase 27.1.

TASK
1. Entity src/LON.Domain/Entities/Finance/FxRate.cs:
   - TenantId, FromCurrency (3-char ISO), ToCurrency (3-char ISO), Rate
     (decimal 18,8), EffectiveDate (date — UTC), Source (enum:
     Manual | NationalBank), CreatedBy, CreatedAt, IAuditable.
   - Unique constraint: (TenantId, FromCurrency, ToCurrency, EffectiveDate).
2. DbSet + configuration + tenant filter.
3. Migration `phase-17.X1_FxRate` includes schema + seed for current
   well-known currencies in Teksport: EUR/MKD, USD/MKD, USD/EUR (from
   today's central-bank rates — placeholder; user updates after deploy).
4. Service src/LON.Application/Finance/FxRateService.cs:
   - `Task<decimal> GetRate(string from, string to, DateTime asOf)`.
   - Returns rate effective <= asOf (latest matching row).
   - If from == to → return 1.0.
   - If exact pair not found, try inverse (1 / rate) or cross via EUR.
   - If still not found → throws FxRateMissingException.
5. Handlers in src/LON.Application/Finance/FxRates/:
   - CreateFxRateCommand
   - UpdateFxRateCommand
   - DeleteFxRateCommand
   - GetFxRatesQuery (filter by currency pair + date range)
   - GetEffectiveRateQuery (single point-in-time lookup)
6. Endpoints under /api/Finance/fx-rates: POST/PUT/DELETE/GET (list + by id + effective).
7. UI new route `/finance/fx-rates`:
   - DataTable with currency pair, date, rate, source.
   - Add/edit form.
   - Filters by currency pair + date range.
   - Quick-action „Copy rate forward to today" for prior row.
8. Integration tests:
   - CRUD smoke.
   - GetRate returns latest effective <= asOf.
   - Cross-rate fallback through EUR.
9. UI wire: where CustomsDeclarationLine, Invoice, MarginReport use currency,
   call FxRateService.GetRate at point of valuation (no schema change to
   those entities; computed via service).
10. tsc + eslint + dotnet test green.
11. Commit: `phase-17.X1: FxRate entity + maintenance UI + service`
12. Deploy + smoke: create FX rate via UI, verify Invoice generated for
    foreign-currency ClientOrder uses correct rate for margin computation.

Before declaring done, walk through VERIFICATION.md §E16. SESSION_LOG evidence.
```

### E15 — Phase 17 E2E Playwright happy-path test

```
Read BLUEPRINT.md §1.3 (v1 acceptance criterion) + §8.5 (testing) and VERIFICATION.md §E15 + §J.

CONTEXT
End of Phase 17. The v1 acceptance loop must be executable end-to-end via
Playwright as proof. Required before declaring Phase 17 complete.

TASK
1. Install Playwright (if not already):
   cd tests && dotnet new tool-manifest && dotnet tool install
     Microsoft.Playwright.CLI (or use Playwright-Test npm equivalent).
   Choose: TypeScript-based Playwright (tests/playwright/) NOT C# binding
   (faster iteration; can call back into API for setup).
2. Project structure:
   tests/playwright/
     ├── playwright.config.ts
     ├── package.json
     ├── tests/
     │   ├── happy-path.spec.ts  ← the v1 loop
     │   └── setup/
     │       ├── auth.ts          ← login helpers per role
     │       └── seeds.ts         ← create test tenant + base data via API
3. happy-path.spec.ts implementation: scripted user flow (see VERIFICATION
   §E15 for explicit steps).
4. Test data: per-test isolated tenant (`Tenant-Playwright-{nanoid}`)
   seeded via API; teardown deletes tenant after test.
5. Run locally: `npx playwright test`. Expected: green (assuming Phase
   17.1-14 all complete).
6. CI integration: GitHub Action runs Playwright on PR + nightly.
7. Screenshots + video on failure (Playwright defaults).
8. Commit: `phase-17.15: E2E Playwright happy-path covering v1 loop`
9. No VPS deploy required (test code only). However, run the test against
   VPS (env BASE_URL=https://elon.elbosoft.click) to confirm it works
   end-to-end on production-like environment.

Before declaring done, walk through VERIFICATION.md Section E15. Paste evidence into SESSION_LOG.md.
```

---

## §F — Phase 18: Subcontractor login

### F1 — Subcontractor role + JWT claim extension

```
Read BLUEPRINT.md §4.3 + §8.2 and VERIFICATION.md §F1.

TASK
1. Seed role „Subcontractor" in RolePermissionTests fixture and
   Migration's seed data.
2. JWT generation: include `external_partner_id` claim when user
   has Subcontractor role (must be set on user creation; new column
   on User table or new UserExternalLink entity if multi-relationship).
3. Decision (Q11.1 of BLUEPRINT — likely answer here):
   - Approach A: User.ExternalPartnerId nullable Guid.
   - Approach B: UserExternalPartnerLink (m:n) for users working with
     multiple producers across tenants.
   - For v1: A (simplest). Document upgrade path to B post-v1.
4. Migration + handler updates for User entity.
5. Integration test: subcontractor user logs in → JWT contains claim.
6. Commit: `phase-18.1: subcontractor role + external_partner_id claim`
7. Deploy.

Before declaring done, walk through VERIFICATION.md Section F1. Paste evidence into SESSION_LOG.md.
```

### F2 — Server-side filter for subcontractor queries

```
Read BLUEPRINT.md §4.3 and VERIFICATION.md §F2.

TASK
1. Create ICurrentUserService extension method
   `Guid? GetExternalPartnerId()` reading from JWT.
2. Add to relevant queries (ProductionOrder, MaterialIssue, Inventory,
   ProductionReceipt) a `.Where(po => po.ProducerPartnerId ==
   _currentUser.GetExternalPartnerId())` when caller is Subcontractor.
3. Endpoints inaccessible to Subcontractor (Customs, Finance, MasterData
   write, Admin) → enforce via `[HasPermission(...)]` attribute check.
4. Integration test: subcontractor calls list endpoints → returns only
   their data. Subcontractor calls forbidden endpoint → 403.
5. Commit: `phase-18.2: server-side filter + RBAC enforcement for subcontractor`
6. Deploy.

Before declaring done, walk through VERIFICATION.md Section F2. Paste evidence into SESSION_LOG.md.
```

### F3 — Subcontractor dashboard UI

```
Read BLUEPRINT.md §4.3 + §7.1 and VERIFICATION.md §F3.

TASK
1. Conditional dashboard rendering: if user.role includes Subcontractor
   AND no other higher-priority role, redirect / → /producer/dashboard.
2. /producer/dashboard page: shows tabs „Active POs", „Materials on
   hand", „Pending issues", „History". Read-only DataTables.
3. Per-PO detail page /producer/orders/:id: shows materials issued,
   produced qty (entry form for ProductionReceipt), QC results.
4. No access to ClientOrder hub, customs, finance, or master data.
5. Playwright E2E: login as subcontractor, navigate, verify visible
   pages + forbidden URLs return 403/redirect.
6. Commit: `phase-18.3: subcontractor dashboard UI`
7. Deploy + smoke.

Before declaring done, walk through VERIFICATION.md Section F3. Paste evidence into SESSION_LOG.md.
```

### F4 — RLS extension for subcontractor (after Phase 20)

```
(BLOCKED until Phase 20 RLS deployment.)

TASK (run after Phase 20.1-20.2 complete)
1. Extend RLS predicate to allow rows where
   `ProducerPartnerId = SESSION_CONTEXT('ExternalPartnerId')` OR
   `TenantId = SESSION_CONTEXT('TenantId')`.
2. Middleware sets SESSION_CONTEXT('ExternalPartnerId') from JWT.
3. Pen test: tampered subcontractor JWT (changed external_partner_id)
   returns 0 rows.
4. Commit: `phase-18.4: RLS extension for subcontractor isolation`

Before declaring done, walk through VERIFICATION.md Section F4.
```

### F5 — Phase 18 E2E Playwright

```
TASK
Extend tests/playwright/tests/ with subcontractor-isolation.spec.ts.
Steps:
1. Setup: create 2 producers under same tenant; 1 ClientOrder uses
   Producer A; another uses Producer B.
2. Login as Producer A subcontractor → assert Producer A's POs visible,
   Producer B's not.
3. Try GET /api/Production/orders/{producerB-poId} directly with A's
   JWT → expect 403.
Commit: `phase-18.5: subcontractor E2E isolation tests`.
```

---

## §G — Phase 19: Speditor role + export polish

### G1 — Speditor role + SpeditorExportProfile entity

```
Read BLUEPRINT.md §5.5, §4.3 and VERIFICATION.md §G1.

TASK
1. Seed Speditor role.
2. SpeditorExportProfile entity: TenantId, SpeditorPartnerId, Name,
   FileFormat (Excel|CSV|XML), ColumnMapping (JSON), Active.
3. CRUD endpoints under /api/MasterData/speditor-profiles.
4. UI for admin: /admin/speditor-profiles.
5. Integration tests.
6. Commit: `phase-19.1: speditor role + export profile entity`
```

### G2 — Speditor login + shipment-detail view

```
TASK
1. JWT external_partner_id for Speditor role (same pattern as F1).
2. /speditor/dashboard route shows assigned shipments.
3. Per-shipment detail: download Izpratnica PDF + EX declaration
   documents.
4. RBAC: Speditor can ONLY GET shipments where SpeditorPartnerId
   matches their external_partner_id.
5. Commit: `phase-19.2: speditor dashboard + shipment view`
```

### G3 — Auto-email on shipment ready (optional v1)

```
TASK
1. Subscribe ShipmentReadyEvent handler to send email via SmtpClient
   (config: AppSettings.Email.{Host,From,...}).
2. Email template: shipment number, scheduled date, attached
   documents (PDF + XML).
3. Configurable per speditor: SpeditorExportProfile.AutoEmailEnabled.
4. Integration test: stub IEmailService; assert called on event.
5. Commit: `phase-19.3: auto-email on shipment ready`
```

### G4 — Phase 19 E2E

```
Playwright test: login as speditor, see only assigned shipment,
download documents, verify file presence.
Commit: `phase-19.4: speditor E2E tests`
```

---

## §H — Phase 20: RLS + tenant security audit

### H1 — RLS predicate function + policy creation

```
Read BLUEPRINT.md §6.9 and VERIFICATION.md §H1.

TASK
1. EF migration `phase-20.1_RLS`:
   - CREATE FUNCTION dbo.fn_TenantPredicate (per BLUEPRINT §6.9).
   - CREATE SECURITY POLICY TenantIsolationPolicy applying predicate
     to every ITenantScoped table.
   - Use raw SQL since EF Core doesn't model RLS natively.
2. Verify: SQL-level test (not via API) — run
   `EXEC sp_set_session_context 'TenantId', '<other-tenant-guid>';
    SELECT * FROM ClientOrders;`
   Expected: 0 rows even if rows exist for the actual current tenant.
3. Commit: `phase-20.1: RLS policy applied to all tenant-scoped tables`
```

### H2 — Middleware: SESSION_CONTEXT per request

```
TASK
1. ASP.NET Core middleware: on every request, after JWT validation,
   open SQL connection scope, execute
   `EXEC sp_set_session_context 'TenantId', @tenantId;`
   and `EXEC sp_set_session_context 'IsSystemAdmin', @isSysAdmin;`.
2. Apply on each EF Core connection (interceptor on
   DbConnection.OpenAsync).
3. Performance: measure overhead on 100 mixed requests; document.
4. Commit: `phase-20.2: SESSION_CONTEXT middleware`
```

### H3 — Pen test

```
TASK
1. Manual test scenarios:
   - Tampered JWT with another tenant_id → API returns own data only.
     Expected: 0 rows or 403.
   - Forge `IgnoreQueryFilters` in code (developer mistake simulation)
     and confirm RLS still blocks at DB.
   - Direct SQL with stolen connection string → must require
     SESSION_CONTEXT or default to 0 rows.
2. Document scenarios + results in docs/security/PHASE20_PENTEST.md.
3. Commit: `phase-20.3: pen test report`
```

### H4 — Security audit doc

```
TASK
1. docs/security/PHASE20_AUDIT.md with sections:
   - Auth flow (login + refresh)
   - RBAC enforcement (controllers + service-layer checks)
   - Tenant isolation (RLS + EF filter defense-in-depth)
   - Audit trail integrity
   - Password storage (BCrypt, no plain-text)
   - JWT secret management
   - SQL injection surface (any raw SQL audited)
   - Backup security (off-VPS encrypted storage)
2. Sign-off section: requires user (Bobby) review.
3. Commit: `phase-20.4: security audit document`
```

### H5 — Backup automation + restore drill

```
TASK
1. Cron job (VPS):
   ```bash
   0 2 * * * /opt/apps/LON/scripts/backup-daily.sh
   ```
   Script: `sqlcmd -S ... -Q "BACKUP DATABASE Teksport TO DISK..."`
   then `scp` to off-VPS storage (configure SSH key + remote path).
2. Retention: 30 days rolling on remote; old archives auto-deleted.
3. Restore drill: monthly, restore to staging container, run
   reconciliation queries (RecordCount per Proces, etc.) — assert
   match within 0.01%.
4. First drill: document in SESSION_LOG.
5. Commit: `phase-20.5: backup automation + restore drill`
```

---

## §I — Phase 21: Migration + launch

### I1 — ELON migration dry-run + reconciliation

```
Read BLUEPRINT.md §9.1 + VERIFICATION.md §I1.

TASK
1. Set up staging environment: clean LON Teksport DB + read-only mirror
   of ELON DB.
2. Run LON.Migration end-to-end. Collect errors.
3. Reconciliation queries (per BLUEPRINT §9.1):
   a. Count by Proces in ELON LagerMaterijali vs InventoryBalance →
      must match within 0.01%.
   b. SUM(GarancijaIznos) per Odobrenie vs SUM in LON → match exact.
   c. SUM(Vrednost), SUM(Davacki), SUM(Carina) per declaration → 10
      random spot-checks.
   d. Count Zaklucoci vs ClientOrders.
   e. Count Normativi vs BOMLines.
   f. Count Ispratnici vs Shipments.
4. For each discrepancy → fix LON.Migration code + re-run.
5. Iterate until all reconciliation passes.
6. Document final timing (how long the full migration takes).
7. Commit: `phase-21.1: ELON migration dry-run reconciled`
```

### I2 — Cutover plan document

```
TASK
1. docs/launch/PHASE21_CUTOVER_PLAN.md:
   - T-7 days: final dry-run, sign-off
   - T-1 day: freeze ELON (read-only)
   - T-0 morning: cutover (4-8h)
   - T-0 afternoon: UAT 20 random orders
   - T-0 evening: go-live decision
2. Include: rollback procedure (if go-live fails).
3. Include: communication template to Teksport staff.
4. User review + sign-off documented in SESSION_LOG.
5. Commit: `phase-21.2: cutover plan`
```

### I3 — USER_MANUAL.md refresh

```
TASK
1. Rewrite docs/USER_MANUAL.md to reflect ClientOrder hub flow
   (BLUEPRINT §5 walk-through, with screenshots).
2. Localized: MK primary, EN translation.
3. Distribute: print + PDF for Teksport staff.
4. Commit: `phase-21.3: USER_MANUAL refresh`
```

### I4 — Final v1 acceptance E2E sweep

```
TASK
1. Run all Playwright E2E tests on VPS production environment:
   - Happy path (E15)
   - Subcontractor isolation (F5)
   - Speditor (G4)
   - Tenant isolation under RLS (H3 scenarios as E2E)
2. All green required for launch sign-off.
3. Document results in SESSION_LOG.
4. Commit: `phase-21.4: final v1 acceptance E2E sweep`
```

### I5 — Go-live

```
TASK
1. Execute cutover plan on agreed date.
2. Live monitoring: dashboards + error log tails for 48h post-launch.
3. Daily check-ins for 2 weeks.
4. Document any post-launch issues + resolutions in SESSION_LOG.
5. Phase 22+ post-v1 backlog formalized.
6. Commit: `phase-21.5: v1 GO-LIVE 🚀`
```

---

## §J — Playwright E2E patterns (used across phases)

> Reference patterns for E2E tests. Re-use across E15, F5, G4, H3, I4.

### J1 — Project setup (once)

```bash
cd tests/playwright
npm install -D @playwright/test
npx playwright install chromium
```

### J2 — Auth helper

```typescript
// tests/playwright/tests/setup/auth.ts
import { Page, APIRequestContext } from '@playwright/test';

export async function loginAs(page: Page, request: APIRequestContext, role: string, tenantId?: string) {
  // POST /api/Auth/login with seeded test user for role
  const resp = await request.post(`${process.env.API_URL}/api/Auth/login`, {
    data: { username: `test-${role}@playwright.local`, password: 'TestPass123!' }
  });
  const { token } = await resp.json();
  await page.context().addCookies([{
    name: 'auth_token', value: token, url: process.env.BASE_URL!
  }]);
  // Or: window.localStorage.setItem('token', token) before navigation
}
```

### J3 — Tenant seed via API

```typescript
// Setup helper: provision an isolated tenant for the test run
export async function seedTestTenant(request: APIRequestContext): Promise<TestTenant> {
  // Call admin endpoint to provision tenant + roles + users + base data
  // Returns tenant ID, login credentials for each role, IDs of seeded
  // Item/Partner/Authorization for use in tests.
}
```

### J4 — Pattern: happy-path test

```typescript
// tests/playwright/tests/happy-path.spec.ts
test('v1 acceptance loop: ClientOrder → IM → Receipt → BOM → Podelba → Issue → Receipt → QC → EX → Razdolzuvanje', async ({ page, request }) => {
  const tenant = await seedTestTenant(request);
  await loginAs(page, request, 'Manager', tenant.id);

  // Step 1: Create ClientOrder
  await page.goto('/orders');
  await page.getByRole('button', { name: 'Нов налог' }).click();
  await page.getByLabel('Клиент').click();
  await page.getByRole('option', { name: tenant.customerPartner.name }).click();
  // ... fill remaining fields
  await page.getByRole('button', { name: 'Зачувај' }).click();

  // Hub opens
  await expect(page).toHaveURL(/\/orders\/[a-f0-9-]+$/);
  await expect(page.getByText('Status: Draft')).toBeVisible();

  // Step 2: Click „Креирај увозна декларација" action
  await page.getByRole('button', { name: 'Креирај увозна декларација' }).click();
  // ... fill IM declaration

  // ... (continues through all 11 steps)

  // Final assertion: ClientOrder status = Closed; guarantee balance reconciled
  await page.goto(`/orders/${createdOrderId}/razdolzuvanje`);
  await expect(page.getByText('Варијанса: €0.00')).toBeVisible();
});
```

### J5 — Pattern: visual regression (optional v1, recommended post)

Snapshot per page; fail on diff > 1%.

### J6 — CI integration

```yaml
# .github/workflows/e2e.yml
on: [pull_request, schedule]
jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker compose up -d --build  # bring up LON locally
      - run: cd tests/playwright && npm ci && npx playwright test
      - if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: tests/playwright/playwright-report/
```

---

## How to use this file

1. Pick the lowest-letter, lowest-number prompt not yet done in current phase.
2. Open a fresh Claude Code session in the repo root.
3. Copy the prompt block (the fenced ` ``` ` content) verbatim.
4. Let Claude Code execute. Stay in that session until VERIFICATION.md checks pass.
5. SESSION_LOG entry written, commit pushed, status updated in PLAN.md.
6. Move to next prompt.

If a prompt fails midway: don't try to "patch" it from a different angle — fix the underlying issue, then resume that same prompt. Cross-prompt entanglement is how we got to the May 2026 chaos in the first place.
