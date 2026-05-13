# Session handoff — 2026-05-13 (post-§E3) → next session

> **READ FIRST.** Most-recent state of LON Phase 17 work. Continue with `§E4` per AGENT-PROMPTS.md.

---

## Where we are

**Phase 17 main:** §E0 + §E1 + §E2 + §E3 shipped & VPS-verified. §E4 is next.

Commits today (2026-05-13):
- `2d166d8` phase-17.E1 — ClientOrder entity + handlers + SQL SEQUENCE
- `9690bcb` phase-17.E1 — session log + plan status + handoff
- `792361e` phase-17.E2 — ClientOrder list + hub UI shell
- `16b029d` phase-17.E2 — session log + plan status + handoff
- `6e2add6` phase-17.E3 — wire IM declaration creation from ClientOrder hub

Repo state at handoff:
- **main** at `6e2add6` pushed to `origin/main`.
- VPS at `6e2add6` deployed; `https://elon.elbosoft.click/health` = 200; new migration applied.
- Local + VPS LONDB at 52 migrations (50 baseline + P17_E1 + P17_E3).

## What changed in this session (E3)

### Backend
- `CreateCustomsDeclarationCommand`: optional `ClientOrderId` field; auto-numbering via SEQUENCE when `DeclarationNumber=""`; status transition Draft → Active on first link.
- New migration `P17_E3_AddDeclarationSequences` — per-tenant `seq_IMDeclaration_<tid>` + `seq_EXDeclaration_<tid>`.
- `GET /api/Customs/declarations?clientOrderId=…` filter added.
- `NumberFormatter.Declaration(prefix, year, seq)` helper.

### Frontend
- `pages/Orders/ImDeclarationDialog.tsx` (new, 456 lines) — header + lines editor with LON authorization + ClientOrder linkage shown as hint banner.
- `OrderHub.tsx` — IM action enabled; Declarations tab now live (`<DeclarationsTab>` react-query against filtered customs endpoint); per-action `enabled` flag retains "Coming in §E…" tooltips on the still-disabled 8 actions.
- `customsApi.getDeclarations` accepts `{ isCleared?, clientOrderId? }` params (backwards-compatible).
- i18n: `orders.imDialog.*` + `orders.hub.tabs.declarationsEmpty/declCols.*` (en + mk).

### Tests
- `ClientOrderDeclarationLinkTests.cs` (new, 2 facts) — auto-numbering + linkage + status transition; parallel-create distinctness.

### VPS evidence
- Created **`IM-2026-000002`** on real `CO-2026-000001` via API smoke (tariff `2905399500`, netWeight=100). Filter API returns 1 row. ClientOrder.Status flipped Draft → Active.
- Browser smoke: hub shows Active chip, Declarations tab populated, IM dialog opens with hint banner.

## Open items / known issues

1. **SEQUENCE gap on validation failure** — `IM-2026-000001` was consumed by a failed earlier attempt (rule-engine rejected tariff). This is normal SEQUENCE behavior and matches legacy ELON (aborted entries created gaps too).
2. **`ClientOrder.Status` flipped inline** in `CreateCustomsDeclarationCommandHandler`. §E11 will refactor this into a domain-event handler subscribed to `CustomsDeclarationCreatedEvent`.
3. **`DeclarationsTab` uses CSS-grid table**, not the shared `DataTable` component. Fine for 6 read-only columns; revisit when sorting/pagination is needed.
4. **8 actions still disabled** on the hub action launcher; §E4–§E10 each enables one.
5. **Hub widgets (`% Produced`, `% Guarantee`)** still render literal `0%`. They start to mean something after §E5 (production) and §E3+guarantee-ledger join (left for §E10 dashboard polish).
6. **react-hook-form .d.ts errors** — still ignored pre-existing noise.
7. **`docs/Prompt za nov blueprint.txt` + `docs/legay_app/`** still untracked locally.

## Recommended next step

Continue with **§E4 — Wire Receipt from hub** per `AGENT-PROMPTS.md §E4`.

Scope summary:
1. Hub action „Прими во магацин" → enable + opens dialog showing approved IM declarations for this ClientOrder.
2. DataTable: columns DeclarationNumber / Date / Sender / TotalLines / ReceivedLines (back via existing receipt handlers — likely `CreateReceiptCommand` + lines).
3. Select declaration → step 2: receive-lines editor (expected qty / received qty / skart qty / location / batch / MRN / qualityStatus).
4. POST `/api/WMS/receipts` with all line data.
5. Side effects to verify on hub:
   - Inventory tab shows new `InventoryBalance` rows.
   - Declaration line marked „Received" in Declarations tab.
6. Variance handling: if received <> expected, show AI helper hint inline (stub for now: „Препорака: проверете packaging").
7. Commit: `phase-17.E4: wire Receipt creation from ClientOrder hub`. Deploy + smoke.

VERIFICATION.md §E4 checklist:
- `grep "Прими во магацин" frontend/web/src/pages/Orders/OrderHub.tsx` — already there but disabled; this task flips `enabled: true`.
- After receive: `InventoryBalance` count via API increases by N where N = number of lines received.
- Variance: receive 95 of 100 declared → line status on hub shows „Partially received".

Estimated session size: medium (~1-2 sessions). Receipt handler + DTO + endpoint may already exist (review `src/LON.Application/WMS/...` and `src/LON.API/Controllers/WMSController.cs`). Most work will be UI wiring + multi-step form.

## Things to read first (hydration checklist)

1. `MEMORY.md` (auto-loaded).
2. `CLAUDE.md` — §11 (Phase 17 tracker; main table E0+E1+E2+E3 = `[x]`).
3. **This handoff doc** + previous handoffs (`SESSION_HANDOFF_2026-05-13.md`, `…_E2.md`).
4. `BLUEPRINT.md` §5.3 (Receipt flow) + §5.2 (CustomsDeclaration IM) before starting §E4.
5. `AGENT-PROMPTS.md §E4` — exact prompt.
6. `VERIFICATION.md §E4`.
7. Last 4 entries in `SESSION_LOG.md` (2026-05-13 §E3 + §E2 + §E1 + §E0).

## VPS access

- SSH from PowerShell: `ssh -i $env:USERPROFILE\.ssh\id_ed25519 root@173.212.254.216`.
- Working dir: `/opt/apps/LON/LON-test`.
- Compose: `docker compose up -d --build api frontend`.
- App URL: `https://elon.elbosoft.click`.

## Test data on VPS for §E4

- `ClientOrder` `4f41b642-0a1a-4d47-9d14-131a2d49c30e` (`CO-2026-000001`) → Status **Active** (after §E3).
- `CustomsDeclaration` `388be0e0-…` (`IM-2026-000002`) linked to it.
- `LONAuthorization` `26/TEKSPORT/0001` — allowed tariffs: `2905399500`, `1211200050`.
- Test users: see previous handoff. `admin / Admin123!` is the smoke account.

---

*End of handoff. Good luck with §E4.*
