# LON — Session Log

> Append-only хронолошки запис. Секој таск добива еден запис. Запиши веднаш по verification, не групно на крај.

## 2026-04-18 — P2.1 IM 42 00 Customs Declaration E2E (backend + UI)

**Status:** [x] done
**Commits:** `e8c72d6 phase-2.1: IM 42 00 declaration flow — LON auth enforce, auto-MRN, status lifecycle` + `c37b011 phase-2.1: fix — propagate Box 02/15/17 sender/country fields to handler`

**Why this one mattered:** First business-critical compliance flow. Mistakes here are rewrites later — so ahead-of-code alignment on MRN policy, lifecycle, and LON authorization semantics was explicit (see CLAUDE.md §10: никогаш не „ова работи" без верификација).

**Design decisions (user-approved):**
1. **Box 37 model:** renamed curated `CustomsProcedure.Code` from internal `INW-PROC` mnemonic to SAD `4200` (member of MK Правилник Box 37 codelist). Declaration.ProcedureCode is now mirror-assigned from the FK procedure.
2. **MRN policy:** (b) auto-fallback. Placeholder format `YYMK<8-hex>A1` (e.g. `26MK62636F15A1`, 14 chars) if payload MRN is empty. User-provided MRN is uppercased. Full state machine for real-customs submission deferred to Phase 4.2 (PEE010 XML).
3. **Lifecycle:** added `DeclarationStatus` enum (Draft/Registered/Submitted/Cleared/Cancelled). On create with MRN → Registered. `IsCleared` bool is kept for backward compat but is the mirror of `Status==Cleared` (backfill migration.)
4. **Scope:** backend + UI + tests + VPS verification, one PR. Followed user "сè заедно".

**Backend changes:**
- `LON.Domain/Enums/Enums.cs` — new `DeclarationStatus`.
- `LON.Domain/Events/DomainEvents.cs` — new `CustomsDeclarationCreatedEvent` (P2.2 guarantee debit listener).
- `LON.Domain/Entities/Customs/Customs.cs` — `CustomsDeclaration.Status` property.
- `LON.Application/Customs/Commands/CreateCustomsDeclaration/CreateCustomsDeclarationCommand.cs` — full rewrite. DTO gains `LONAuthorizationId`, Box 02/15/17 fields (`SenderName`, `SenderAddress`, `SenderCountry`, `CountryOfDispatch`, `CountryOfDestination`, `SpecialRemarks`), optional `Status`. Handler:
  - Validates procedure exists & is active.
  - For codes `4200`/`5100` → **enforces** LONAuthorizationId (tenant-scoped lookup + active status + IssueDate/ExpiryDate window). Clear error on failure.
  - Generates placeholder MRN if missing; per-tenant uniqueness check prevents replay.
  - Creates `MRNRegistry` row for `procedure.RequiresMRNTracking = true` procedures, with `TotalQuantity = Σ line.Quantity`, `UsedQuantity = 0`, `ExpiryDate = DeclarationDate + procedure.DueDays`.
  - Line Duty = `CustomsValue × DutyRate / 100`; VAT base = `CustomsValue + Duty` (per ELON_Research/04 `PresmetajDavackiPoNaim`).
  - Emits `CustomsDeclarationCreatedEvent` pre-save.
  - Status → Registered when MRN present (default).
- New rules:
  - `CurrencyIsoRule` (Box 22, 38 ISO 4217 codes accepted by MK customs).
  - `CountryIsoRule` (Box 15/17/34/02, 50 ISO 3166-1 alpha-2).
  - `LONAuthorizationRequiredRule` (safety-net for /validate endpoint; delegates same DB check).
- Patched `ProcedureCodeValidRule` to fall back to `CustomsProcedures` table (fixed pre-existing bug — KB `CodeListItems.ListType='ProcedureCode'` is empty).
- `CustomsController` — new `GET /api/customs/lon-authorizations`; validate() endpoint carries new fields.
- `ApplicationDbContextSeed` — renamed `INW-PROC` → `4200` in seed; new idempotent `SeedTeksportLONAuthorizationIdempotent` seeds `26/TEKSPORT/0001` (Active, 1-year validity, GuaranteeAmount=100k EUR).
- Migration `20260418190910_AddDeclarationStatusAndProcedureCode4200`: `AddColumn Status INT DEFAULT 0`, backfill `Status = IsCleared ? 3 : (MRN IS NOT NULL ? 1 : 0)`, and `UPDATE CustomsProcedures SET Code='4200' WHERE Code='INW-PROC'`.

**Frontend changes:**
- `frontend/web/src/services/api.ts` — `customsApi.getLONAuthorizations(activeOnly)`.
- `CustomsDeclarationForm.tsx`:
  - State gains `lonAuthorizationId`, `senderName/Address/Country`, `countryOfDispatch/Destination`.
  - Loads LON authorizations in parallel with other ref data.
  - LON auth `<select>` shown conditionally when selected procedure.code is `4200`/`5100` (with "Задолжително" hint + УСЦЗ член 349 reference).
  - MRN placeholder updated to `Остави празно за авто-генерирање` with small-print explanation.
  - Box 02/15/17 inputs added; `senderName` required client-side; ISO country inputs uppercase on change.
  - `StatusBadge` component in header for edit mode (colored by Draft/Registered/Submitted/Cleared/Cancelled).

**Tests (4 in `tests/LON.IntegrationTests/CustomsDeclarationTests.cs`, run on CI):**
1. IM 4200 with valid LON auth + MRN empty → 200; DB row has MRN matching `^\d{2}MK[0-9A-F]{8}A1$`, Status=Registered, TotalDuty=50, TotalVAT=189; MRNRegistry row with Total=100, Used=0.
2. IM 4200 without LON auth → 400 with `LONAuthorizationId is required`.
3. IM 4200 with currency `XYZ` → 400 (rejects invalid ISO).
4. IM 4200 with explicit MRN → stored uppercased.

**How verified on VPS (commit `c37b011` deployed):**

- SQL before deploy:
  ```
  Code   | Name
  4200   | Увоз за облагородување (42 00)     ← renamed from INW-PROC ✅
  26/TEKSPORT/0001 | Active                   ← seeded LON auth ✅
  ```
- `POST /api/customs/declarations` (full payload, MRN empty) → 200, `data=1b7c7185-a76e-4a97-808e-cf7ff67c3fd1`
- SQL on saved declaration:
  ```
  DEC-P21-SMOKE | 26MK62636F15A1 | Status=1 | 4200 | Duty=50.0000 | VAT=189.0000
  ```
- SQL on MRN registry:
  ```
  26MK62636F15A1 | Total=100.0000 | Used=0.0000 | Expires=2026-10-15 (180 days after DeclarationDate) ✅
  ```
- Negative: without LON auth → 400 `"LONAuthorizationId is required for procedure '4200'. File a LON authorization before submitting an IM 4200 declaration."`
- Negative: currency `XYZ` → 400 includes `"Box 22: Валутата 'XYZ' не е од дозволените ISO 4217 кодови"`.

**Compliance footprint:**
- Box 37 procedure code = `4200` (SAD-compliant).
- Box 02 Sender required (Правилник, член 8 — enforced by both handler/rule engine AND frontend).
- ISO 4217 / ISO 3166 currency & country validation.
- LON authorization enforced under УСЦЗ член 349 (active + tenant-scoped + period).
- MRN registry opens per-declaration tracking window (180 days for 4200; configurable via `CustomsProcedure.DueDays`).

**Follow-ups (parallel backlog, not blocking):**
- P2.2 guarantee auto-debit — consume `CustomsDeclarationCreatedEvent`. Already emitted.
- PEE010 XML output (Phase 4.2) will consume registered declarations to build the customs submission envelope; state will transition Registered → Submitted.
- Full CustomsDeclaration update endpoint (PUT) doesn't yet use MediatR or refresh Status/MRNRegistry. Declarations currently edited via raw EF in the controller — out of P2.1 scope.
- Cyrillic mojibake in `kb/processed/*.json` (P6.18) unblocks i18n of rule messages but doesn't affect P2.1.
- Legacy Trosoci/Rabat (landing costs pro-rata) not modeled (ELON_Research/04 §1 "Trosoci/Rabat"). Plan: P2.x.

---


## 2026-04-18 — P1.6 User ↔ Tenant provisioning (MediatR)

**Status:** [x] done
**Commit:** `59878b6 phase-1.6: MediatR CreateUserCommand + cross-tenant provisioning`

**Files changed:**
- `src/LON.Application/Common/Interfaces/IPasswordHasher.cs` (new) — Application-layer abstraction so the handler avoids referencing Infrastructure.
- `src/LON.Application/Users/Commands/CreateUser/CreateUserCommand.cs` (new) — record + handler. Validates tenant existence/active, global username uniqueness (IgnoreQueryFilters since User.Username is still global), role ids; explicit `TenantId == Guid.Empty` falls back to DbContext auto-fill (caller's tenant).
- `src/LON.Infrastructure/Services/AuthService.cs` — `IAuthService` now extends `IPasswordHasher` so the existing HashPassword method satisfies both contracts.
- `src/LON.Infrastructure/DependencyInjection.cs` — register `IPasswordHasher` forwarded to the `IAuthService` singleton instance (same scope).
- `src/LON.API/Controllers/UsersController.cs` — class-level `[Authorize(Roles="Administrator")]`; POST refactored to dispatch `CreateUserCommand` via MediatR. `CreateUserRequest` gains optional `Guid? TenantId`.
- `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` — regenerated (tenantId now in CreateUserRequest schema).
- `tests/LON.IntegrationTests/UserProvisioningTests.cs` (new) — 4 tests: cross-tenant provisioning + new-user isolation; invalid tenantId → 400; omitted tenantId → caller's tenant via auto-fill; unauthenticated → 401.

**Semantics chosen:**
- `tenantId` in payload is **optional**. Omitting it keeps legacy behavior (DbContext auto-fill = caller's tenant). Provided → handler validates + persists explicit value. This is backwards-compatible with `frontend/web/src/pages/UserManagement.tsx` which currently doesn't send tenantId.
- Handler authorization is coarse — trusts the controller's `[Authorize(Roles="Administrator")]`. A finer super-admin vs tenant-admin split is a future task (outside P1.6 scope).
- Username remains globally unique (`User.Username` without composite index). P1.7 will decide between `username@tenant-code` / subdomain / tenant-picker before relaxing.

**How verified on VPS:**
1. Commit+push → `ssh root@... git pull && docker compose build api && up -d api`. API healthy.
2. Admin login → POST /api/tenants → **DUP-CODE-TEST** (`9f5f7912-fafd-41c4-bcff-eb88ce488dbb`).
3. POST /api/users with explicit `tenantId=DUP-CODE-TEST.id` → 200. SQL assert:
   ```
   admin         | B8D4FE76-... | TEKSPORT      | 1
   dup-p16-admin | 9F5F7912-... | DUP-CODE-TEST | 1
   ```
4. Login as `dup-p16-admin/DupTest123!` → JWT `tenant_id` = `9f5f7912-...` ✅
5. **Isolation proof (bidirectional):**
   - Admin GET /api/users → only `admin` (not `dup-p16-admin`).
   - `dup-p16-admin` GET /api/users → only himself (not `admin`).
   - Admin GET /api/masterdata/items → 5 TEKSPORT items.
   - `dup-p16-admin` GET /api/masterdata/items → `count: 0` (DUP-CODE-TEST has none).
6. **Negative paths:**
   - POST /api/users with bogus tenantId → **400** `{"errorMessage":"Tenant '00000000-...' does not exist or is inactive."}`.
   - POST /api/users unauthenticated → **401**.

**Notes / follow-ups:**
- Integration tests run on CI (Docker required for Testcontainers; local Windows box has no Docker Desktop). Next GitHub Actions run should validate all four tests.
- UI retrofit for tenant selector in `UserManagement.tsx` is intentionally deferred — frontend still works because tenantId is optional and falls back to caller tenant. Flagged for P1.7 or a dedicated UI sub-task.
- Non-goal for P1.6 (explicit per WORK_PLAN Current Active Task): super-admin switcher UI; multi-tenant login UX reform.

---

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

## 2026-04-18 — P1.5 Composite (TenantId, Code) unique indices

**Status:** [x] done
**Commits:** `2a2924d phase-1.5: composite (TenantId, Code) unique indices for tenant-scoped entities`
**Files changed:**
- 6 config files updated: `MasterDataConfigurations.cs`, `UserManagementConfiguration.cs`, `CustomsConfigurations.cs`, `LONAuthorizationConfiguration.cs`, `GuaranteeConfigurations.cs`, `ProductionConfigurations.cs`, `WMSConfigurations.cs`
- New migration `20260418182719_CompositeTenantUniqueIndices.cs` (dropped 22 globally-unique indices, created 22 composite (TenantId, X) unique indices)

**What was done:**
22 single-column unique indices replaced with composite `(TenantId, X)`:
- MasterData (8): `Item.Code`, `Warehouse.Code`, `Partner.Code`, `Shift.Code`, `WorkCenter.Code`, `Machine.Code`, `Employee.EmployeeNumber`, `Employee.Email`
- WMS (6): `Receipt.ReceiptNumber`, `Shipment.ShipmentNumber`, `PickTask.TaskNumber`, `Transfer.TransferNumber`, `PickingWave.WaveNumber`, `CycleCount.CountNumber`
- Production (3): `ProductionOrder.OrderNumber`, `MaterialIssue.IssueNumber`, `ProductionReceipt.ReceiptNumber`
- Customs (3): `CustomsDeclaration.DeclarationNumber`, `CustomsDeclaration.MRN`, `MRNRegistry.MRN`
- Guarantee (1): `GuaranteeAccount.AccountNumber`
- LON (1): `LONAuthorization.AuthorizationNumber`

**Explicitly LEFT globally unique:**
- `User.Username`, `User.Email` — login flow assumes global uniqueness. Multi-tenant login UX (tenant-code prefix, subdomain, etc.) is a deferred decision.
- `Tenant.Code`, `Tenant.LegacyUvoznik` — the scope root.
- Reference/KB data: `UnitOfMeasure.Code`, `Role.Name`, `Permission.Name`, `TariffCode.TariffNumber`, `CodeListItem.(ListType,Code)`, `CustomsProcedure.Code`, `DeclarationRule.RuleCode`.

**How verified на VPS:**
- Migration applied cleanly (no errors in logs).
- DB check: 22 `IX_*_TenantId_*` unique indices exist on the expected tables.
- **Positive test** — inserted `Items.Code='RM-001'` under a new 2nd tenant (`DUP-CODE-TEST`) while TEKSPORT already has `RM-001` → both rows coexist. ✅
- **Negative test** — attempted to insert a SECOND `RM-001` under TEKSPORT → rejected with `Msg 2601: Cannot insert duplicate key row in object 'dbo.Items' with unique index 'IX_Items_TenantId_Code'. The duplicate key value is (b8d4fe76-..., RM-001).` ✅
- Regression counts unchanged: Receipts 6, Inventory 3, Items 5, Partners 4, Warehouses 2, Tenants 1.
- Artifacts cleaned up afterward.

**Follow-ups / notes:**
- `ShiftConfiguration` lives in `UserManagementConfiguration.cs` (legacy from when Shift was user-adjacent). Single source of truth — no duplicate config today — but misplaced. Add to deferred backlog as a tiny move if we touch the file again.
- `EmployeeNumber + Email` per-tenant uniqueness assumes employees never straddle tenants. That's the intended model (Employee is tenant-scoped).
- EF warnings about cross-filter required relationships fire on `ef migrations add` (CustomsProcedure↔CustomsProcedureDocument, User↔UserRole, etc.). Advisory only. Tracked mentally; no action needed until a broken query surfaces.

**Next (new session recommended — see end-of-turn note):** P1.6 — User ↔ Tenant provisioning UX. Currently the seeder pins `admin` to TEKSPORT and we have no way to create a second-tenant user through the product. TenantsController CRUD exists; user-create with tenant assignment is the missing piece.

---

## 2026-04-18 — P1.4 EF global query filter for every ITenantScoped entity

**Status:** [x] done
**Commits:** `5cc6f72 phase-1.4: EF global query filter for every ITenantScoped entity`
**Files changed:**
- `src/LON.Infrastructure/Persistence/ApplicationDbContext.cs` — `CurrentTenantId` captured from `ICurrentUserService.TenantId` at construction; `ConfigureTenantScoped<T>` promoted to instance method and now sets `HasQueryFilter(e => !e.IsDeleted && (CurrentTenantId == null || e.TenantId == CurrentTenantId))`
- `tests/LON.IntegrationTests/TenantIsolationTests.cs` — new `AuthenticatedQuery_DoesNotLeakOtherTenantsItems` seeds a 2nd tenant + foreign Item and asserts admin can't see it

**What was done:**
1. **`CurrentTenantId` on DbContext:** read once from `_currentUser.TenantId` (which reads `tenant_id` claim from JWT via IHttpContextAccessor). Null for seeders/migrations/login-before-auth — that null triggers a filter bypass so those paths still see every row.
2. **Reflection pass in `OnModelCreating` upgraded:** same loop that wires up FK + index now also sets the combined query filter. Combines soft-delete (`!IsDeleted`) AND tenant scoping in a single `HasQueryFilter` — needed because EF only allows one filter per entity, and per-entity configurations already declared the soft-delete filter.
3. **Instance method for `ConfigureTenantScoped<T>`:** was static; now closes over `this.CurrentTenantId`. EF re-reads the field per query per DbContext instance, so every request gets the correct scope from its own JWT claim.
4. **Integration test** (TenantIsolationTests): seeds `ISO-DEMO` tenant + `FOREIGN-ISOLATION-TEST` item via `IgnoreQueryFilters()` path, then logs in as TEKSPORT admin and asserts `/api/masterdata/items` never contains the foreign code. Runs on CI (Testcontainers-MsSql).

**How verified на VPS (elon.elbosoft.click):**
- Migration-less deploy (no schema change) — API restarted clean, no errors in logs.
- **Regression check** — all existing reads return same counts as P1.3: Receipts 6, Inventory 3, Items 5, Partners 4, Warehouses 2, Tenants 1 ✅
- **Isolation proof on VPS:**
  1. SQL inserted 2nd tenant (`VPS-ISO-DEMO`) + foreign Item (`FOREIGN-VPS-CHECK`). DB total: 6 items, 2 tenants.
  2. API `GET /api/masterdata/items` (admin/TEKSPORT bearer) returned 5 items: `FG-001, SF-001, RM-001, RM-002, PKG-001`. `FOREIGN-VPS-CHECK` **not leaked**. ✅
  3. Cleaned up — DB back to 5 items, 1 tenant. Final state verified.
- Login flow still works (auth query against Users table at login time runs with CurrentTenantId=null because user hasn't authenticated yet → filter bypassed → admin found).

**Follow-ups / notes:**
- **Tenant** entity itself has NO query filter (as designed; it's the scope root, not ITenantScoped). TenantsController is admin-only and returns the full list — which is correct.
- **Global reference tables** (UoM, Role, Permission, CustomsProcedure, KB tables) unaffected — no filter applied, all tenants see the same global data.
- **Admin cross-tenant read** (super-admin UI to view all tenants' data): not implemented, deferred until it's a real requirement (currently tracked in P1.6 pending concrete UX ask). Meanwhile handlers can use `IgnoreQueryFilters()` where genuinely needed.
- **EF warnings** about required cross-filter relationships (CustomsDeclaration↔CustomsDocument etc.) still fire — those are advisory and not errors; can be addressed if they cause query surprises.

**Next:** P1.5 — `(TenantId, Code)` composite unique constraints instead of globally-unique `Code`. Currently `Warehouse.Code`, `Item.Code`, `Partner.Code` etc. are globally unique — that breaks the moment a second tenant wants to use a code another tenant already uses (e.g. both tenants having `RM-001`). Migration + index rework.

---

## 2026-04-18 — P1.3 tenant_id JWT claim + claim-based auto-fill

**Status:** [x] done
**Commits:** `e723f7e phase-1.3: tenant_id JWT claim + zero-lookup auto-fill path`
**Files changed:**
- `src/LON.Infrastructure/Services/AuthService.cs` — `GenerateJwtToken` emits `tenant_id` claim from `user.TenantId`
- `src/LON.Application/Common/Interfaces/ICurrentUserService.cs` — new `Guid? TenantId` property
- `src/LON.API/Services/CurrentUserService.cs` — implementation reads `tenant_id` claim via IHttpContextAccessor
- `src/LON.Infrastructure/Persistence/ApplicationDbContext.cs` — auto-fill resolution order now: claim → Users lookup → first active
- `tests/LON.IntegrationTests/AuthTests.cs` — `Login_JwtContainsTenantIdClaim_MatchingSeededTenant` asserts the claim is present and is a non-empty Guid

**What was done:**
1. `AuthService` adds one claim (`tenant_id` = `user.TenantId.ToString()`) to every issued JWT. Safe for admin and non-admin users — all users are tenant-scoped since B1.
2. `ICurrentUserService.TenantId` exposes the claim without hitting DB. Safe to inject into `ApplicationDbContext` (no DI cycle since ICurrentUserService only depends on `IHttpContextAccessor`).
3. `CurrentTenantService.GetTenantIdAsync` (from B1) already preferred the claim as step 1 — now that path actually fires. Users lookup + first-active fallbacks remain for background jobs, seeders, and legacy tokens.
4. `ApplicationDbContext.SaveChangesAsync` auto-fill: reads `_currentUser?.TenantId` as first choice (zero DB hits for authenticated writes); falls back to `Users` lookup then first-active for background jobs.

**How verified на VPS:**
- Login + base64-decode JWT payload → `"tenant_id": "b8d4fe76-8d94-470b-a251-f8111d3f1db3"` ✅ (matches TEKSPORT id seeded in P1.1)
- Full claim set intact: nameidentifier, name, email, EmployeeId, role, Permission[] — no regression.
- Existing reads continue to work (`receipts` → 6, `inventory` → 3, `items` → 5, `partners` → 4).
- Integration test `Login_JwtContainsTenantIdClaim_MatchingSeededTenant` added; runs on CI (Testcontainers-MsSql needs Docker, not available on the local Windows host at time of commit — to be observed on next CI run).

**Follow-ups / notes:**
- **Refresh tokens still work** — `ValidateRefreshTokenAsync` looks up user, then re-issues a JWT via `GenerateJwtToken`. New JWT will include the claim automatically.
- **Stale tokens before deploy**: any in-flight token issued before this commit lacks the claim. They still work thanks to the Users-lookup fallback. After their natural expiry (ExpiryMinutes), they disappear.
- **Ready for P1.4** — global query filters can now call `ICurrentTenantService.GetTenantIdAsync()` which, in an authenticated request, resolves via claim with zero DB round-trip.

**Next:** P1.4 — apply `HasQueryFilter(e => e.TenantId == tenantId)` to every ITenantScoped entity via reflection (same pattern as auto-FK-wiring in `OnModelCreating`). Then seed a 2nd tenant and verify data isolation.

---

## 2026-04-18 — P1.2-B2 ITenantScoped on 31 remaining entities

**Status:** [x] done
**Commits:** `bbf8ac9 phase-1.2-B2: ITenantScoped on 31 remaining domain entities`
**Files changed:**
- `src/LON.Domain/Entities/MasterData/MasterData.cs` (+3: Shift, WorkCenter, Machine)
- `src/LON.Domain/Entities/WMS/WMS.cs` (+8: Transfer, TransferLine, CycleCount, CycleCountLine, PickingWave, PickTask, Shipment, ShipmentLine)
- `src/LON.Domain/Entities/Customs/Customs.cs` (+4: CustomsDeclaration, CustomsDeclarationLine, CustomsDocument, MRNRegistry)
- `src/LON.Domain/Entities/Customs/LONAuthorization.cs` (+2: LONAuthorization, LONAuthorizationItem)
- `src/LON.Domain/Entities/Guarantee/Guarantee.cs` (+3: GuaranteeAccount, GuaranteeLedgerEntry, DutyCalculation)
- `src/LON.Domain/Entities/Production/Production.cs` (+9: BOM, BOMLine, Routing, RoutingOperation, ProductionOrder, ProductionOrderMaterial, ProductionOrderOperation, MaterialIssue, ProductionReceipt)
- `src/LON.Domain/Entities/Traceability/Traceability.cs` (+2: TraceLink, BatchGenealogy)
- `src/LON.Infrastructure/Migrations/20260418174311_AddTenantIdToRemainingEntities.cs` (new)
- `src/LON.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (regenerated)

**What was done:**
1. 31 entities got `: ITenantScoped` + `public Guid TenantId { get; set; }` (first property of each class).
2. `OnModelCreating` auto-configures FK + index per ITenantScoped via reflection (unchanged since B1). `SaveChangesAsync` auto-fills TenantId on new inserts (unchanged since B1).
3. Migration `AddTenantIdToRemainingEntities`:
   - 31 AddColumn with `defaultValue = Guid.Empty`
   - 31 CreateIndex `IX_<Table>_TenantId`
   - **Inline SQL backfill block** (manually inserted between CreateIndex and AddForeignKey) — resolves TEKSPORT tenant (fallback: first active), then 31 `UPDATE ... SET TenantId = @tenantId WHERE TenantId = '00000000-...'` so the FK constraints accept every row.
   - 31 AddForeignKey to `Tenants(Id) ON DELETE RESTRICT`.
4. Explicitly kept NON-tenant-scoped (per ITenantScoped.cs comment): `Tenant` itself, `UnitOfMeasure`, `ItemUoMConversion`, `Role`, `Permission`, `CustomsProcedure`, `CustomsProcedureDocument`, all KB tables (`TariffCode`, `CodeListItem`, `CustomsRegulation`, `DeclarationRule`, `KnowledgeDocument`, `KnowledgeDocumentChunk`).

**Verified на VPS (elon.elbosoft.click):**
- `docker compose build api && up -d api` → migrations applied cleanly (logs: "Database is ready (migrations applied or already up to date)").
- SQL: `INFORMATION_SCHEMA.COLUMNS` check against 31 table names → **0 tables missing the TenantId column** ✅
- SQL sample: `WorkCenters`(2 rows), `GuaranteeAccounts`(2), `BOMs`(1), `Shifts`(3) — every row backfilled to TEKSPORT (0 `Guid.Empty` survivors) ✅
- API smoke: `GET /api/wms/receipts` → 6, `GET /api/wms/inventory` → 3, `GET /api/masterdata/items` → 5, `GET /api/masterdata/partners` → 4 — no query-filter regressions ✅

**Follow-ups / discoveries:**
- EF validation warnings (advisory, not errors) about required relationships crossing query-filter boundaries: CustomsDeclaration↔CustomsDocument, CustomsProcedure↔CustomsProcedureDocument, Partner↔LONAuthorization, Item↔LONAuthorizationItem. Two of those (CustomsDeclaration↔Document, Partner↔LONAuthorization) are now tenant-filtered on both sides, but config-side filter alignment should still be revisited when query filters land (P1.4). Logged as context for P1.4.
- Receipt count went 5 → 6 between B3 and B2 verifications. Not a bug: auto-fill kept working through the schema change; new insert correctly tenant-scoped.
- **41 of ~45 business entities are now tenant-scoped.** The 4 remaining "global" reference-data DbSets and joined tables (UoMs, Roles, Perms, CustomsProcedures, KB) stay shared across tenants by design.

**Next:** P1.3 — JWT `tenant_id` claim on login, `CurrentTenantService` starts reading it. Unblocks Phase 2 end-to-end tenant isolation.

---

## 2026-04-18 — P1.2-B3 WH-TEK-VN (Vinica) warehouse seeded

**Status:** [x] done
**Commits:** `b609f4b phase-1.2-B3: seed WH-TEK-VN`, `f845c5d phase-1.2-B3: use ASCII for WH-TEK-VN address`
**Files changed:** `src/LON.Infrastructure/Persistence/ApplicationDbContextSeed.cs`

**What was done:**
1. `SeedWarehouses` refactored to per-code idempotent upsert (`SeedWarehousesIdempotent`). Definitions extracted into `WarehouseSeed`/`LocationSeed` records so additional sites can land as data-only diffs.
2. Added `WH-TEK-VN` (TEKSPORT Vinica) with 7 default locations (same codes as WH-MAIN; `Location.Code` unique per warehouse only).
3. `TenantId` populated by `ApplicationDbContext.SaveChangesAsync` auto-fill (TEKSPORT fallback) — handler stayed untouched.

**How verified на VPS (elon.elbosoft.click):**
- `SELECT ... FROM Warehouses` → 2 rows: `WH-MAIN` + `WH-TEK-VN`, both `TenantId = b8d4fe76-...`
- `SELECT ... FROM Locations WHERE w.Code='WH-TEK-VN'` → 7 locations RCV-01/STG-A-01/STG-A-02/PICK-01/PROD-01/SHIP-01/QUA-01, Types 1–6, all TenantId = TEKSPORT
- `GET /api/masterdata/warehouses` (admin bearer) → both warehouses, address `"Vinica, North Macedonia"` clean ASCII ✅

**Discoveries / follow-ups:**
- **UTF-8 source encoding bug (PRE-EXISTING, not introduced by B3)** — Cyrillic string literals in seed files get stored as CP1251→UTF-8 mojibake in the DB. TEKSPORT tenant `Address` (seeded in P1.1) already has this corruption. Root cause: `.cs` files lack UTF-8 BOM and the compiler guesses the wrong codepage on the Linux build container. Initial Vinica address `"Виница, Република Северна Македонија"` triggered the same issue; switched to ASCII `"Vinica, North Macedonia"` to scope the fix. **New Phase 6 ticket:** `P6.18 — Fix UTF-8 source encoding`, covers BOM/csproj setting + one-shot backfill of corrupted rows (Tenants.Address at minimum).
- Seeder's new per-code idempotent pattern is safe to reuse for other master-data types that should grow across releases (items, partners, procedures). Previously `AnyAsync()` guards would have blocked growth.
- Noted during CLAUDE.md hydration: Current Active Task recommendation was B3 → B2 → P1.3; B3 done. Next recommended: B2.

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

## 2026-04-18 — Session wrap + handoff prepared

**Why:** Корисник изрази дека има „лошо искуство" со нови сесии кои бараат re-explanation. Подготвен handoff материјал така што следна сесија да продолжи без прекинат контекст.

**Added / updated:**
- `CLAUDE.md §8.1` — NEW „ПРЕД првата реплика на корисник — задолжителна hydration". Експлицитно: НЕ ПРАШУВАЈ за VPS/креденцијали/одлуки, сè е запишано. Чекори: MEMORY.md → CLAUDE.md 3–7 → WORK_PLAN Current Active Task + first 40 lines → last 3 SESSION_LOG entries.
- `WORK_PLAN.md` (top) — NEW „🎯 SESSION KICKOFF" блок со quick-facts табела (VPS, admin creds, TEKSPORT id, ELON DB, languages, deploy flow, TEST project).
- `WORK_PLAN.md` Current Active Task — ажурирано со експлицитни алтернативи (P1.2-B3 брзо, P1.3 средно, P1.2-B2 голем) + препорака + контекст „што мора да знаеш од претходна сесија" (DI cycle, pattern, регенерирање API types).
- `memory/session_handoff.md` (NEW) — најрепрезентативен memory document; индекс-ориентирана hydration за следна сесија, со сите quick-facts.
- `memory/MEMORY.md` — додаден pointer со **„READ FIRST."** ознака за handoff.

**State на main (last good):**
- Commit `7a4ebc0 log: P1.2-B1 verified — 5 receipts + 2 balances backfilled to TEKSPORT`
- Phase 0 done. Phase 6-A done. Phase 2.5 setup done (retrofit паралелно — Login, Sidebar, Dashboard преведени; ~30 страници чекаат). Phase 1 P1.1 done + P1.2-B1 done.
- VPS up, API healthy, admin login works, 5 receipts + 2 inventory balances сите со TEKSPORT TenantId.

**Следна сесија очекува:** P1.2-B3 (брз: Виница warehouse seed) → P1.2-B2 (extend ITenantScoped to remaining entities) → P1.3 (JWT tenant claim). Друг редослед прифатлив ако корисникот одлучи.

---

## 2026-04-18 — P1.2-B1 ITenantScoped на 10 core entities

**Status:** [x] done — partial scope (B1 of B1/B2/B3)

**Mechanism:**
- `ITenantScoped { Guid TenantId }` interface во `LON.Domain/Common/`
- 10 entities implement it: Item, Warehouse, Location, Partner, Employee, User, Receipt, ReceiptLine, InventoryBalance, InventoryMovement
- `ApplicationDbContext.OnModelCreating` auto-wires Tenant FK + TenantId index за сите `ITenantScoped` entities преку reflection-dispatched generic helper (`ConfigureTenantScoped<T>`)
- `ApplicationDbContext.SaveChangesAsync` auto-fills TenantId кога entity е Added со Guid.Empty:
  1. Lookup од тековен user's TenantId (преку `ICurrentUserService.UserId` → `Users` table)
  2. Fallback на first active Tenant (за seeders, migrations, background jobs)
  - Inlined наместо да инјектира `ICurrentTenantService` за да се избегне DI cycle (DbContext ↔ service).

**Migration `AddTenantIdToCoreEntities`:**
1. AddColumn `TenantId` (Guid.Empty default) на 10 tables
2. CreateIndex `IX_<Table>_TenantId` на 10 tables
3. Sql backfill — **SET TenantId = TEKSPORT.Id WHERE TenantId = Guid.Empty** на сите 10
4. AddForeignKey `FK_<Table>_Tenants_TenantId` на 10 tables (FK constraint passes бидејќи backfill завршил)

**Additional infrastructure:**
- `ICurrentTenantService` + `CurrentTenantService` (API) — достапен за handlers кога сакаат explicit tenant pre-save. JWT claim > user lookup > first active.
- Program.cs: `ApplicationDbContextSeed.SeedTenantsAsync(context)` повикан **ПРЕД** `UserManagementSeed.SeedAsync(...)`. TEKSPORT мора да постои пред admin user за auto-fill да работи на fresh DB.
- `CreateReceiptCommandHandler` unchanged од caller's POV — auto-fill го покрива (каubavite CQRS changes од B1 scope).

**Verified end-to-end на VPS:**
- `SELECT tenantId FROM existing receipts` → сите 5 со `b8d4fe76-8d94-470b-a251-f8111d3f1db3` (TEKSPORT) ✅
- `SELECT tenantId FROM existing inventory balances` → 2 со TEKSPORT ✅
- POST нов receipt БЕЗ `tenantId` во payload → handler не го сета, SaveChangesAsync auto-fill → resulting record со TEKSPORT ✅
- `createdBy: "admin"` audit-трагата од P0.5 останува функционална ✅

**Interesting observation:** RM-002 inventory сега има 50.0000 (беше 30). Корисникот направил уште receipt во browser-от за 20 — upsert pattern (match by Item+Location+Batch+MRN+UoM+Quality) work-а правилно.

**Next:**
- B2 — применување на ITenantScoped на останатите ~25 scoped entities + миграција
- B3 (бонус) — seed WH-TEK-VN (Виница) warehouse за TEKSPORT

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
