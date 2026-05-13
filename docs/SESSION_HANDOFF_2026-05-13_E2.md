# Session handoff — 2026-05-13 (post-§E2) → next session

> **READ FIRST.** Most-recent state of LON Phase 17 work. Continue with `§E3` per AGENT-PROMPTS.md.

---

## Where we are

**Phase 17 main:** §E0 + §E1 + §E2 shipped & VPS-verified. §E3 is next.

Commits today (2026-05-13):
- `2d166d8` phase-17.E1 — ClientOrder entity + handlers + SQL SEQUENCE
- `9690bcb` phase-17.E1 — session log + plan status + next-session handoff
- `792361e` phase-17.E2 — ClientOrder list + hub UI shell

Repo state at handoff:
- **main** at `792361e` pushed to `origin/main`.
- VPS at `792361e` deployed; `https://elon.elbosoft.click/health` = 200; `/orders` + `/orders/:id` live.
- Local LONDB and VPS LONDB both at 51 migrations (50 baseline + P17_E1).

## What changed in this session (E2)

### Frontend additions
- `pages/Orders/OrderList.tsx` (382 lines) — DataTable + 4 filters + „Нов налог" FormDialog.
- `pages/Orders/OrderHub.tsx` (351 lines) — 3-column layout per BLUEPRINT §5.1.
- `hooks/queries/useClientOrders.ts` (165 lines) — react-query hooks for list / detail / create / update / cancel.
- `services/api.ts` — `clientOrdersApi` block.
- `nav/types.ts` — `NavGroupKey` += `'orders'`.
- `nav/navGroups.ts` — „📋 Налози" group prepended (hub-and-spoke entry point first).
- `App.tsx` — `/orders` + `/orders/:id` routes + module mapping.
- `i18n/locales/en.json` + `mk.json` — `nav.orders.*`, `nav.groups.orders`, top-level `orders.{statusNames,list,hub,actions}.*`.

### VPS evidence

- `GET /api/ClientOrders` → 2 rows (CO-2026-000001, CO-2026-000002).
- `POST /api/ClientOrders` with E2-SMOKE-001 ref → `486e7222-…` → `CO-2026-000002`.
- Browser smoke (admin@VPS): list + dialog + hub + tooltip all rendered cleanly. Screenshots saved.

## Open items / known issues

1. **Hub widgets render `0%` literal** — `producedPct` and `guaranteePct` are hardcoded placeholders. Real values wire in §E5 / §E7 (producedQty / FG.Quantity ratio) and §E3 + GuaranteeLedger (debit total / authorization amount). Same for `% Произведено` / `% Гаранција` columns on /orders.
2. **Hub tabs show placeholder copy** — Declarations / ProductionOrders / Shipments / Materials tabs show static §EX hints. Real DataTables wire in §E3–§E8.
3. **Timeline shows 3 stub events** — Created (filled with createdAt) + FirstDeclaration (pending) + LastShipped (pending). Real domain-event sourcing wires in §E11.
4. **Role-based action enable/disable not yet wired** — Nav `allowedRoles` controls sidebar visibility. Action launcher buttons are all disabled in E2 anyway; per-role enable + permission check lands alongside §E3 implementations.
5. **react-hook-form .d.ts errors** — pre-existing library noise in `tsc --noEmit`; ignored. CRA `npm run build` works.
6. **`docs/legay_app/` + `docs/Prompt za nov blueprint.txt`** untracked locally — user added these, intentionally not auto-committed.

## Recommended next step

Continue with **§E3 — Wire IM declaration creation from hub** per `AGENT-PROMPTS.md §E3`.

Scope (high-level):
1. Make the „Креирај увозна декларација (IM)" action button in `OrderHub.tsx` `enabled` and open an inline `FormDialog`.
2. Form fields (react-hook-form + Zod or rules-prop):
   - DeclarationNumber (auto-suggested from `INumberSequenceService` for entity `CustomsDeclaration` — emit `IM-{year}-{seq:D6}`).
   - DeclarationDate (default today).
   - CustomsProcedure (combo; default `51 00` or `42 00`).
   - Partner / sender (autocomplete from `/api/MasterData/partners`).
   - SenderName / Address / Country (auto-populate from Partner).
   - LONAuthorization — prefilled from ClientOrder.LONAuthorizationId.
3. „Преглед / Lines" tab — inline DataTable for line entry (or routed line-edit page if simpler).
4. „Зачувај како Draft" / „Поднеси (Submitted)" buttons.
5. On Create: hits POST `/api/Customs/declarations` with `clientOrderId` set; close dialog; refetch ClientOrder detail + Declarations tab; refresh list page.
6. Concurrency regression test: open 2 parallel browser tabs to same `/orders/{id}`, create simultaneously → expect 2 distinct DeclarationNumbers (validates SEQUENCE).
7. Side-effect on ClientOrder: when first IM lands, transition Status from `Draft` → `Active` (computed; not user-editable).
8. Commit: `phase-17.E3: wire IM declaration creation from ClientOrder hub`. Deploy + VPS smoke (open `/orders/4f41b642…` → create → see in Declarations tab).

VERIFICATION.md §E3 checklist:
- `grep "Креирај увозна декларација" frontend/web/src/pages/Orders/OrderHub.tsx` matches.
- `grep "actions.imDeclaration" frontend/web/src/i18n/locales/mk.json` matches.
- 2-tab concurrency yields distinct numbers.
- Hub Declarations tab refetches and shows new entry post-create.
- ClientOrder.Status transitions Draft → Active after first IM submit.

Estimated session size: medium (~1-2 sessions). The handler + endpoint may already exist for CustomsDeclaration — confirm before duplicating; this is mostly UI plumbing + form fields + status-transition wiring.

## Things to read first (hydration checklist)

1. `MEMORY.md` (auto-loaded).
2. `CLAUDE.md` — esp. §11 (Phase 17 tracker; §11.2 main table: E0+E1+E2 = `[x]`).
3. **This handoff doc** — what changed in 2026-05-13 §E2 session.
4. Yesterday's handoff: `docs/SESSION_HANDOFF_2026-05-13.md` — context for §E2 (now done).
5. `BLUEPRINT.md` §5.2 (CustomsDeclaration IM flow) + §5.1 (hub) + §7.2 (contextual actions) before starting §E3.
6. `AGENT-PROMPTS.md §E3` — exact prompt for the next task.
7. `VERIFICATION.md §E3` — what to assert before declaring done.
8. Last 3 entries in `SESSION_LOG.md` (2026-05-13 §E2 + §E1 + §E0).

## VPS access

- SSH: `ssh -i ~/.ssh/id_ed25519 root@173.212.254.216`. Use PowerShell `$env:USERPROFILE\.ssh\id_ed25519` from Windows because MSYS bash can't resolve Cyrillic-named home dir.
- Working dir: `/opt/apps/LON/LON-test`.
- Compose file: `docker-compose.yml` (services: `sqlserver`, `api`, `frontend`, `worker` — container names `lon-*`).
- Rebuild frontend only: `docker compose up -d --build frontend`.
- App URL: `https://elon.elbosoft.click`.

## Test users on VPS

| Username | Password | Role | Sidebar 📋 Налози | Notes |
|---|---|---|---|---|
| admin | Admin123! | Administrator | ✅ (always) | smoke-tested in this session |
| tek-mgr | Test123! | Manager | ✅ | edit rights for all actions |
| tek-customs | Test123! | Customs Officer | ✅ (read-only) | will edit IM/EX in §E3/§E8 |
| tek-wh-op | Test123! | Warehouse Operator | ✅ (read-only) | will edit Receipt in §E4 |
| tek-qc | Test123! | Quality Controller | ✅ (read-only) | will edit QC in §E8 |
| tek-finance | Test123! | Finance Clerk | ✅ (read-only) | will edit ClientContract / Invoice (later) |
| tek-hr | Test123! | HR Manager | ❌ (no Налози — HR-only) | — |
| tek-operator | Test123! | Production Operator | ❌ | — |
| tek-maint | Test123! | Maintenance Tech | ❌ | — |

---

*End of handoff. Good luck with §E3.*
