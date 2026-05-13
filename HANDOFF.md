# LON — Session Handoff (2026-05-13 → next session)

> **Read first.** This file is the bridge between today's session and the next one. Memory + SESSION_LOG carry the long form; this is the 5-minute orientation.

## Where the project stands (after today)

**Phase 17 (ClientOrder hub + flow wiring + AI helper)** — 10 of 16 sub-tasks closed.

```
[x] E0  Sticky defaults + bulk-update pattern             (06e6019)
[x] E1  ClientOrder entity + endpoints                    (2d166d8)
[x] E2  Order list + hub UI shell                         (792361e)
[x] E3  Wire IM declaration from hub                      (6e2add6)
[x] E4  Wire Receipt from hub                             (5ee4785)
[x] E5  Wire BOM + ProductionOrder from hub               (38f2b93)
[x] E6  Wire Podelba from hub                             (16f8711)
[x] E7  Wire MaterialIssue + ProductionReceipt from hub   (d47f973)
[x] E7.5 Department + Position → CodeListItem FK          (e50c3dd)
[x] E7.6 DeliveryNote entity + polymorphic auto-gen       (1c21599 + 607eb9e)
[x] E8  Wire EX declaration + Shipment + QC from hub      (0a2d458)
[ ] E8.5 CommercialInvoice entity + wire from EX (D4)
[ ] E9  Razdolzuvanje view per ClientOrder
[ ] E.MIGRATE  LON.Migration refactor + Z2779 e2e (deferred from PRE.7)
[ ] E10  AI helper + 3 core recommendations
[ ] E10.5 AlertRule + AlertEvent + worker
[ ] E11  Domain events infrastructure
[ ] E12  SQL SEQUENCE objects (most already created inline)
[ ] E13  Audit interceptor + /admin/audit-log
[ ] E14  Soft-delete + recycle bin
[ ] E16  FxRate entity + maintenance UI
[ ] E15  Playwright E2E happy-path
```

**Hub action launcher**: 8 enabled / 2 disabled
```
✓ BOM                    ✓ IM declaration         ✓ Receive into warehouse
✓ Podelba                ✓ Issue material         ✓ Production receipt
✓ EX declaration          ✓ QC + Packaging
  Razdolzuvanje (E9)       AI recommendations (E10)
```

**EF migrations applied:** 52
**Last good commit on `main`:** `0a2d458` (E8 code) + the SESSION_LOG / CLAUDE.md status commit will land before the next session.

---

## What's next — pick one of three

The task to start with is **your choice**; all three are independent and ready to go. Recommendation order:

### Option A — §E9 (Razdolzuvanje view per ClientOrder) — RECOMMENDED
- **Why first:** closes the user-facing loop on the hub (IM → Receive → Podelba → Issue → Receipt → EX → Razdolzuvanje). Without it the hub has a happy-path gap.
- **Scope:** a `/orders/:id/razdolzuvanje` panel that aggregates IM duty (charged), EX duty pro-rata + Waste + Return + FinalImport (released), variance flag (€0.50 tolerance), snapshot button that POSTs to `/api/Guarantee/snapshots`. Auto-flips `ClientOrder.Status` to `Closed` when balance reconciled + all lines flagged.
- **Prompt:** `AGENT-PROMPTS.md` §E9.
- **Verification:** `VERIFICATION.md` §E9.
- **Effort estimate:** ~half-day. Most of the math lives in existing GuaranteeAccount / GuaranteeLedgerEntry queries.

### Option B — §E8.5 (CommercialInvoice entity, D4)
- **Why second:** the EX dialog from today optionally chains a Commercial Invoice, but the entity doesn't exist yet. D4 (2026-05-12) approved it as a new v1 entity.
- **Scope:** new `CommercialInvoice` + `CommercialInvoiceLine` entities (BLUEPRINT §3.2.1), migration with per-tenant SQL SEQUENCE (`seq_CommercialInvoice_<tenantId>`), `CommercialInvoiceSuggestionService.SuggestFromShipment` (auto-populate lines from the just-created Shipment), CRUD handlers, controller, UI list+detail. Toast prompt after EX submission: „EX поднесен. Креирај commercial invoice?".
- **Prompt:** `AGENT-PROMPTS.md` §E8.5.
- **Verification:** `VERIFICATION.md` §E8.5.
- **Effort estimate:** ~full day. Mirrors the DeliveryNote shape from §E7.6.

### Option C — §E.MIGRATE (LON.Migration refactor + Z2779 end-to-end)
- **Why third:** unblocks Phase 21 cutover. Per PRE.7 findings, full Z2779 migration was deferred until E1+E5+E7.6+E8.5 all landed. E8.5 is the only one still pending.
- **Prerequisite:** E8.5 must land first.
- **Scope:** rewrite `src/LON.Migration` mappers around the 8 new/changed entities, add `--zaklucok 2779` flag, execute the canonical happy-path against the local ELON DB slice, run 6 reconciliation queries.
- **Prompt:** `AGENT-PROMPTS.md` §E.MIGRATE.

**Suggestion:** Start with **Option A (E9)** — it closes the hub UX loop and uses entities that are already shipped. Tackle E8.5 + E.MIGRATE as a pair afterward.

---

## Quick-facts you should NOT re-ask the user

These are in memory (`MEMORY.md`) but listing here for fast eye-scan:

| Thing | Value |
|---|---|
| VPS | `root@173.212.254.216` (Contabo) · passwordless SSH via `~/.ssh/id_ed25519` |
| VPS app path | `/opt/apps/LON/LON-test` |
| VPS domain | `https://elon.elbosoft.click` (Caddy + auto SSL) |
| Containers | `lon-sqlserver` (internal 127.0.0.1:1433), `lon-api`, `lon-worker`, `lon-frontend` |
| Admin login | `admin` / `Admin123!` |
| Test tenant | `TEKSPORT` id `b8d4fe76-8d94-470b-a251-f8111d3f1db3` |
| Local LON DB | `localhost`, Windows auth, DB=`LONDB`. 52 migrations applied. |
| Local legacy DB | `localhost`, Windows auth, DB=`ELON` (**read-only**, TEKSPORT slice) |
| Languages | mk (primary fallback), en, sq, sr (sq/sr fall back to mk for new keys per precedent) |
| Deploy flow | `git push origin main` → SSH VPS → `git pull && docker compose up -d --build api frontend` (~60s) |
| EF migration count | 52 |

---

## Useful sample data on VPS

```
ClientOrder CO-2026-000001 (4f41b642-0a1a-4d47-9d14-131a2d49c30e)
  ├ Status: Producing
  ├ 1 PO (LON-20260513-fc945b61 status=Completed produced=100/100)
  ├ 1 PO (eb315932-af20-45e0-960e-8b26fa8744d5 status=InProgress produced=… of 10)
  ├ 1 IM declaration: IM-2026-000002, MRN 26MK02203754A1, Used 50.0000
  ├ Inventory:
  │   ├ RCV-01 / PKG-001 / IM-2026-000002 / 26MK02203754A1 / qty 8.0 (producer PRD-SMOKE)
  │   ├ RCV-01 / PKG-001 / IM-2026-000002 / 26MK02203754A1 / qty 47.5556 (unassigned)
  │   ├ PROD-01 / PKG-001 / FG-PKG-001-20260513-B / qty 75.0 (FG from PR)
  │   └ PROD-01 / PKG-001 / FG-PKG-001-20260513 / qty 25.0 (FG from PR)
  └ 1 DeliveryNote: DN-2026-000001 ProducerDispatch (Sent, ConfirmedAt 17:24)
```

**Watch out:** the IM has `Used=50` but inventory totals `55.5556` due to TEKSPORT inflate-for-waste (5% inflation). An EX bulk-ship for this MRN trips `export.over_discharge`. For VPS smoke of new EX flow, either (a) pick a smaller subset of balances, or (b) seed a fresh ClientOrder + IM with a higher `Used` field.

```
ClientOrder CO-2026-000002 (486e7222-6e54-4382-92a2-2e16cff5bb66)
  └ Status: Draft, no FGs, no POs — clean slate
```

```
Partner: PRD-SMOKE (75d7780c-17e2-4c52-a886-11029e18af3c)
  └ Producer type, used as fixture for §E6/§E7.6 auto-gen smoke
```

```
CodeListItems (Phase 17 §E7.5, seeded via VPS smoke):
  EmployeeDepartment / SEW / Шиење  (feb8a99e-0d50-…)
  EmployeePosition   / STAFF / Работник  (267749ef-9ff5-…)
```

---

## Watchlist — things that will bite if you ignore them

1. **`BulkShipmentFromFG` is filter-based, not selection-based.** The hub EX dialog's checkbox picker is advisory; the server re-filters and drains everything matching the MRN. If user selects 1 of 2 same-MRN balances, the bulk drains both. Not a regression — documented Phase 22 follow-up to add a selection-based variant if real users complain.
2. **`createProductionReceipt` (legacy) posts to `/Production/receipts`, which isn't a route.** Pre-existing bug. The hub uses the new `createReceiptForOrder` helper that hits the correct `/Production/orders/{id}/receipts`. Standalone `ProductionReceiptForm.tsx` is therefore still broken. Phase 22 follow-up.
3. **`POST /WMS/inventory/quality-status` legacy field:** today's E8 added the missing handler. It accepts both `InventoryBalanceId` (legacy) and `BalanceId` (new). Don't try to "fix" the legacy field name in callers without verifying.
4. **`MaterialIssue` auto-gen of DeliveryNote skips silently when source balance has no `AssignedProducerId`.** This is correct (legacy/direct issues have no producer to ship to). If a user reports „no Propratnica created", confirm the materials went through §E6 Podelba first.
5. **sq / sr locales fall back to mk for the entire `orders.*` block.** Precedent set with §E1; do not break it without lifting the whole block. Phase 22 i18n catch-up.
6. **`positional records` trap (`feedback_positional_records_trap.md`).** Any new `[FromBody]` DTO must use init-only properties. E8 added `UpdateQualityStatusBody` with `init` correctly.
7. **EF migration version 52** is the canonical count; CLAUDE.md §5 reflects this. Bump the count in CLAUDE.md when adding migrations (E8.5 will add 53).

---

## Files to read first (in order)

1. `CLAUDE.md` — single source for rules + environments + defaults.
2. `MEMORY.md` (auto-loaded) — pointer index to durable facts.
3. `SESSION_LOG.md` — last 4 entries are E8 / E7.6 / E7.5 / E7. Each lists files touched + VPS evidence.
4. `AGENT-PROMPTS.md` §E9 (or §E8.5 / §E.MIGRATE based on chosen task).
5. `VERIFICATION.md` §E9 (idem).
6. `BLUEPRINT.md` §5.11 (Razdolzuvanje) — if pursuing Option A.

---

## Verification template for the next task

Use the same protocol every Phase 17 task has followed:

```
1. dotnet build src/LON.API/LON.API.csproj  → 0/0
2. ./scripts/gen-api-types.sh               → swagger + schema.d.ts updated (if DTOs changed)
3. cd frontend/web && CI=true node_modules/.bin/react-scripts build → compiled successfully
4. cd frontend/web && node_modules/.bin/eslint src/<changed files>  → no output
5. Integration test (if server logic changed): write a focused one before deploying.
6. git commit -m "phase-17.E<task>: <verb-phrase>" + co-author footer.
7. git push origin HEAD
8. ssh root@173.212.254.216 "cd /opt/apps/LON/LON-test && git pull && docker compose up -d --build api frontend"
9. until curl -sf -o /dev/null https://elon.elbosoft.click/health; do sleep 3; done
10. VPS smoke: POST/GET against the new endpoint(s) on CO-2026-000001 — paste actual JSON output in SESSION_LOG.
11. Update SESSION_LOG.md (append at top) + CLAUDE.md §11.2 table + last-revision footer.
12. Final commit: `phase-17.E<task>: session log + plan status`.
```

---

## End of handoff. Good luck.
