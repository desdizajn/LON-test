# Session handoff — 2026-05-13 → next session

> **READ FIRST.** Most-recent state of LON Phase 17 work. Continue with `§E2` per AGENT-PROMPTS.md.

---

## Where we are

**Phase 17 main**: §E0 + §E1 shipped & VPS-verified. §E2 is next.

Commits since session start (2026-05-12):
- `6e27a88` phase-17.pre.1+2 — BLUEPRINT + PLAN bootstrap + CLAUDE.md fact corrections + BLUEPRINT §9.1 mapping update
- `7e67f1e` phase-17.pre.2b — Cowork audit corrections (Izdatnica/Ispratnica + inflate flag + sticky-defaults reframe + HR caveat)
- `f6f0fb7` phase-17.pre.3 — 6 user decisions D1-D6 ratified; CommercialInvoice + DeliveryNote entities added
- `4847d43` phase-17.pre.4 — `docs/migration/MAPPING.md` (500-line authoritative legacy→LON mapping)
- `6e2b9c7` → `5f07cb2` phase-17.pre.5 — VPS LONDB wipe (script + execution)
- `4b9170a` phase-17.pre.6 — env-var admin password infrastructure (D2)
- `31bbaa9` phase-17.pre.7 — LON.Migration discovery + E.MIGRATE deferred
- `96cc515` + `06e6019` phase-17.E0 — sticky-defaults hook + bulk field-update foundation
- `2d166d8` phase-17.E1 — ClientOrder entity + handlers + SQL SEQUENCE

Repo state at handoff:
- **main** at `2d166d8` pushed to `origin/main` (GitHub: desdizajn/LON-test).
- VPS at `2d166d8` deployed and healthy (`https://elon.elbosoft.click/health` returns 200).
- Local LONDB has 51 migrations applied (50 baseline + 1 P17_E1).
- VPS LONDB has 51 migrations applied; backup `LONDB_pre-wipe_20260512T091454Z.bak` retained at `/var/opt/mssql/backup/`.

## What changed in this session

### Phase 17.PRE closed (foundations)

- Reframed BLUEPRINT §9.1 mapping (9 corrections from prep recon + 8 Cowork audit findings).
- New entities documented in BLUEPRINT: `CommercialInvoice` (§3.2.1, D4) + `DeliveryNote` (§3.8, D5).
- D6=Phase 21 prod-export for HR → new task §21.1.1.
- `docs/migration/MAPPING.md` is the table-by-table legacy→LON authority. Update it when E-tasks discover new gaps.
- VPS LONDB wiped + re-seeded with TEKSPORT tenant (id `95daf6d1-3723-4750-bb30-e1217540d622`) + admin + 8 test users + master data.

### Phase 17 main started

- **§E0** done — generic sticky-defaults hook + bulk field-update endpoint pattern. Foundation only; consumed by E3/E5/E8.
- **§E1** done — ClientOrder + ClientOrderFinishedGood entities, 5 handlers, 5 endpoints under `/api/clientorders`, per-tenant SQL SEQUENCE for `CO-{year}-{seq:D6}` numbering, nullable ClientOrderId FK on CustomsDeclaration / ProductionOrder / Shipment.

VPS smoke evidence:
- `POST /api/clientorders` → ClientOrder `4f41b642-0a1a-4d47-9d14-131a2d49c30e`, OrderNumber `CO-2026-000001`, Status Draft.
- `GET /api/clientorders/{id}` returns DTO with `finishedGoods: []`.

## Open items / known issues

1. **CI integration tests** — All E0 + E1 backend integration tests added but run on CI Docker (Testcontainers MsSql). Locally not executed; trust the CI run.
2. **Admin password fallback** — VPS API seed logs warning `LON_BOOTSTRAP_ADMIN_PASSWORD env var not set — seeding admin with fallback 'Admin123!'`. D2 infrastructure is in place; setting the env var on VPS `.env` is a future improvement (not blocking; admin can change password via UI anyway).
3. **`docs/legay_app/` + `docs/Prompt za nov blueprint.txt`** untracked locally — user added these, intentionally not auto-committed.
4. **react-hook-form .d.ts errors** — pre-existing library noise in `tsc --noEmit`; ignored. CRA `npm run build` works.

## Recommended next step

Continue with **§E2 — ClientOrder list + hub UI shell** per `AGENT-PROMPTS.md §E2`.

Scope:
1. New route `/orders` — DataTable list page (uses `components/common/DataTable.tsx`). Columns: OrderNumber, Customer, Status, OrderDate, RequestedShipDate, %Produced, GuaranteeUtilization. Filters: status, customer, dateRange. „Нов налог" button → FormDialog.
2. New route `/orders/:id` — hub layout per BLUEPRINT §5.1:
   - Header: order number, status badge, customer link, authorization link, dates, notes.
   - Left vertical timeline (stub for now; real events in §E11).
   - Center: 3 widgets (produced %, guarantee utilization %, days-to-ship).
   - Right action launcher (buttons disabled for now; E3-E9 wire them).
   - Tabs: Declarations | Production Orders | Shipments | Materials in stock.
3. React-query hooks: `frontend/web/src/hooks/queries/useClientOrders.ts` — useClientOrders, useClientOrder(id), useCreateClientOrder, useCancelClientOrder, useUpdateClientOrder. Reuse `OpenAPI schema.d.ts` types.
4. Sidebar (`frontend/web/src/nav/navGroups.ts`): add „📋 Налози" group with item linking to `/orders`. Roles: Administrator, Manager, ProductionPlanner (full); WhMgr, Customs, QC, Finance (read-only).
5. i18n keys (en + mk only per BLUEPRINT §6.8 v1 scope): `nav.orders.*`, `orders.list.*`, `orders.hub.*`, `orders.actions.*`.
6. VPS smoke: visit `/orders`, click „Нов налог", create one, navigate to /orders/{id}, verify all action buttons render disabled with „Coming in E3-E9" tooltip.

Estimated session size: medium (~1-2 sessions; E2 is mostly UI scaffolding + react-query hooks).

## Things to read first (hydration checklist for new session)

1. `MEMORY.md` (auto-loaded).
2. `CLAUDE.md` — esp. §11 (Phase 17 tracker; §11.1 PRE table all `[x]`; §11.2 main table E0+E1 `[x]`).
3. **This handoff doc** — what changed in 2026-05-13 session.
4. `BLUEPRINT.md` §5.1 (ClientOrder hub UX) + §7.1 (hub-and-spoke) + §7.2 (contextual actions) before starting §E2.
5. `AGENT-PROMPTS.md §E2` — exact prompt for the next task.
6. `VERIFICATION.md §E2` — what to assert before declaring done.
7. Last 2-3 entries in `SESSION_LOG.md` (2026-05-13 §E1 + §E0; 2026-05-12 PRE phase close).

## VPS access

- SSH: `ssh root@173.212.254.216` (passwordless from `~/.ssh/id_ed25519`).
- Working dir: `/opt/apps/LON/LON-test`.
- App URL: `https://elon.elbosoft.click`.
- Containers: `lon-api`, `lon-frontend`, `lon-sqlserver`, `lon-worker`.
- SA password trick: `base64(docker inspect lon-api → ConnectionStrings__DefaultConnection Password)` because the literal has shell-special chars. See `scripts/wipe-vps-londb.sh` for the helper.

## Test users on VPS (for §E2 manual smoke)

| Username | Password | Role |
|---|---|---|
| admin | Admin123! | Administrator |
| tek-mgr | Test123! | Manager |
| tek-customs | Test123! | Customs Officer |
| tek-wh-op | Test123! | Warehouse Operator |
| tek-qc | Test123! | Quality Controller |
| tek-finance | Test123! | Finance Clerk |
| tek-hr | Test123! | HR Manager |
| tek-operator | Test123! | Production Operator |
| tek-maint | Test123! | Maintenance Tech |

---

*End of handoff. Good luck with §E2.*
