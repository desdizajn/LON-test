# Session handoff — 2026-05-13 (post-§E5) → next session

> **READ FIRST.** Most-recent state of LON Phase 17 work. Continue with `§E6` per AGENT-PROMPTS.md.

---

## Where we are

**Phase 17 main:** §E0 + §E1 + §E2 + §E3 + §E4 + §E5 shipped & VPS-verified. §E6 is next.

Commits today (2026-05-13), in order:
- `2d166d8` E1 — ClientOrder entity + SQL SEQUENCE
- `9690bcb` E1 — docs
- `792361e` E2 — list + hub UI shell
- `16b029d` E2 — docs
- `6e2add6` E3 — IM declaration from hub
- `d524cb1` E3 — docs
- `5ee4785` E4 — Receipt from hub
- `38f2b93` E5 — BOM + ProductionOrder from hub

Repo state at handoff:
- **main** at `38f2b93` pushed to `origin/main`.
- VPS at `38f2b93`; `https://elon.elbosoft.click/health` = 200.
- Local + VPS LONDB at 52 migrations (50 baseline + P17_E1 + P17_E3).

## ClientOrder hub state (`CO-2026-000001` on VPS)

`Status = 2 (Producing)`, 1 IM declaration, 1 Receipt, 1 FinishedGood, 1 ProductionOrder.

| Action | Status | Wires what |
|---|---|---|
| Внеси готови производи (BOM) | ✅ enabled (§E5) | Add FG + optional PO |
| Креирај увозна декларација (IM) | ✅ enabled (§E3) | IM declaration |
| Прими во магацин | ✅ enabled (§E4) | BulkReceiptFromDeclaration |
| Распредели подизведувач | 🚧 §E6 | — |
| Издади материјал | 🚧 §E7 | — |
| Креирај извозна декларација (EX) | 🚧 §E8 | — |
| Razdolzuvanje | 🚧 §E9 | — |
| Аудит / историја | 🚧 §E13 | — |
| 💡 AI препораки | 🚧 §E10 | — |

Hub tabs (all live with real react-query data now): Declarations, Production Orders, Shipments (still placeholder), Materials in stock (= Receipts).

## What changed in this session (E4 + E5)

### Backend (additive — no breaking changes)
- `GET /api/WMS/receipts` + `?clientOrderId=…` filter via `Receipt.Lines[].CustomsDeclarationId → CustomsDeclaration.ClientOrderId`.
- `GET /api/Production/orders` + `?clientOrderId=…` filter.
- `POST /api/ClientOrders/{id}/finished-goods` — new endpoint.
- `CreateProductionOrderCommand` — optional `ClientOrderId` field; status transition Draft/Active → Producing on first PO link.
- `AddClientOrderFinishedGoodCommand` (new).

### Frontend
- `BomDialog.tsx` + `ReceiveDialog.tsx` — both follow the same FormDialog + react-hook-form pattern as `ImDeclarationDialog`. All 3 invalidate `clientOrderKeys.all` on success.
- `ProductionOrdersTab` + `ReceiptsTab` — react-query against the new filtered endpoints.
- `productionApi.getOrders` + `wmsApi.getReceipts` migrated from positional args to params-object (2 + 2 existing positional callers updated in the same commits).
- `clientOrdersApi.addFinishedGood` helper.

### Tests
- `ClientOrderReceiptLinkTests.cs` — bulk-receipt linkage via clientOrderId filter.
- `ClientOrderBomFlowTests.cs` — FG + PO linkage + Producing transition + filter.

## Open items / known issues

1. **Producing status doesn't auto-revert** if all POs become Cancelled. §E9 (Razdolzuvanje / Closed-status) will introduce the corresponding reverse transitions via domain-events (§E11).
2. **Shipments hub tab still shows placeholder copy** — §E8 wires it.
3. **Materials in stock tab shows Receipts**, not InventoryBalance — sufficient for hub use case (which receipts apply to this order). Real per-item on-hand qty stays in `/warehouse/receipts`; deep-link from the row could land later.
4. **BomDialog: BOM auto-suggestion** highlights highest-version BOM only globally — partner-scoped preference exists in the backend (`CreateProductionOrderCommandHandler` already prefers partner-scoped BOMs) but isn't shown in the UI dropdown order. Cosmetic, not blocking.
5. **No status transition Producing → Shipped/Closed yet** — §E8 + §E9 handle those.
6. **No domain events yet** — §E11 will refactor every inline status transition + side-effect into MediatR notifications.

## Recommended next step

Continue with **§E6 — Wire Podelba (distribute material to subcontracted producer) from hub** per `AGENT-PROMPTS.md §E6`.

Scope sketch:
- Hub action „Распредели подизведувач" → dialog with:
  - Source warehouse picker (default = HQ warehouse).
  - Target producer picker (autocomplete `/api/MasterData/partners?type=Producer`).
  - Material rows: list `InventoryBalance` for materials linked to this ClientOrder's POs (filter via existing `wmsApi.getInventory()`); user picks qty per material.
- AI helper stub panel inline: `/api/Suggestions/producer?clientOrderId=X` returning most-used producer in past 3 months. v1: stub it inline; real impl in §E10.
- Submit → `POST /api/WMS/inventory/bulk-move-balances` with producerId set. (Existing wmsApi.bulkMoveBalances method already in api.ts.)
- Side-effect verified on hub: `InventoryBalance.AssignedProducerId` set → Materials tab refreshes.

VERIFICATION.md §E6 checklist:
- After podelba: `InventoryBalance` rows have `AssignedProducerId` set.
- Hub Materials tab refreshes (filter by producer).

Estimated session size: medium. The hardest part is figuring out which InventoryBalance rows to surface (filter on `Receipt.Lines.CustomsDeclarationId → CustomsDeclaration.ClientOrderId` + `LonProcessState ∈ {Imported, AwaitingDistribution}`). Most plumbing exists; mostly UI + a producer-suggestion stub.

## VPS access

- SSH from PowerShell: `ssh -i $env:USERPROFILE\.ssh\id_ed25519 root@173.212.254.216`.
- Working dir: `/opt/apps/LON/LON-test`.
- Compose: `docker compose up -d --build api frontend` (both because backend changed in §E5).
- App URL: `https://elon.elbosoft.click`.

## Test data on VPS for §E6

- `ClientOrder` `4f41b642-…` (`CO-2026-000001`) → Status **Producing**, 1 IM decl, 1 Receipt, 1 FG, 1 PO.
- `Receipt` `22767dbd-…` (`RCP-20260513-7da4ee24`) with 1 line × 50 qty → has created InventoryBalance row(s).
- Use `tek-mgr / Test123!` or `admin / Admin123!` for smoke.

---

*End of handoff. Good luck with §E6.*
