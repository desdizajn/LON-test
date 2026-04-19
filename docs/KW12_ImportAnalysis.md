# KW12.xlsx — gap analysis + import plan

**Source file:** `docs/KW12.xlsx` (882 KB). Split deliverables: `docs/KW12_Matriks.xlsx`, `docs/KW12_Faktura.xlsx`, `docs/KW12_Transport.xlsx`.

Textile manufacturing week 12/2026 snapshot: **6 customer orders** exploded into **126 work-order lines** (size/color variants), **3 import MRNs**, **8 commercial invoices**, **3 customs closings** (dorabotka/verpackung/naknadna). All tied to a single tenant (**Firma=100**, i.e. the customer issuing the orders), delivered into **Lager 222**.

---

## 1. Sheet summaries

### 1.1 Matriks — 7 582 rows × 27 cols

Flattened BOM explosion. Every row is **one material consumption for one work-order line**.

| Column | Meaning | LON equivalent |
|---|---|---|
| TypKP (all `P`) | Record type (P=production) | — |
| Auftragsart (all `PRJ`) | Order class (project) | **MISSING** — would add `ProductionOrder.OrderClass` |
| Firma (all `100`) | Customer/client code | **MISSING** — `ProductionOrder.CustomerPartnerId` (LON `Partner` exists but is supplier-skewed) |
| WorkOrder (`PA2602067-0001`) | Internal WO number (root+line suffix) | `ProductionOrder.OrderNumber` |
| OrderNumber (`222-2026/10`) | Customer's order reference | **MISSING** — `ProductionOrder.CustomerOrderNumber` |
| Product (`182485422XL-1`) | FG item SKU (embeds size+color) | `ProductionOrder.ItemId` (via Items.Code) |
| TargetQty | Pieces to produce | `ProductionOrder.Quantity` |
| GutMenge | Produced so far (0 at import) | `ProductionOrder.ProducedQuantity` |
| Zustand (all `2`) | WO status (2 ≈ released in source system) | `ProductionOrder.Status` (map `2` → `Released`) |
| PlanBeginn | Planned start date | `ProductionOrder.PlannedStart` |
| Lager | Warehouse code (`222`) | `ProductionOrder.WarehouseId` |
| MRNNr (`202603876`) | Internal Austrian MRN seq | part of `UL number` chain (see Faktura) |
| BelegPos | Document position | `CustomsDeclarationLine.LineNumber` |
| Ingredient | Material item code | `ProductionOrderMaterial.ItemId` |
| ShortName / Description1/2 | Material descriptions | `Item.Name / Description` |
| ColorCode (`010`) | Color variant | **MISSING** — `Item.ColorCode` (or variants model) |
| Size | Size variant (usually empty, encoded in Product code) | **MISSING** |
| Qty | Material required per WO-line | `ProductionOrderMaterial.RequiredQuantity` |
| Gebucht | Already booked (0 at import) | `ProductionOrderMaterial.IssuedQuantity` |
| Unit (`M`, `STK`) | UoM code | `ProductionOrderMaterial.UoMId` (via `UnitsOfMeasure.Code`) |
| **MRNMK** (`26MKIM10150003D7B3`) | **Pre-assigned MK MRN to consume** | **MISSING** — `ProductionOrderMaterial.PreAssignedMRN` |
| MRNAT | AT MRN (reference only) | → `Notes` |
| InvoiceNumber | Invoice that delivered this material | — |
| EFF (`0.8934`) | Efficiency factor (output/input ratio) | **MISSING** — `BOMLine.EfficiencyFactor` or `ProductionOrderMaterial.EfficiencyFactor` |
| WeekNumber (`12`) | Calendar week | **MISSING** — `ProductionOrder.WeekNumber` (or derive) |

**Distinct counts:**
- 126 work-order lines → **6 unique root POs** (each root = 1 customer order exploded into ~21 size variants)
- 126 distinct FG products
- 118 distinct materials
- 1 MRN (all materials in Matriks came from the D7B3 batch)
- 1 invoice

### 1.2 Faktura — 490 rows × 17 cols (134 data rows)

Commercial invoice lines — the materials physically delivered.

| Column | Meaning | LON equivalent |
|---|---|---|
| Menge | Qty | `CustomsDeclarationLine.Quantity` / `ReceiptLine.Quantity` |
| VPE | Unit | `CustomsDeclarationLine.UoMId` (via Code lookup) |
| ArtNr | Article number | `CustomsDeclarationLine.ItemId` (via Items.Code) |
| Bezeichnung | Description | `Item.Name` (seed time) |
| m² | Area | **MISSING** — second quantity dimension |
| meter | Running meters | — (same as Menge for most) |
| GewichtNetto | Net weight (kg) | `CustomsDeclarationLine.NetWeight` |
| ZTN | Tariff code | `CustomsDeclarationLine.TariffCode` |
| **UL** | Origin + preferential flag (`BG`, `BG no pref.`, …) | **PARTIAL** — `CountryOfOrigin` has no preferential flag |
| Einzelpr | Unit price | `CustomsDeclarationLine.ItemPrice` |
| Gesamt | Total value | `CustomsDeclarationLine.StatisticalValue` (or sum) |
| Referenz | Invoice number | **MISSING** — `CustomsDeclaration.CommercialInvoiceNumber` |
| ULBroj | Transport manifest # | **MISSING** |
| MRNNrAT | AT MRN | — (reference) |
| MRNNrMK | MK MRN | `CustomsDeclaration.MRN` |

**Distinct counts:**
- 8 commercial invoices, 3 MK MRNs (same 3 as Transport)
- 17 origin tags (of which 8 are `<country> no pref.` variants)
- 35 tariff codes

### 1.3 Transport — 22 rows × 15 cols (8 data rows)

Customs event per (MRN, invoice).

| Column | Meaning | LON equivalent |
|---|---|---|
| KW | Calendar week | — |
| Zaklucok (`2397`, `2376nl13`, `2377nl11`) | Customs closing reference | **MISSING** — `CustomsDeclaration.ClosingNumber` |
| MRNMK | MK MRN | `CustomsDeclaration.MRN` |
| Datum | Date | `CustomsDeclaration.DeclarationDate` |
| **OpisPostapka** (`dorabotka`, `verpackung`, `naknadna`) | Procedure desc | **PARTIAL** — would map to `CustomsProcedure.Code` but our code list doesn't have these variants labelled; all three are **42 00** sub-cases |
| Faktura | Commercial invoice | (per-line: see Faktura mapping) |
| MRNAT | AT MRN | reference |
| Bruto / Neto | Gross / net weight totals | **MISSING** on declaration header (we sum from lines) |
| CMR | CMR transport doc | **MISSING** — `CustomsDeclaration.CMRNumber` |
| Koleti | Package count | `CustomsDeclaration.TotalPackages` ✓ |

---

## 2. Mapping to LON entities (strategic)

```
Transport row                      →  CustomsDeclaration header   (3 decls — one per MRN)
Faktura row                        →  CustomsDeclarationLine
Faktura header (invoice) aggregate →  ReceiptLine cohort          (imports the physical goods)
Matriks WO root (6)                →  6 ProductionOrder HEADS
Matriks WO sub-line (126)          →  126 ProductionOrder LINES (size-variant children)
Matriks row                        →  ProductionOrderMaterial     (7 582 consumption lines)
```

Best-fit import order (respecting FK dependencies):

1. **Items** — seed 259 new entries (133 materials + 126 FGs).  *Existing importer Items target handles this.*
2. **Partners** — 1 supplier (Texport AT), 1 customer (`Firma 100`).  *Existing Partners target handles it.*
3. **Transport + Faktura → CustomsDeclarations** — 3 declarations with ~45/40/49 lines each.  *Existing CustomsDeclarations target works for 1 declaration per file; **need to split by MRN** or extend target to group.*
4. **Receipts** — 134 receipt lines consuming the 3 MRNs.  *Existing Receipts target works.*
5. **Matriks → ProductionOrders** — **NEEDS NEW TARGET** (not in P5.1.5).  6 heads + 126 variant children + 7 582 material consumptions.

---

## 3. Gaps in LON data model

**Hard blockers (features wouldn't be correctly captured):**

| # | Gap | Impact | Proposed fix |
|---|---|---|---|
| G1 | `ProductionOrders` is not an import target | Can't automate Matriks at all | Add `ProductionOrdersTargetSchema` + executor (P5.1-follow-up) |
| G2 | `CustomsDeclarationLine.IsPreferentialOrigin` missing | `BG` vs `BG no pref.` is a duty-calculation distinction; we'd lose it | Add bool + migration; transform `UL` value `.* no pref.` → country="BG", pref=false |
| G3 | `ProductionOrderMaterial.PreAssignedMRN` missing | File pre-assigns which batch each WO consumes; LON would FEFO-pick instead | Add optional `PreAssignedMRN` + `PreAssignedBatchNumber`; `CreateMaterialIssueCommand` honours it over FEFO |
| G4 | `UnitOfMeasure.Code = 'STK'` missing | 81/259 items use it | Seed alongside existing UoMs |
| G5 | `Warehouse.Code = '222'` missing | All WOs and receipts target it | Seed OR map to `WH-TEK-VN` |
| G6 | `ProductionOrder.CustomerPartnerId` missing | `Firma=100` is the customer (LON authorization holder's client), but our `Partner` linkage is supplier-skewed | Add `CustomerPartnerId` nullable FK to Partners |

**Soft gaps (data bloats `Notes` or is derivable):**

| # | Gap | Workaround | Fix if clean |
|---|---|---|---|
| S1 | `ProductionOrder.CustomerOrderNumber` (`222-2026/10`) | `Notes` | New column |
| S2 | `ProductionOrder.WeekNumber` | Derivable from PlanBeginn | New column |
| S3 | `CustomsDeclaration.CMRNumber`, `ClosingNumber`, `CommercialInvoiceNumber` | `SpecialRemarks` | New columns (cheap) |
| S4 | `Item.ColorCode`, `Item.Size` | Parse from `Item.Code` | Variants model (heavy) |
| S5 | `BOMLine.EfficiencyFactor` (EFF=0.8934) | Use `ScrapPct = (1-EFF)*100` | New column (cheap) |
| S6 | Area `m²` on CustomsDeclarationLine | — | New column (low value unless reported) |
| S7 | `CustomsDeclaration.TotalGrossWeight / TotalNetWeight` | Sum from lines | New columns (cheap) |

---

## 4. Practical import strategy (with today's P5.1 wizard)

**What works out of the box after splitting + a small pre-seed:**

1. Upload `KW12_Faktura.xlsx` (split into 3 files per MRN) → target `CustomsDeclarations` with D7B3/D920/D938 as declarationNumber, procedureCode `4200`, partnerCode `TEXPORT-AT`.
   - Blocked by G2 origin preferential flag → can TRANSFORM `UL` to strip `no pref.` suffix at import time; preferential info is dropped (temporary).
2. Upload the same file (after declaration landed) → target `Receipts` with header defaults for `warehouseCode=222`, `partnerCode=TEXPORT-AT`, MRN per row.

**What needs backend work first:**

1. **Matriks import** → need a new `ProductionOrders` target (G1) + `PreAssignedMRN` field (G3). Without those, we can dump the file but can't commit a real production order.

---

## 5. Recommended follow-up task list

1. **G4, G5** (15 min) — seed `UnitOfMeasure.Code='STK'` + `Warehouse.Code='222'` (or map 222 → WH-TEK-VN in the import).
2. **G2** (~2 h) — add `CustomsDeclarationLine.IsPreferentialOrigin` column + migration + field-extracting transform rule `SPLIT:suffix`  that emits 2 fields (country, pref). Then plug into schema.
3. **G1 + G3 + G6** (~1 day) — new `ProductionOrdersTargetSchema` + executor that groups rows by WorkOrder root (creates parent + variant children), adds `PreAssignedMRN`/`PreAssignedBatchNumber` to `ProductionOrderMaterial`, creates `CustomerPartnerId` nullable.
4. **S1–S3, S5, S7** (batch, ~3 h) — add small fields to existing tables in one migration.
5. **UAT corpus** — once G1–G4 land, we have a ~7k-line production order set we can use to stress-test issue/release/bulk-issue flows. Ideal for the expert to validate on real-shape data.

---

## 6. Test data we can harvest right now (regardless of gaps)

- **134 Items catalog rows** — can be imported today via the existing Items target.
- **1 supplier Partner + 1 customer Partner** — via Partners target.
- **3 draft CustomsDeclarations** — once G2 transform is added OR we accept losing preferential-origin fidelity.

These alone give us a rich catalog for UAT of inventory / master-data features. Production-order features are blocked on G1+G3.

---

## 7. What we actually tried on VPS (2026-04-19)

Attempted real imports via the P5.1 wizard to validate the gap findings empirically.

### ✅ Worked

1. **Seed `UoM=STK`, `Warehouse=222`, `Partner=TEXPORT-AT`, `Partner=FIRMA-100`** via direct admin API calls.
2. **Bulk-import 138 NEW items** via `/api/import/sessions` → target `Items`, LOOKUP `UnitsOfMeasure.Code` on the UoM column. All committed atomically.
3. **Dry-run Faktura → CustomsDeclarations** (116 lines for MRN D7B3) passed with 0 errors after seeding all supporting data (`UoM=KO` was a missed seed, added on the fly).

### ❌ Blocked empirically

- **G7 — Soft-deleted legacy items break LOOKUP** (NEW, discovered). Phase-3 migration left 121/259 of the KW12 codes in `IsDeleted=1` state. The unique index on `(TenantId, Code)` made the Items import's duplicate check surface "already taken" while the tenant query filter hid them from LOOKUP. Fix options: (a) migration should have set `IsDeleted=0`; (b) Items import should upsert/skip soft-deleted rows; (c) add an "undelete" import mode. Workaround today: manual SQL `UPDATE Items SET IsDeleted=0 WHERE Code IN (...)`.
- **G8 — `UoM` POST endpoint ignores `isActive`** (NEW). `POST /api/masterdata/uom` always creates `IsDeleted=true` regardless of payload; GET then filters it out. Cost me 15 min of confused debugging. One-liner fix in `MasterDataController.CreateUnitOfMeasure`.
- **G9 — CustomsDeclarations executor doesn't pre-validate MRN uniqueness** (NEW). The existing D7B3 declaration from prior P2.3 tests caused a SaveChanges 500 because the executor only checks `DeclarationNumber` dupe, not the `(TenantId, MRN)` unique index. Dry-run said committable; commit threw SQL 2601. Need to extend the executor's pre-check to cover MRN.
- **G2 — Preferential origin flag absent.** Workaround today: CSV pre-process strips `" no pref."` suffix (column `Pref` emitted but ignored at import time). Information loss until we add the column.

### Not attempted today

- **G1 — Matriks import to ProductionOrders**: target doesn't exist in P5.1.5; Matriks can't be imported without new code. See §5 follow-up item 3.
- **Receipt import after the declaration**: dependent on G9 fix.

### State of VPS after these experiments

- Added seeds: `UoM=STK`, `UoM=KO`, `Warehouse=222` (id `e30888b3-…`), Partners `TEXPORT-AT` + `FIRMA-100`.
- 138 new items in TEKSPORT tenant (finished-good SKUs from KW12).
- 14 legacy items restored from soft-delete (not reverted; user can use them for UAT).
- No declarations / receipts / production orders were created.

These are intentional UAT seed artifacts and can be kept or cleaned up later.

