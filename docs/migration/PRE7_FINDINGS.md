# PRE.7 — `LON.Migration` discovery + happy-path scope assessment

> Phase 17.PRE.7 was originally scoped as: "import Z2779 → assert ClientOrder + IM + Receipt + BOM + Izdatnica + Razdolzuvanje + DeliveryNote". On closer inspection of `src/LON.Migration/` against `BLUEPRINT.md §9.1` + `docs/migration/MAPPING.md`, the migrator code has **structural mismatches** that block a meaningful happy-path drill right now. This document captures the findings + defers the full drill to a new Phase 17 task (`E.MIGRATE`) that runs after `E1` (ClientOrder entity) + `E5` (BOM wiring) + `E7.6` (DeliveryNote) + `E8.5` (CommercialInvoice) land.
>
> *Created 2026-05-12 — Phase 17.PRE.7.*

---

## §1 — Current `LON.Migration` state

5 mappers exist in `src/LON.Migration/Mappers/`:

| Mapper | What it does | BLUEPRINT-aligned? | Z2779-filter? |
|---|---|---|---|
| `ItemMapper` | `tblArtikli` → `Item` | ✅ Mostly correct (maps by ArtKatTip flag) | ❌ No filter |
| `AuthorizationMapper` | `Zaklucoci` → `LONAuthorization` | ❌ **Wrong direction.** BLUEPRINT says `Odobrenija → LONAuthorization` + `Zaklucoci → ClientOrder`. Current code conflates them. | ❌ No filter |
| `DeclarationMapper` | `FakturiU5Z` + `FakturiU5` → `CustomsDeclaration` + `Line` | ⚠ Aborts: expects `INW-PROC` procedure (legacy abbreviation that doesn't exist post-Phase 15). Should map to `4051/1041/6121/4200`. | ❌ No filter |
| `InventoryMapper` | `LagerMaterijali` → `InventoryMovement` + balance | ⚠ Partial: doesn't honor Proces→DocumentSource resolver (PREP recon revealed Proces=7→Izdatnica, 9→Ispratnica — see MAPPING.md §11.1). | ❌ No filter |
| `ReconciliationReporter` | R1–R6 queries | ⚠ Needs refresh per current 6 reconciliation queries in BLUEPRINT §9.1 + MAPPING.md §10 | ❌ No scoping |

Build status: `dotnet build src/LON.Migration/LON.Migration.csproj` → **0 warnings, 0 errors** (2026-05-12).

## §2 — Dry-run results (against local `ELON` + freshly-seeded local `LONDB`)

Commands executed (without writing — `--dry-run --limit 5..10`):

```text
items     : 10/10 mapped (write=10, dupe=0)
auths     :  5/5  mapped (writes Zaklucok→LONAuthorization — semantically wrong but plumbing works)
decls     :  ABORT: "no 'INW-PROC' CustomsProcedure in LON; seed it first"
inventory : 10/10 read, 0 written (all blocked by missingItem because items dry-run didn't materialize them)
```

Plumbing works (SQL conns + cmd dispatch + dry-run accounting). Logic needs alignment.

## §3 — Structural mismatches blocking happy-path execution

### 3.1 `Zaklucok → LONAuthorization` (current) vs `Zaklucok → ClientOrder` (BLUEPRINT)

`AuthorizationMapper.cs:14-16` reads:
> "We map each Zaklucok to a LONAuthorization in LON (finer granularity, since each decision is what individual declarations cite)."

This was a design choice before `ClientOrder` (Phase 17 §3.1) entered the BLUEPRINT. Now that ClientOrder is the canonical "Zaklucok analog", the migrator must:
- Map `Odobrenija → LONAuthorization` (4 rows in TEKSPORT).
- Map `Zaklucoci → ClientOrder` (269 rows).
- Map `Odobrenija.OdobrenieRBr` (via Zaklucok parent lookup) → `ClientOrder.LONAuthorizationId` FK.

**Blocked by:** `ClientOrder` entity (Phase 17 §E1 — not yet built).

### 3.2 `INW-PROC` procedure (current) vs `4051/1041/6121/4200` (BLUEPRINT)

`DeclarationMapper` aborts because LON's `CustomsProcedure` table no longer has a row with code `INW-PROC`. Current LON seed has `4051`, `1041`, `6121`, `4200` (per BLUEPRINT §9.1 + actual seed). DeclarationMapper needs to:
- Resolve procedure code from `FakturiU5Z.VidUIS` (legacy procedure code field).
- Default to `4051` (Inward processing import) when `VidUIS` is empty.

**Blocked by:** quick edit to DeclarationMapper (no schema change needed).

### 3.3 Proces→DocumentSource resolver missing

`InventoryMapper` doesn't honor the resolver per MAPPING.md §11.1:
- Proces=1 → `Receipt` (no exit doc) ✓ correctly handled
- Proces=7 → `MovementType=IssueToProducer`, DokRBr → `Izdatnici.IzdatnicaRBr`
- Proces=9 → `MovementType=WasteDestroyed`, DokRBr → `Ispratnici.IspratnicaRBr`
- Proces=8 → `ReturnFromProducer`, DokRBr → `Izdatnici.IzdatnicaRBr` (return voucher)

Current code maps all rows as plain inventory adjustments without invoking the matching legacy doc.

**Blocked by:** code refactor in InventoryMapper + `MaterialIssueMapper` + `WasteDeclarationMapper` (new mappers needed; entities exist).

### 3.4 Missing mappers

Per MAPPING.md §1-§7:

| Missing mapper | Target entity | Status |
|---|---|---|
| `ClientOrderMapper` | `ClientOrder` (Phase 17 §E1) | Entity missing |
| `BOMMapper` | `BOM` + `BOMLine` + `BOMLineWasteOverrides` | Entity exists |
| `FinishedGoodMapper` | `ClientOrderFinishedGood` (Phase 17 §E1) | Entity missing |
| `MaterialIssueMapper` | `MaterialIssue` (sourced from Proces=7 + Izdatnici) | Entity exists |
| `WasteDeclarationMapper` | `WasteDeclaration` (sourced from Proces=9 + Ispratnici) | Entity exists |
| `DeliveryNoteMapper` | `DeliveryNote` (Phase 17 §E7.6) | Entity missing |
| `CommercialInvoiceMapper` | `CommercialInvoice` (Phase 17 §E8.5) | Entity missing |
| `ReferenceTablesMapper` | `CodeListItem` (Country) + `UoM` | Could land now |

### 3.5 No `--zaklucok` / `--happy-path` flag

`Program.cs:4-19` doesn't parse a Zaklucok filter. Every mapper runs against full ELON. For Z2779 drill, need:
- `--zaklucok 2779` flag (or `--happy-path Z2779` alias).
- Each mapper's SELECT WHERE clause adds `AND ZaklucokBroj=@zb` (with appropriate JOIN to resolve which Zaklucok any given legacy row belongs to).

## §4 — Effort to reach full Z2779 happy-path

Minimum for end-to-end Z2779 drill:

1. **Phase 17 §E1 (ClientOrder)** — entity + handlers + endpoints. ~1–2 days.
2. **Phase 17 §E5 (BOM wiring)** — already exists; just wired from hub. ~1 day.
3. **Phase 17 §E7.6 (DeliveryNote)** — entity + auto-gen. ~2 days.
4. **Phase 17 §E8.5 (CommercialInvoice)** — entity + EX wiring. ~2 days.
5. **`E.MIGRATE` (new task)** — refactor LON.Migration + new mappers + `--zaklucok` flag + run + verify. ~2–3 days.

Total: roughly 8–10 days of focused work to deliver true happy-path. Phase 17 main work is then in parallel + dovetailed.

## §5 — What PRE.7 actually delivered

| Deliverable | Status |
|---|---|
| LON.Migration build verified (0 warnings, 0 errors) | ✅ |
| Dry-run smoke against local ELON + LONDB | ✅ |
| Findings catalogued vs MAPPING.md / BLUEPRINT §9.1 | ✅ (this document) |
| Z2779 happy-path migrated end-to-end | ⏳ Deferred to `E.MIGRATE` (after E8.5) |
| Six reconciliation queries pass | ⏳ Deferred to `E.MIGRATE` |

## §6 — `E.MIGRATE` — new Phase 17 task (deferred)

Added to `PLAN.md` + `AGENT-PROMPTS.md` + `VERIFICATION.md` as the Phase 17 milestone that does what PRE.7 originally aspired to. Lands AFTER `E1 + E5 + E7.6 + E8.5`.

Scope:
1. Refactor `AuthorizationMapper` → split into `OdobrenijaMapper` (→LONAuthorization) + `ClientOrderMapper` (→ClientOrder per BLUEPRINT §3.1).
2. Refactor `DeclarationMapper` → use 4051/1041/6121/4200 procedure resolution per `FakturiU5Z.VidUIS`.
3. Refactor `InventoryMapper` → emit DocumentSource per Proces (MAPPING.md §11.1); chain `MaterialIssueMapper` (Proces=7) + `WasteDeclarationMapper` (Proces=9).
4. Add `BOMMapper` (Normativi → BOM/BOMLine/BOMLineWasteOverrides) per MAPPING.md §5.2.
5. Add `FinishedGoodMapper` (GotoviProizvodi → ClientOrderFinishedGood).
6. Add `DeliveryNoteMapper` (auto-gen on MaterialIssue commit per BLUEPRINT §3.8).
7. Add `CommercialInvoiceMapper` (tblIzvozniFakturi → CommercialInvoice per §3.2.1).
8. Add `--zaklucok` filter to every mapper.
9. Refresh `ReconciliationReporter` to run R1–R6 per MAPPING.md §10.
10. Run `lon-migrate all --zaklucok 2779 --legacy ELON --lon LONDB`.
11. Assert: 1 LONAuthorization, 1 ClientOrder, 1 IM CustomsDeclaration with 13 lines, 13 InventoryMovements (Proces=1), 5-line BOM, 1 MaterialIssue + 1 DeliveryNote(ProducerDispatch), 3 WasteDeclaration rows, all 6 reconciliation queries pass within tolerance.

`E.MIGRATE` is positioned after `§E9 Razdolzuvanje view` and before `§E10 AI helper` in the Phase 17 sequence — by E9 all happy-path entities are wired.

## §7 — PRE phase closing summary

| Task | Status | Commit |
|---|---|---|
| PRE.1 — CLAUDE.md fact corrections | ✅ | `6e27a88` |
| PRE.2 — BLUEPRINT §9.1 + Cowork audit | ✅ | `6e27a88` + `7e67f1e` |
| PRE.3 — 6 user decisions D1–D6 | ✅ | `f6f0fb7` |
| PRE.4 — `docs/migration/MAPPING.md` | ✅ | `4847d43` |
| PRE.5 — VPS LONDB wipe (backup: `LONDB_pre-wipe_20260512T091454Z.bak`) | ✅ | `9b0967b` + `5f07cb2` |
| PRE.6 — env-var admin password infrastructure (D2) | ✅ | `4b9170a` |
| PRE.7 — LON.Migration discovery + E.MIGRATE deferred | ✅ (this doc) | (this commit) |

PRE phase is **CLOSED**. Phase 17 main starts with `§E0` (sticky-defaults hook).

---

*End of PRE7_FINDINGS.md.*
