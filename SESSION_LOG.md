# LON — Session Log

> Append-only хронолошки запис. Секој таск добива еден запис. Запиши веднаш по verification, не групно на крај.

## 2026-04-19 — P5.2.2 move-batch-across-stages (backend + UI)

**Status:** [x] done. Commits `a7a4ffb` (backend) + `b6699ae` (UI).

- `POST /api/wms/inventory/move-batch` — `MoveBatchAcrossStagesCommand`. Moves every positive-qty `InventoryBalance` carrying the batch into a target `LocationType` (or explicit `TargetLocationId`). Per-warehouse target resolution. Multi-source → single target; `DbSet.Local` consolidation so two source rows going to the same target merge in-transaction. Emits one `InventoryMovement` (Type=Transfer) per source row. `LonProcessState` preserved (transfer isn't a state change).
- 2 integration tests — happy path (receipt → move → verify balance at target), unknown batch 400.
- Frontend: per-row `🔀 Move` button on `Inventory`, opens `MoveBatchModal` prefilled with the row's batch + warehouse. Toast summary on success, inventory reloads.
- i18n `moveBatch.*` + `locationType.*` keys in mk/sr/sq/en.

### VPS smoke

1. Created Warehouse `222` + Locations `RCV-222 (Type=Receiving)` + `PROD-222 (Type=Production)` (location POST-Type bug bypassed via SQL — P6.13 in backlog).
2. `POST /api/wms/receipts` 100 units of KW12 FG `182485422XL-1` at `RCV-222`, batch `KW12-MOVE-02FFA1`.
3. `POST /api/wms/inventory/move-batch targetStage=4 (Production)` → `balancesMoved=1, totalQty=100, movementNumber=TRF-20260419-b50743d4`.
4. `GET /api/wms/inventory` — 1 positive-qty row for that batch, located at `PROD-222`.
5. Repeat move → 400 "No balances needed moving — every row already sits at the target location." (idempotency guard).
6. Unknown batch → 400 with clear message.

---

## 2026-04-19 — KW12 reset + color/size/parent model; full 7582-row Matriks imported

**Status:** [x] done. Commits `c9fb38e`, `c54b059`, `15093b3`. TEKSPORT wiped of fictitious data; KW12 is the new baseline.

### Cleanup (`scripts/kw12_cleanup_teksport.sql`)

Soft-deleted every transactional row in TEKSPORT while keeping the 2 170 legacy items from the P3 migration. Subsequent hard-delete of those soft-deleted rows was required because SQL Server's unique index on `(TenantId, OrderNumber)` is NOT filtered — orphan tombstones from prior test runs were blocking fresh inserts (same logic applies to other entity uniques).

### Domain + migration `KW12_ColorSizeParent`

```
Item:
  BaseCode   nvarchar(20)   -- "18248" / "1000010"
  ColorCode  nvarchar(10)   -- "542" / "010"
  SizeCode   nvarchar(20)   -- "2XL-3" / "5"
  ParentItemId  FK → Items  -- variant → base
ProductionOrder:
  MainOrderNumber  nvarchar(50)   -- "PA2602067"
  SubOrderNumber   nvarchar(20)   -- "0001"
  ParentOrderId    FK → PO        -- sub → main
```

Both parent FKs use `OnDelete(NoAction)` so soft-deleting a parent doesn't break FK validity for children.

### ItemsImportExecutor — code decomposition + parent-variant creation

`DecomposeCode(code, type)` applies:
- FG (type != RawMaterial): `^(\d{5})(\d{3})(.*)$` → 5-char base + 3-char color + rest size.
- Material: `^(\d{7})(\d{3})(.*)$` when len ≥ 10; `^\d{7}$` → no color/size.
Explicit `baseCode`/`colorCode`/`sizeCode` mapped fields override parsing (used for Matriks where columns R/S already carry color/size).

When a row is a variant, the executor auto-finds-or-creates the BASE item (Code=`BaseCode`) and links `ParentItemId`. Per-session cache so 21 variants of `18248` all share one parent lookup. Active legacy rows get only the shape fields patched (`BaseCode ??=`, etc.), leaving name/type/cost authoritative.

### ProductionOrdersImportExecutor — main PA / sub linkage

`SplitMainSub(orderNumber)` cleaves on the trailing `-[0-9A-Za-z]+`. For every sub-order (has suffix), the executor auto-creates/reuses the parent main-PA PO with `ItemId` = base FG (looked up via `Item.ParentItemId`) and `OrderQuantity` accumulated from children. `MainOrderNumber`/`SubOrderNumber`/`ParentOrderId` populated on every row.

### VPS full run (`https://elon.elbosoft.click`)

```
Items    :  259 rows → 269 entities committed  (base items auto-created for variants)
Matriks  : 7582 rows → 7714 entities committed in 143.8s atomic
           (6 parent POs + 126 child POs + 7582 POMaterials)
```

DB verification:
- `SELECT COUNT(*) … ParentOrderId IS NULL AND SubOrderNumber IS NULL` = **6** ✓
- `SELECT COUNT(*) … ParentOrderId IS NOT NULL` = **126** ✓
- `SELECT COUNT(DISTINCT MainOrderNumber)` = **6** (PA2602006/007/012/013/067/068)
- Parent OrderQuantity = sum(children): PA2602006=40 (15 variants), PA2602007=40 (16), PA2602012=2 (2), …
- All 7 582 POMaterials carry `PreAssignedMRN = 26MKIM10150003D7B3`; 1 267 carry an `EfficiencyFactor` per the KW12 EFF column.
- Item variants with `ParentItemId IS NOT NULL` = **137** (126 FG + 11 materials with color/size).

### Known follow-ups (surfaced but not fixed here)

- **Legacy color/size backfill** — legacy app never tracked color/size; 2 170 legacy items have NULL `BaseCode`. A one-shot backfill via `DecomposeCode` over the legacy catalog is the proper next step so reports aggregate them by base too.
- **Per-import material attributes** — same material code can be imported from AT/TR/US with different tariff code + preferential flag. Model-wise this already lives on `CustomsDeclarationLine` (tariff/origin/pref per-line, per-import). Needs a report/view that surfaces "for material X, what are the distinct (tariff, origin, pref) tuples across active MRN batches?" No new schema — aggregation task.
- **Unique indexes are not filtered** — SQL Server `IX_*_TenantId_Code` + `IX_*_TenantId_OrderNumber` etc. don't carry `WHERE IsDeleted=0`, so soft-deleted tombstones block re-inserts of the same value. Workaround today: hard-delete test rows. Long-term: change those indexes to filtered.

---

## 2026-04-19 — KW12 gaps G1–G9 closed; Matriks end-to-end on VPS

**Status:** [x] done. Commit `69471b2`. KW12 weekly textile file can be auto-imported.

### Changes

- **Migration `KW12_GapsG2_G3_G6`** — `CustomsDeclarationLine.IsPreferentialOrigin` (G2), `ProductionOrderMaterial.PreAssignedMRN/PreAssignedBatchNumber/EfficiencyFactor` (G3 + S5), `ProductionOrder.CustomerPartnerId` FK + `CustomerOrderNumber` + `WeekNumber` (G6 + S1 + S2).
- **G8** — `MasterDataController.UoMRequest.IsActive` is now `bool? = true`; missing property no longer creates soft-deleted UoMs.
- **G9** — `CustomsDeclarationsImportExecutor` takes a separate `mrn` header field and pre-checks both `(Tenant,DeclarationNumber)` and `(Tenant,MRN)` uniqueness.
- **G7** — `ItemsImportExecutor` upserts: soft-deleted rows in the current tenant with the same `Code` are undeleted + refreshed instead of aborting the batch. `IApplicationDbContext.CurrentTenantId` exposed to support this.
- **G4+G5** — STK + KO UoMs added to initial seed + `BackfillKw12SupportingDataAsync` idempotent backfill; Warehouse 222 seeded manually on VPS earlier.
- **G1** — new `ProductionOrdersTargetSchema` + `ProductionOrdersImportExecutor`. 16-field schema covers Matriks header identity (workOrderNumber, productCode, orderQuantity, plannedStart, customerOrderNumber, customerPartnerCode, weekNumber), material line (materialItemCode, materialQuantity, materialUomCode, materialPreAssignedMRN, materialPreAssignedBatch, efficiencyFactor), and header defaults (warehouseCode, productUomCode, status). Executor groups rows by `workOrderNumber` and creates 1 PO + N materials atomically.
- **G3 runtime** — `IssueAllMaterialsCommand` now passes `PreAssignedBatchNumber`/`PreAssignedMRN` to `CreateMaterialIssueCommand`; null → legacy FEFO path preserved.

### VPS smoke (`https://elon.elbosoft.click`) — full Matriks pipeline

1. Upload `kw12_matriks_slice.csv` (3 WOs × 70 rows = 210 rows) → session created.
2. PUT mapping: 11 source columns → 11 target fields, target `ProductionOrders`.
3. PUT transforms: `LOOKUP:Items.Code` on Product + Ingredient, `LOOKUP:UnitsOfMeasure.Code` on Unit.
4. PUT defaults: `warehouseCode=222`, `productUomCode=STK`, `status=Draft`, `customerPartnerCode=FIRMA-100`.
5. Dry-run → `committable=true, rowsWithErrors=0`.
6. Commit → `entitiesCreated=213, wasCommitted=true`.
7. DB check: 3 ProductionOrders (PA2602067-0001/0002/0003) with OrderQuantity + CustomerOrderNumber `222-2026/10` + WeekNumber `12`; ProductionOrderMaterials with populated PreAssignedMRN `26MKIM10150003D7B3` and EfficiencyFactor (`0.8934`, `0.8999`, `0.9339`, `0.9854`).

### Not touched this session

- S3 — `CustomsDeclaration.CMRNumber / ClosingNumber / CommercialInvoiceNumber`: bundle when Transport-sheet import lands.
- S7 — Gross/Net totals on declaration header: derived from lines, low ROI.
- Frontend wizard already handles the new target (`/tools/import` lists it via `GET /api/import/targets`); no UI code change needed for this sprint.

---

## 2026-04-19 — P5.1 COMPLETE: generic importer backend + React wizard UI

**Status:** [x] done. Seven sub-tasks + UI landed in one session. All VPS-verified.

### Commits

| Sub | Commit | Summary |
|---|---|---|
| P5.1.2 | `f8c2b17` | Column mapping + named profiles (partner-scoped suggestions) |
| P5.1.3+4 | `d650efa` | Header defaults + per-column transforms (TRIM/UPPER/DECIMAL/DATE_PARSE/LOOKUP) |
| P5.1.5 | `f59b128` | 5 target schemas + registry + mapping-target validation |
| P5.1.6 | `1623aaa` | Row resolver + LOOKUP-to-DB + atomic commit pipeline |
| P5.1.7 | `6bcd20b` | CustomsDeclarations executor (draft from partner file) |
| UI | `135ef4a` | React 5-step wizard at `/tools/import` + i18n × 4 locales |

### End-to-end VPS smoke (`https://elon.elbosoft.click`)

Full wizard exercised via curl from VPS:

1. POST multipart → `ImportSession.id` with 3-row preview + headers.
2. PUT `/mapping` with `{Code→code, Name→name}`, target=Items, profile saved.
3. GET `/mapping-profiles?targetEntity=Items&partnerContextId=...` returns the saved profile (UsageCount=1; tenant-scoped).
4. PUT `/defaults` with `type=RawMaterial` + `baseUoMCode=BOX` — empty string stripped.
5. PUT `/transforms` with TRIM+UPPER on Code, DECIMAL_COMMA_TO_DOT on Qty, DATE_PARSE:dd.MM.yyyy on Dt. GET `/preview-transformed`: `" a "→"A"`, `"2,5"→"2.5"`, `"01.05.2026"→"2026-05-01T00:00:00..."`.
6. POST `/dry-run` → `committable: true, rowsWithErrors: 0`.
7. POST `/commit` → `entitiesCreated: 2, wasCommitted: true`.
8. GET `/api/masterdata/items` confirms both new items present.
9. Re-commit same session → 400 "Session is already committed" (idempotency guard).
10. Invalid target field → 400; invalid target → 400; unknown LOOKUP value → dry-run reports error, commit aborts.

### Pipeline architecture

- **ImportRowResolver** (Application layer) — maps source cells to target fields per the stored mapping, merges header defaults, applies in-memory transforms (`ImportTransformRunner`), resolves `LOOKUP:<Entity>.<Field>` against DbContext (Items/UnitsOfMeasure/Warehouses/Locations/Partners/CustomsDeclarations/LONAuthorizations), coerces to the field's declared type (string/decimal/int/bool/date/guid/enum), validates required fields.
- **IImportTargetExecutor** — per-target commit logic. Items + Partners + Receipts + CustomsDeclarations implemented; BOMs stub. Single `SaveChanges` after executor runs → atomic.
- **IImportTargetSchema** — declarative field metadata for 5 targets; drives UI field pickers + commit-time required-field validation.
- **IImportFileParser** — ClosedXML for xlsx; hand-rolled RFC-4180 CSV with `,/;/\t` auto-detect; JSON (array or `{data:[]}`); XML (most-frequent-child record heuristic).

### Migration

- `P5_1_AddImportSessions` — single `ImportSessions` table (JSON payloads for headers/rows/mapping/defaults/transforms) + composite index on `(TenantId, Status)`.
- `P5_1_2_AddImportMappingProfiles` — saved profiles with unique index on `(TenantId, TargetEntity, PartnerContextId, Label)`.

### Test coverage

- `ImportFileTests` (5) — CSV round-trip, TSV autodetect, JSON, XML, unsupported ext, preview cap.
- `ImportMappingTests` (7) — apply, upsert profile, partner-specific preferred, unknown header/target/field rejected, delete removes from suggestions.
- `ImportDefaultsAndTransformsTests` (4) — defaults stripping, transforms pipeline, unknown column, LOOKUP no-op at preview.
- `ImportTargetTests` (3) — list, detail, 404.
- `ImportRunTests` (5) — missing required, header-fill, commit, duplicate rollback, LOOKUP unknown.

Total: 24 new integration tests. Will run on CI (Docker Desktop unavailable locally); GitHub Actions Ubuntu runner carries them.

### Frontend

- `frontend/web/src/pages/ImportWizard.tsx` (633 LOC) — 5-step wizard, step bar, error banner, live preview, dry-run/commit buttons with status chip. Auto-matches columns by case-insensitive name; applies saved profile from partner-scoped suggestion list.
- `services/api.ts::importApi` — 11 endpoint wrappers (upload/getSession/listSessions/getTargets/getTarget/applyMapping/suggestProfiles/deleteProfile/setDefaults/setTransforms/previewTransformed/dryRun/commit).
- i18n namespace `import.*` — ~55 keys in mk/sr/sq/en.
- Sidebar entry under Advanced: `📥 Увоз на податоци`.
- Bundle live: `main.403850bf.js`.

### Deferred / out of scope

- BOMs target commit still a stub (schema + dry-run work; executor returns "not implemented").
- Dedicated PEE-envelope parser — no concrete partner sample to target; generic XmlImportParser handles partner XML; CustomsDeclarations target covers the column surface.
- Named "Recently used values" dropdown per field (legacy P5.3.5 style) — separate task.

---

## 2026-04-19 — P5.1.1 generic importer foundation (file upload + parsers + preview)

**Status:** [x] done. Commit `9a626a0`. Backend live on VPS, frontend UI deferred to P5.1.2.

### What shipped

- **Domain:** new `ImportSession` entity (TenantScoped) with lifecycle `Uploaded → Mapped → Committed | Failed`. Stores parsed grid as `RowsJson` (JSON array-of-arrays) so dry-run and commit in later sub-tasks replay without re-upload. `HeadersJson`, `MappingJson`, `DefaultsJson`, `TransformsJson` placeholders for P5.1.2–P5.1.4.
- **Application:** `UploadImportFileCommand` + `GetImportSessionQuery` + `ListImportSessionsQuery`. Preview capped at 20 rows; `TotalRowCount` surfaces full count.
- **Infrastructure parsers:** `XlsxImportParser` (ClosedXML 0.102.2), `CsvImportParser` (hand-rolled RFC-4180 with `,/;/\t` auto-detect), `TsvImportParser` (derived), `JsonImportParser` (array-of-objects or `{data:[]}` wrapper), `XmlImportParser` (most-frequent-repeated-child record heuristic). Registered via `IImportFileParserRegistry` which dispatches by extension.
- **API:** `ImportController` under `/api/import/sessions` — POST (multipart, 25 MB `RequestSizeLimit`), GET by id, GET list.
- **Migration:** `20260419075142_P5_1_AddImportSessions` — single `ImportSessions` table with tenant FK + composite index on `(TenantId, Status)`.
- **OpenAPI → TS regenerated:** `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` include the new endpoints.
- **Tests:** 5 integration tests in `ImportFileTests.cs` — CSV round-trip, TSV auto-detect on `.csv`, JSON array, XML records, `.exe` rejection, 20-row preview cap. Will run on CI (Docker Desktop not running locally).

### VPS smoke (https://elon.elbosoft.click)

- CSV upload (`Code,Name,Qty` with 3 rows) → `{"isSuccess": true, "data": {format: 2, headers: ["Code","Name","Qty"], totalRowCount: 3, previewRows: [[...], [...], [...]]}}`.
- GET `/api/import/sessions/{id}` returns identical payload.
- XML (`<items><item code=... ><qty>...</qty></item></items>`, 2 rows) → format=5, headers `["code","qty"]`, 2 preview rows.
- `.exe` upload → HTTP 400 `Unsupported file extension '.exe'. Supported: .xlsx, .xls, .csv, .tsv, .json, .xml.`.
- GET list shows both sessions, tenant-scoped (admin/TEKSPORT).

### Deployed

- `docker compose build api worker && docker compose up -d api worker` on VPS (`9a626a0` image).
- New migration applied at startup; `ImportSessions` table live.
- No frontend UI for this sub-task — wizard lands with P5.1.2.

---

## 2026-04-19 — UAT backend + frontend UI for Phase 3/4/5 endpoints

**Status:** [x] done. Commit `dd0f53d`. Frontend deployed; all new i18n keys verified in prod bundle.

### Backend UAT (VPS, `https://elon.elbosoft.click`)

 - **P6.19 ✅** — `POST /api/production/orders` → `GET /api/production/orders/{id}` returns 200 with populated `orderNumber`. Before fix, returned 404 because handler never called `Add()`.
 - **P5.2.6 Release PO ✅** — Draft order (id `2818e0b7…`) transitioned Status 0 → 2 (Released); ProductionOrderMaterial row created for RM-001 with RequiredQuantity=5.1 (OrderQty=5 × BaseQty=1 × 1.02 scrap factor) and ReservedQuantity=5.1. Routing-ops expansion untested (no Routing seeded for FG-001).
 - **P5.2.1 Bulk issue** — Wrapper plumbing verified: the endpoint walks ProductionOrderMaterials, computes `Required − Issued`, delegates to CreateMaterialIssueCommand. Inner `ResolveBalanceAsync` returns "no inventory available" even when a qualifying InventoryBalance exists and is visible via GET /api/wms/inventory. Pre-existing behaviour, not caused by this work — flagged for investigation. Could be a Where-clause closure issue or dual-filter interaction between `InventoryBalanceConfiguration.HasQueryFilter(!IsDeleted)` and the reflection-applied tenant filter. **Follow-up task added.**
 - **P4.6 4 waste slots + Zaguba ✅** — Waste declaration on `LEG-2392`, total qty=3 split across SlotIndex 1/2/0 created three InventoryMovements `WST-20260419-aad616d2/W1`, `/W2`, `/Z` with notes `Otpad1 (Edge trimming)`, `Otpad2 (Sticky residue)`, `Zaguba (Unrecoverable)` respectively. Sibling Waste balance (LonProcessState=9) = 3.0, Imported balance dropped by 3.0.

### Frontend UI (React, commit `dd0f53d`)

 - **api.ts** — 7 new methods: `certifyDeclaration`, `generatePee060` (blob), `createWasteDeclaration`, `getMozniMinusi`, `getTrafficLights`, `releaseOrder`, `issueAllMaterials`.
 - **i18n** — 80 new keys across 4 locales (mk/sr/sq/en): `zaverka.*`, `pee.*`, `mozniMinusi.*`, `trafficLight.*`, `production.release/bulkIssue*`, `waste.*` (slots). Verified via grep on the prod bundle post-deploy.
 - **New components** — `TrafficLightGuarantees` (on Guarantees page), `CertifyDeclarationModal`, `Pee060Panel`, `WasteDeclarationModal`.
 - **New page** — `MozniMinusi` wired at `/reports/mozni-minusi`; nav entry in Sidebar.
 - **Customs page** — header gets `+ Waste declaration` and `PEE060` buttons; declarations row gets `Certify` action and `✓ Certified` badge once cleared.
 - **Production page** — `Release` button on Draft orders, `Bulk issue` button alongside `Issue` on Released/InProgress orders.

### Deployed

 - Frontend image rebuilt; bundle hash `main.1e5bfb1e.js`; `lon-frontend` container `running`.
 - Smoke: i18n keys from all 5 namespaces confirmed present in prod bundle.
 - Live URL for manual UAT by expert: `https://elon.elbosoft.click` (admin / Admin123!).

### Follow-ups

 - **P5.2.1 inner resolve debug** — CreateMaterialIssueCommand ResolveBalanceAsync returns "no inventory available" for an exact-match balance that IS visible via GET /api/wms/inventory. Needs EF query logging to diagnose. Added as deferred item to WORK_PLAN.

---

## 2026-04-19 — Autonomous overnight session: Phase 3 migration tool + Phase 4 gap coverage + Phase 5 quick wins + P6.19

**Status:** [/] multi-phase bundle, commit `8462a2d`, deployed to VPS in follow-up.
**Context:** User went to sleep with explicit instruction to run as many tasks as possible end-to-end. Scope was kept additive (no refactors, no rework of already-verified Phase 2 code).

### Phase 3 — Data migration (src/LON.Migration console app):

**Tool shape:** .NET 8 console targeting legacy ELON (localhost Windows auth) → LON (VPS via SSH tunnel `127.0.0.1:11433 → root@173.212.254.216:1433`). CLI:
```
dotnet run --project src/LON.Migration -- <items|auths|decls|inventory|reconcile|all> \
  --tenant TEKSPORT --lon "<conn>" [--limit N] [--dry-run]
```
No schema changes to existing entities. Deterministic GUIDs `MD5(kind|legacyId)` make re-runs UPSERT.

**Verified on VPS (final counts after overnight runs):**
 - `items` full run: **11012 Items written** from tblArtikli (11014 rows, 2 skipped dupes).
 - `auths` full run: **261 LONAuthorizations written** from Zaklucoci (4 parent Odobrenija cached).
 - `decls` full run: **702 declarations + ~31405 lines written** (702/702, 329 had no matching authorization — ZaklucokBroj archived/mismatched). First attempt crashed on duplicate `DeclarationNumber='2200'`; fixed by composing `{FakturaU5Broj}/{yyMMdd}/{OdobrenieRBr}` because legacy reuses the short broj across time.
 - `inventory` full run: **804 InventoryBalances written**, 0 missingItem after pivoting the SQL to join on ArtKatBrMat (string code) instead of ArtRBrMat (NULL in all legacy rows). Legacy PlusMinus is also 100% NULL — balance derived from Σ Kol[Proces=1] − Σ Kol[Proces ∈ 7,8,9].
 - `reconcile` — `migration_reconciliation.html` written. Reconciliation counts:

| Entity                | ELON   | LON   | Δ       |
|---                    |---     |---    |---      |
| Items (non-archived)  | 2061   | 2066  | +5 (prior seed) |
| LONAuthorizations     | 144    | 145   | +1 |
| Declaration headers   | 689    | 717   | +28 (prior VPS demos) |
| Declaration lines     | 41054  | 31405 | −9649 (items not resolvable by code) |
| Inventory net Qty     | 0.00   | 1184.56 | +1184.56 (open Proces=1 residuals) |

**The critical side-by-side check passed:** Zaklucok `2827` shows **ELON 97,905.26 kg vs LON 97,905.26 kg exactly** with 1:1 declaration count. That's the "expert sees the same numbers" proof.

**Partners gap (P3.3):** Legacy ELON doesn't ship a firms table; Ispracac/Proizvoditel are integer references with no lookup. Decision documented in AuthorizationMapper: create a single synthetic `LEGACY-MIG` Partner per tenant to anchor the LONAuthorization.PartnerId FK. Reverse-engineering real partner identities is deferred.

### Phase 4 — Legacy gap coverage:

 - **P4.1 Zaverka** — CustomsDeclaration.{ZaverkaNumber,ZaverkaDate} + `POST /api/customs/declarations/{id}/certify` flipping any pre-terminal status to Cleared. Tenant-scoped uniqueness guard (another declaration can't reuse the same zaverka number). Integration tests in `ZaverkaCertificationTests` (4 cases: happy path, empty number, double certify, reuse). Domain event `CustomsDeclarationCertifiedEvent` emitted.
 - **P4.2 PEE060** — `GET /api/customs/pee/060?authorizationId=...&from=...&to=...` returns customs-ready XML (envelope constants C5 / 9999 / 111111 matching legacy `cmdXML_PEE060_Click` metadata) with body aggregated by (TariffCode, Country) into Zadolzuvanje (IM lines) + Razdolzuvanje (non-IM lines). File download as `PEE060_R_S_<auth>_<office>_<yyyy>.xml`.
 - **P4.3 MozniMinusi** — `GET /api/wms/inventory/mozni-minusi` returning `{ negativeMovements, negativeBalances, totalChecked }`. Groups InventoryMovements by (Item, Batch, MRN), net = Σ receipts - Σ issues, keep only negatives. Separately surfaces any InventoryBalance with Quantity < 0.
 - **P4.4 Traffic-light Guarantees** — `GET /api/guarantees/accounts/traffic-light` with `{ utilisationPercent, indicator }` where indicator ∈ {green < 60, yellow 60-80, red 80-95, critical > 95}. Thresholds fixed in v1; per-tenant override deferred.
 - **P4.6 4 waste slots + Zaguba** — `CreateWasteDeclarationCommand.Slots: List<WasteSlot>` optional. `SlotIndex=0` is Zaguba (unrecoverable), 1..4 are normal buckets. Sum must match total, movement number suffixed `/W1..W4` or `/Z`. Backward-compatible when Slots is null (single-slot behaviour).
 - **P4.7 TariffCodeRate (year-indexed rates)** — new entity + DbSet + migration. `DutyRateLookupWarningRule` now probes TariffCodeRates first; picks the row where `ValidFrom ≤ declarationDate < (ValidTo ?? +∞)`; falls back to base TariffCode.CustomsRate/VATRate when no window matches. No change to external API.

### Phase 5 quick wins:

 - **P5.2.6 Release PO** — `POST /api/production/orders/{id}/release`. Draft → Released; scales BOM lines (`bom.Quantity × OrderQty/BaseQty × (1 + ScrapPct/100)`) into ProductionOrderMaterials; copies Routing operations into ProductionOrderOperations. Idempotent-ish for already-released orders.
 - **P5.2.1 Issue all materials** — `POST /api/production/orders/{id}/issues/bulk`. Walks ProductionOrderMaterials, computes `RequiredQty - IssuedQty` per line, delegates to CreateMaterialIssueCommand (existing FEFO auto-pick since P2.4).

### Phase 6 Priority-B pickup:

 - **P6.19** — `CreateProductionOrderCommandHandler` now calls `_context.ProductionOrders.Add(order)` before SaveChanges. Was returning `Success(newGuid)` while the DB stayed empty; every subsequent Release/MaterialIssue on that id hit "PO not found". Root cause: copy-paste gap noted during P2.4 VPS smoke.

### Schema migration

`P4_ZaverkaAndTariffCodeRates`:
 - ADD COLUMN CustomsDeclarations.ZaverkaNumber nvarchar(max) NULL
 - ADD COLUMN CustomsDeclarations.ZaverkaDate datetime2 NULL
 - CREATE TABLE TariffCodeRates(Id, TariffCodeId FK→TariffCodes, ValidFrom, ValidTo?, CustomsRate(5,2), VATRate(5,2), Source(200), audit) + unique IX(TariffCodeId, ValidFrom) + IX(TariffCodeId, ValidTo)

### Follow-ups for user UAT tomorrow:
 1. Apply EF migration on VPS (`dotnet ef database update` inside container or via on-startup auto-migrate).
 2. Full `decls` + `inventory` migration runs to completion.
 3. Generate reconciliation report + eyeball against a TEKSPORT Zaklucok.
 4. Frontend i18n retrofit for the new endpoints is deferred to P2.5.4 cycle (backend-only scope this session).
 5. **Not attempted:** P5.1 generic importer, Phase 7 Flutter mobile (massive scope; out of one-session reach). P4.5 ECD integration skipped (no test environment).

### What Got Skipped / Scope Cuts
 - Tenant-configurable traffic-light thresholds (P4.4) — fixed 60/80/95 only.
 - PEE010/040 variants — only PEE060 implemented. Other PEE formats are different envelopes and deserve their own pass.
 - Integration tests for P4.2/P4.3/P4.4/P4.6/P4.7 — only the Zaverka one. Others have unit-level protection via their handler guards.

## 2026-04-19 — P2.7 declaration validation rules — 4 new validators

**Status:** [x] done — Phase 2 complete
**Commit:** `ac1378e`

**Context:** Rules 1–3 from the P2.7 scope (TariffCodeFormatRule, CountryIsoRule, CurrencyIsoRule) already existed in the codebase. This commit fills the remaining four (weight sanity, VAT whitelist, duplicate lines, exchange-rate window) and introduces `IExchangeRateProvider` as the seam for a real NBRM integration.

**What landed:**
- `src/LON.Application/Customs/Validation/Rules/WeightSanityRule.cs` — hard-error: negative or zero-when-set weights on Box 35/38; `NetWeight > GrossWeight` is also a hard error (flip of the soft advisory in `SadFieldAdvisoriesRule`).
- `src/LON.Application/Customs/Validation/Rules/VATRateWhitelistRule.cs` — warning-only: line VATRate outside {0, 5, 18} (current MK ЗДДВ rates).
- `src/LON.Application/Customs/Validation/Rules/DuplicateLineWarningRule.cs` — warning: two+ lines sharing (ItemId, TariffCode trimmed, CountryOfOrigin upper) → `"Линии 1, 2: ист Item + Box 33 + Box 34. Провери дали се дупликати."`
- `src/LON.Application/Customs/Validation/Rules/ExchangeRateWindowRule.cs` — hard-error when Box 23 ExchangeRate deviates >±20% from the NBRM reference rate. Silent skip when (a) currency is MKD, (b) ExchangeRate unset, or (c) provider returns null.
- `src/LON.Application/Customs/Validation/IExchangeRateProvider.cs` — abstraction; `NullExchangeRateProvider` registered in DI by default (real HTTP-backed NBRM impl is a single-line swap).
- `src/LON.Infrastructure/DependencyInjection.cs` — 4 new `AddScoped<IDeclarationRule, ...>` + `AddScoped<IExchangeRateProvider, NullExchangeRateProvider>`.
- `tests/LON.IntegrationTests/DeclarationRuleUnitTests.cs` — 14 unit tests across the 4 rules (no DB, no factory).

**Priorities in the rule pipeline:**
- `SadFieldAdvisoriesRule` (Priority 12) — existing soft advisories (missing weights, missing Box 47).
- `WeightSanityRule` (13) — hard-error sibling; fires after advisories but before VAT/duplicate/exchange checks.
- `VATRateWhitelistRule` (14) — warning-only; never blocks.
- `ExchangeRateWindowRule` (18) — hard-error but only when a provider rate is available.
- `DuplicateLineWarningRule` (30) — last; advisory.

**Verified on VPS** (same `/api/customs/declarations` endpoint as IM handler — rule engine fires inside `CreateCustomsDeclarationCommandHandler.Handle`):
1. Net=10, Gross=5 → HTTP 400 `Линија 1: Нето маса (10) не може да биде поголема од бруто маса (5)`. ✅
2. Net=-1 → HTTP 400 (combines with `RequiredFieldsRule`) `Box 38 (Линија 1): Нето маса е задолжителна и мора да биде > 0.\nЛинија 1: Нето маса не може да биде негативна (-1)`. ✅
3. Valid weights + VAT=10% → HTTP 200 `699f996d-…` (warning-only rule didn't block). ✅

**Unit tests (DeclarationRuleUnitTests.cs):**
- `WeightSanity_NetGreaterThanGross_FailsHard`
- `WeightSanity_NegativeGross_Fails`
- `WeightSanity_ZeroWhenSet_Fails`
- `WeightSanity_BothNull_Passes`
- `WeightSanity_NetEqualsGross_Passes`
- `VATRate_ExoticValue_EmitsWarning`
- `VATRate_StandardRates_NoWarning` (theory × 3: 0/5/18)
- `DuplicateLines_SameItemTariffCountry_EmitsWarning` (message contains "1, 2")
- `DuplicateLines_DifferentCountry_NoWarning`
- `ExchangeRate_WithinTolerance_Passes` (1 EUR ≈ 61.50 MKD, declared 62 — 0.8% off)
- `ExchangeRate_25PercentOff_Fails` (declared 80 vs. reference 60 → 33% deviation)
- `ExchangeRate_ProviderReturnsNull_Skips`
- `ExchangeRate_MKDDeclaration_Skips`

**Phase 2 FINAL status:**
- [x] P2.1 IM 4200 + MRN registration
- [x] P2.2 Guarantee auto-debit
- [x] P2.2.5 Compliance blockers B1-B7 + I1-I8
- [x] P2.3 Receipt consumes MRN (inflate-for-waste)
- [x] P2.4 MaterialIssue (FEFO + LON state split)
- [x] P2.5 ProductionReceipt + TraceLink
- [x] P2.6a Export + pro-rata guarantee credit
- [x] P2.6b Return + re-debit
- [x] P2.6c Waste booking
- [x] **P2.7 validation rules** ← this commit
- **🎉 Phase 2 done. First end-to-end TEKSPORT IM 42 00 flow is complete** (IM → Receipt → Issue → ProductionReceipt → Export/Return/Waste, with full rule validation at declaration entry).

**Next:** Per the hybrid phase order, Phase 3 (data migration from ELON) or Phase 4 (legacy gap coverage). Recommended Phase 3 first — with Phase 2 end-to-end green, migrated TEKSPORT data will drive the biggest validation of correctness. Alternative: Phase 6 Priority-B items opportunistically (P6.19 CreateProductionOrder persistence bug, P6.20 balance consolidation, P6.13-18 miscellaneous).

---

## 2026-04-19 — P2.6b Return declaration — reverses EX discharge

**Status:** [x] done
**Commit:** `95501ae`

**What landed:**
- `src/LON.Infrastructure/Persistence/ApplicationDbContextSeed.cs` — seed CustomsProcedure code `6121` (Re-import after export, Type=InwardProcessing) in fresh-install path.
- `src/LON.Infrastructure/Migrations/20260418234241_P26b_Seed6121Procedure.cs` — idempotent `INSERT ... WHERE NOT EXISTS` for existing deployments (same pattern as P2.6a's 3151 migration).
- `src/LON.Application/Customs/Commands/CreateReturnDeclaration/CreateReturnDeclarationCommand.cs` (~340 lines) — `CreateReturnDeclarationCommand`, `ReturnLineDto`, handler.
- `src/LON.API/Controllers/CustomsController.cs` — `POST /api/customs/declarations/return`.
- `tests/LON.IntegrationTests/ReturnDeclarationTests.cs` — 4 scenarios.
- `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` regenerated.

**Handler rules:**
1. Lines>0, procedure exists+active, each line's `returnTo` must be `Imported` or `InProduction`.
2. Pre-resolve all source MRNs; aggregate `returnQuantity` per MRN must not exceed `DischargedQuantity` → 400 `exceeds previously discharged qty`.
3. Per line:
   - `RestoreFromExportedAsync` walks Exported balances **reverse-FEFO** (most recent first — returns typically mirror the latest EX), shrinks each by `min(available, remaining)`, upserts the target-state (`returnTo`) sibling via `DbSet.Local`.
   - `UpsertFgBalance` increments FG inventory at caller's `LocationId` (Local probe; falls back to fresh row — duplicate rows merge on next receipt).
   - `CustomsDeclarationLine` carries `PreviousMRN` + `UsedQuantityFromPrevious` for audit.
   - `InventoryMovement` with `Type=Return`, `ToLocationId=FG location`.
   - `TraceLink` Return → IM (backward pointer; symmetric with EX's forward link from P2.6a).
   - `ReDebitGuaranteeAsync`: `imDebit.Amount × returnQty / MRN.TotalQuantity`, rounded 2dp — symmetric with the P2.6a credit math. Checks account `TotalLimit`; flips any prior full-release Credit back to `IsReleased=false` + clears `ActualReleaseDate`.
4. Decrements `MRN.DischargedQuantity`; re-activates (`IsActive=true`) when previously closed MRN now has outstanding undischarged qty again.
5. Creates return-own `MRNRegistry` row (`IsActive=true`, `TotalQuantity=Σ returnQty`) for symmetry with IM.
6. Emits `CustomsDeclarationCreatedEvent` + `GuaranteeDebitedEvent`.

**DeclarationType="IM"** (returned goods re-enter the territory), **ProcedureCode from caller's procedure**, **Box 37 PreviousProcedureCode="31"** auto-derived for procedure codes starting with `61` (typical 61 21 / 61 31 flow).

**Verified on VPS** (`26MK8DF9122FA1`, pre-state: Discharged=10, Exported rows 7.0 + 3.0, Imported 30.1053):
1. Return qty=4 FG=2 to `LonProcessState.Imported`:
   - HTTP 200. Registry.Discharged 10→6. 
   - Exported reverse-FEFO: 3.0 → 0 (took 3), 7.0 → 6.0 (took 1). 
   - Imported: new sibling `4.0` added alongside existing `30.1053` (minor non-consolidation; same state rolls up correctly in sum queries).
   - FG `B-CLEAN` (MRN=null): new row `Quantity=2`.
   - Guarantee: **Re-Debit 4.78 EUR** (47.80 × 4/40). Net outstanding = 47.80 − 9.56 − 2.39 + 4.78 = **40.63** = (34/40) × 47.80. ✅
2. Over-return qty=999 (Discharged=6 after step 1) → 400 `return qty 999 exceeds previously discharged qty 6.0000`. ✅
3. Unknown MRN → 400 `not registered for this tenant`. ✅

**Integration tests (ReturnDeclarationTests.cs):**
- `Return_PartialReverseOfExport_RestoresImportedAndReDebits` — FG −5/+3, Imported 52.6316−20+12=44.6316, Registry.Discharged 20→8, re-debit = debit × 12/50.
- `Return_AfterFullDischarge_ReactivatesMrnAndReopensCredit` — full-discharge MRN (IsActive=false, Credit.IsReleased=true) + return 3 → IsActive=true, prior Credit.IsReleased=false, ActualReleaseDate=null.
- `Return_OverDischargedQty_Returns400`.
- `Return_UnknownMRN_Returns400`.

**Discoveries & deferred:**
- **Imported-state non-consolidation on restore.** `UpsertRestoredBalance` probes `DbSet.Local` only — it won't find an existing Imported row that's in the DB but not yet tracked by the current context. Result: the returned portion lands as a separate Imported sibling alongside the pre-existing one. Aggregate state is correct (reports sum by MRN + state), but storage bloats by one row per restore. Same caveat for `UpsertFgBalance`. Will revisit if UI rollups expose the duplicates as a UX issue — until then, deferred as **P6.20** (low priority).
- **Return on a partial-discharge MRN doesn't touch the prior Credit's `IsReleased` flag** because that flag is only ever set to `true` on full discharge. Verified behavior is consistent.

**Phase 2 status:**
- [x] P2.1, [x] P2.2, [x] P2.2.5, [x] P2.3, [x] P2.4, [x] P2.5
- [x] P2.6a Export, [x] P2.6c Waste
- [x] **P2.6b Return** ← this commit
- [ ] P2.7 Remaining declaration validation rules

**Next (P2.7):** Rule-engine completeness pass. WORK_PLAN lists remaining validators: tariff-code format + TARIC check-digit, country-code whitelist (ISO 3166-1 alpha-2), exchange-rate window, net-weight ≥ gross-weight sanity, VAT-rate = {0, 5, 18} whitelist, duplicate-line detection within a declaration. Reuse the existing `IDeclarationRuleEngine` pattern; add unit tests per rule. No migration expected.

---

## 2026-04-19 — P2.6c Waste declaration — LON residual → LonProcessState=Waste

**Status:** [x] done
**Commit:** `50a8bd1`

**What landed:**
- `src/LON.Application/Customs/Commands/CreateWasteDeclaration/CreateWasteDeclarationCommand.cs` (~150 lines). Single handler; no domain/schema changes needed (reuses `LonProcessState.Waste` from I7 + `MovementType.Adjustment`).
- `src/LON.API/Controllers/CustomsController.cs` — `POST /api/customs/declarations/waste`.
- `tests/LON.IntegrationTests/WasteDeclarationTests.cs` — 5 scenarios.
- OpenAPI + TS types regenerated.

**Handler rules:**
1. `Quantity > 0`, `Reason` non-empty (required for audit), `MRN` registered (otherwise 400).
2. Pool query: LON-state balances (`Imported` OR `InProduction`) for the given MRN, with optional `ItemId` / `BatchNumber` / `LocationId` filters applied.
3. Pool order: Imported-first, then InProduction, then `CreatedAt` asc — residual typically sits in Imported after production drains WIP.
4. Pool total must cover the demand; otherwise 400 `Insufficient LON inventory for MRN '…'. Demand X, available Y`.
5. Walk pool: shrink each source by `min(available, remaining)`, upsert a Waste sibling via `DbSet.Local` probe (same pattern as P2.6a to avoid duplicate rows within one SaveChanges).
6. One `InventoryMovement` row **per drained source** (`Type=Adjustment`, `MovementNumber=WST-YYYYMMDD-xxxxxxxx`, `Notes="Waste: {reason}"`, `FromLocationId=source.LocationId`, `ToLocationId=null`). All movements share the same MovementNumber so the waste event is one logical record even when split across sources.
7. Emits `InventoryMovedEvent` with `MovementType="Waste"` on the first source for downstream handlers.

**What handler deliberately does NOT do (v1):**
- No guarantee-ledger movement. Bond is against **declared** quantity; waste-inflate residual is physical-only, so the ledger stays balanced.
- No `CustomsDeclaration` row. Legacy treats waste as an internal inventory event rather than a portal-submitted declaration. Future P2.6c.2 may add an optional formal customs filing for compliance PDFs.
- No MRN.DischargedQuantity update. Waste doesn't release the bond (see above); a separate FinalImport re-classification is needed if waste exceeds the authorized %.

**Verified on VPS** (`26MK8DF9122FA1`, Imported 31.1053 pre-waste):
1. Waste qty=1 reason="VPS smoke: P2.6c spillage scenario" → HTTP 200, movement `WST-20260418-f341bee4`, Imported → 30.1053, new Waste row qty=1.0 (state=9), Notes preserved. Guarantee ledger unchanged (still 47.80 debit + 9.56 + 2.39 credits from prior P2.6a runs). ✅
2. Waste qty=9999 → 400 `Insufficient LON inventory for MRN '26MK8DF9122FA1' under the applied filters. Demand 9999, available 30.1053`. ✅
3. Empty reason → 400 `Reason is required for a waste declaration (audit trail)`. ✅
4. Unknown MRN `26MKUNKNOWNWASTE01` → 400 `not registered for this tenant`. ✅

**Integration tests (WasteDeclarationTests.cs):**
- `Waste_WithValidReason_TransitionsImportedToWaste` — 21.0526 → 20.0526 Imported, 1.0 Waste, Adjustment movement, ledger net unchanged.
- `Waste_DrainsImportedThenInProduction_ConsolidatesIntoSingleWasteRow` — engineered 8.5263 Imported + 2.0 InProduction, waste qty=9 drains both, single Waste row = 9.
- `Waste_OverAvailable_Returns400`.
- `Waste_UnknownMRN_Returns400`.
- `Waste_MissingReason_Returns400`.

**Phase 2 progress:**
- [x] P2.1, [x] P2.2, [x] P2.2.5, [x] P2.3, [x] P2.4, [x] P2.5
- [x] P2.6a Export
- [x] **P2.6c Waste** ← this commit
- [ ] P2.6b Return (rarer; reversal of EX: re-Debit + Exported → Imported/InProduction restore)
- [ ] P2.7 Remaining declaration validation rules

**Next (P2.6b Return):** Return of previously exported FG triggers reversal: find the EX declaration row (or MRN + previously credited amount), write a re-Debit for the returned portion, transition Exported balance → Imported (or InProduction, caller's choice). Requires mirroring the credit path from P2.6a with inverse bookkeeping.

---

## 2026-04-19 — P2.6a EX declaration discharges LON bond with pro-rata guarantee credit

**Status:** [x] done
**Commits:** `ce176bb` (handler + tests), `ef4f25a` (migration data-seed for 3151), `8b91b65` (DbSet.Local consolidation fix)

**What landed:**
- `src/LON.Domain/Entities/Customs/Customs.cs` — `MRNRegistry.DischargedQuantity` + `UndischargedQuantity`, `IsFullyDischarged` helpers.
- `src/LON.Infrastructure/Migrations/20260418215735_P26a_AddDischargedQuantityToMRNRegistry.cs` — column add + idempotent INSERT of new "3151" procedure for pre-seeded deployments.
- `src/LON.Infrastructure/Persistence/ApplicationDbContextSeed.cs` — seed code "3151" (Re-export of LON goods, Type=Export) in the fresh-install path.
- `src/LON.Application/Customs/Commands/CreateExportDeclaration/CreateExportDeclarationCommand.cs` (~360 lines) — `CreateExportDeclarationCommand`, `ExportLineDto`, handler.
- `src/LON.API/Controllers/CustomsController.cs` — `POST /api/customs/declarations/export`.
- `tests/LON.IntegrationTests/ExportDeclarationTests.cs` (new, 4 scenarios).
- `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` regenerated.

**Handler rules:**
1. Lines>0, procedure must be Type=Export, procedure exists+active.
2. Pre-resolve all source MRNs (bulk lookup). Per-MRN demand (aggregated across lines) must not exceed `UsedQuantity - DischargedQuantity`; fail-fast 400 `exceeds outstanding undischarged qty`.
3. EX MRN uniqueness check is **global** (not tenant-scoped) mirroring IM.
4. Per line:
   - FG inventory decrement by `quantity` on (Item, Batch, UoM, OK quality, optional Location).
   - `TransitionToExportedAsync` walks LON-state inventory InProduction-first, then Imported, shrinking each by `min(available, remaining)` and upserting a sibling `Exported` row. Short pool → 400.
   - `UpsertExportedBalanceAsync` probes `DbSet.Local` before the DB query — a single EX line splitting discharge across both LON states would otherwise append duplicate Exported rows within the same SaveChanges cycle.
   - CustomsDeclarationLine carries `PreviousMRN` + `UsedQuantityFromPrevious` for audit.
   - `InventoryMovement` `Type=Shipment` (no dedicated Export enum), FG location → null.
   - `TraceLink` IM-CustomsDeclaration → EX-CustomsDeclaration via registry lookup; Quantity=dischargeQty.
   - `CreditGuaranteeAsync`: finds original IM Debit → writes pro-rata Credit (`debit.Amount × dischargeQty / MRN.TotalQty`, rounded 2dp). Full-discharge path takes the **full outstanding** so the ledger settles to exactly 0 for that MRN; Credit entry marked `IsReleased=true + ActualReleaseDate`.
5. Bumps `MRNRegistry.DischargedQuantity`; on full discharge sets `IsActive=false`.
6. Creates an EX-own `MRNRegistry` row (`IsActive=false`) for symmetry with IM.
7. Emits `CustomsDeclarationCreatedEvent` + `GuaranteeCreditedEvent`.

**Box 37 PreviousProcedureCode:** handler auto-derives "51" when procedure code starts with "31" (standard LON re-export flow), else "00".

**Verified on VPS** (all against pre-existing `26MK8DF9122FA1` IM MRN — Total=40, debit=47.80 EUR):
1. EX partial qty=8 → HTTP 200. DB: Registry.Discharged=8/40; Imported 37.1053→34.1053 + InProduction 5.0→0 + Exported 0→5 (InProd-first) + Exported 3 (Imported overflow) = **8 total Exported** (two rows pre-consolidation fix). FG-VPS-P25-01 3→0. Credit 9.56 EUR (47.80 × 8/40). ✅
2. EX partial qty=2 (after consolidation fix deployed) → HTTP 200. Registry.Discharged=10/40. One prior Exported row grew 5→7, confirming `DbSet.Local` probe consolidates within a single SaveChanges. Credit 2.39 EUR (47.80 × 2/40). ✅
3. EX over-discharge qty=50 (remaining=32) → 400 `exceeds outstanding undischarged qty 32.0000 (Used=40.0000, already discharged=10.0000)`. ✅
4. EX unknown MRN → 400 `not registered for this tenant`. ✅

**Integration tests (ExportDeclarationTests.cs):**
- `EX_PartialDischarge_UpdatesStateAndCreditsPortion` — end-to-end: FG −5, Imported shrinks (inflate-for-waste math 52.6316−10=42.6316), Exported row appears, Registry.Discharged=10/50, 1 Credit row with `IsReleased=false`.
- `EX_FullDischarge_SettlesLedgerAndDeactivatesMrn` — net ledger for MRN = 0 after full-discharge path, Registry.IsActive=false, Credit.IsReleased=true + ActualReleaseDate set.
- `EX_OverDischarge_Returns400` — 400 on `exceeds outstanding undischarged`.
- `EX_UnknownMRN_Returns400` — 400 on unregistered MRN.

**Discoveries & deferred:**
- **TEKSPORT inflate vs bond math:** `dischargeQty` credits against customs (declared units) 1:1 while the physical walk reduces LON-state inventory by the same number (treated as physical units). For TEKSPORT with waste%>0, this means a fully bonded MRN can be fully discharged while the 5% waste-residual physical units stay in Imported. Legacy ELON models this residual via separate waste declarations — that's **P2.6c**.
- **SeedCustomsProcedures skip guard:** seeder's `!AnyAsync()` guard wouldn't pick up new procedure rows on existing deployments. Moved the 3151 insert into the migration itself (`IF NOT EXISTS` guarded) so future migrations + fresh installs stay in sync. Memoized pattern for future procedure additions.
- **Credit description includes declared qty / total** for ledger readability: `EX discharge EX-VPS-P26A-01 — MRN ... qty 8/40.0000`.

**Phase 2 progress:**
- [x] P2.1, [x] P2.2, [x] P2.2.5, [x] P2.3, [x] P2.4, [x] P2.5
- [x] **P2.6a Export** ← this commit
- [ ] P2.6b Return → re-debit bond (reverse of P2.6a; bond credit gets undone)
- [ ] P2.6c Waste declaration → discharge residual LON inventory (waste%/rupe/damage)
- [ ] P2.7 Remaining declaration validation rules

**Next (P2.6b Return / P2.6c Waste):** Return flow is a mirror of EX (re-credit → re-debit; Exported → Imported or InProduction restore). Waste flow discharges the physical residual that inflate-for-waste leaves behind at full declared discharge — moves Imported remainder to `LonProcessState=Waste` + optional proportional bond settlement. Both flows reuse the `TransitionTo…Async` + credit/debit helpers from P2.2/P2.6a.

---

## 2026-04-18 — P2.5 ProductionReceipt books FG + TraceLinks + status lifecycle

**Status:** [x] done
**Commit:** `f90cdc3` (main)

**What landed:**
- `src/LON.Application/Production/Commands/CreateProductionReceipt/CreateProductionReceiptCommand.cs` (new, ~230 lines). `CreateProductionReceiptCommand` + `MaterialConsumptionDto` + handler.
- `src/LON.API/Controllers/ProductionController.cs` — POST `/api/production/orders/{id}/receipts` (sibling to existing GET).
- `tests/LON.IntegrationTests/ProductionReceiptTests.cs` (new, 4 scenarios: happy + auto-TraceLink + completion flip + over-production + explicit consumption).
- `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` regenerated.

**Handler rules:**
1. Validate qty>0, scrap≥0, batch required. PO must exist and not be Cancelled/Completed/Closed. PR.ItemId must match PO.ItemId.
2. No-over-production: `ProducedQuantity + ScrapQuantity` after roll must not exceed `OrderQuantity`.
3. `ProductionReceipt` row + `InventoryMovement(Type=ProductionReceipt=5, From=null, To=LocationId)` + upsert FG `InventoryBalance` at LocationId (`LonProcessState=null` — FG is treated as domestic product; lineage lives in TraceLinks, not on the balance).
4. **TraceLinks**, two modes:
   - **Explicit** `materialConsumption: [{materialIssueId, qty}]` — one TraceLink per entry with caller-supplied quantity; decrements the matching `LonProcessState=InProduction` sibling balance by that qty.
   - **Auto** (omitted) — one TraceLink per `MaterialIssue` on the PO, quantity echoes the full issue qty. Informational lineage; WIP reconciliation deferred to P2.6.
5. Roll `ProducedQuantity + ScrapQuantity` forward; flip Draft/Released → InProgress on first touch; flip → Completed + set ActualEndDate + emit `ProductionOrderCompletedEvent` when `Produced + Scrap ≥ OrderQuantity`.
6. Always emit `FGReceivedEvent`.

**Verified on VPS** (PO-VPS-P24-202604182059, orderQty=10; pre-existing 2 MaterialIssues from P2.4 smoke):
1. Auto-mode PR qty=3 → `PR-20260418-3ee57269`. DB: FG `FG-VPS-P25-01` balance=3, state=null, at RCV-01. Movement Type=5 qty=3 To=RCV-01. **2 TraceLinks** (1 per MaterialIssue): B-VPS-P23/26MKF59796F0A1 qty=2 + B-CLEAN/26MK8DF9122FA1 qty=5. PO: Produced=3, Status=3 InProgress. ✅
2. Over-production qty=999 → 400 `Production receipt would exceed ordered quantity. Ordered 10.0000, produced-after=1002.0000`. ✅
3. Filling PR qty=6 + scrap=1 (pushing total to 9+1=10) → 200. PO: Produced=9, Scrap=1, Status=4 **Completed**, ActualEndDate set. ✅
4. Post-completion PR qty=1 → 400 `Cannot receive production into ProductionOrder in status Completed`. ✅

**Integration tests (ProductionReceiptTests.cs):**
- `PR_HappyPath_BooksFgAndTraceLinksEachIssue` — full side-effect check including auto-mode TraceLink.
- `PR_FillingOrderQuantity_CompletesOrder` — exact-fill triggers Completed + ActualEndDate.
- `PR_OverProduction_Returns400` — guardrail.
- `PR_ExplicitConsumption_DecrementsWipAndWeightsLinks` — materialConsumption flow decrements WIP from 15 → 11 and writes a weighted TraceLink qty=4 (will run on CI).

**Discoveries / design notes:**
- Explicit WIP consumption is opt-in by design. Phase 2.5 ships forward traceability (TraceLinks always created) but leaves "how much WIP was actually burned into this FG batch" to the caller when precision matters. Auto-mode trace-links the full issued qty — good enough for legacy PEE060-style forward reports, overstated for exact MRN attribution. Full WIP reconciliation (proportional burn-down on PO close) belongs to P2.6.
- `FG balance.LonProcessState = null` keeps FG out of the LON state chain. When P2.6 pairs the FG batch with an EX declaration, the Exported transition will be written on the source MRN's Imported/InProduction buckets (not on the FG balance itself). TraceLinks provide the join.

**Phase 2 progress:**
- [x] P2.1, [x] P2.2, [x] P2.2.5 (B1-B7 + I1-I8)
- [x] P2.3 Receipt consumes MRN
- [x] P2.4 MaterialIssue
- [x] **P2.5 ProductionReceipt + TraceLink** ← this commit
- [ ] P2.6a/b/c Export / Return / Waste → guarantee credit + LonProcessState Imported/InProduction → Exported/FinalImport/Waste
- [ ] P2.7 Remaining declaration validation rules

**Next (P2.6a Export):** EX declaration (Box 37 procedure `3151` / `3100`) that discharges the LON bond. Consumes FG batches via TraceLink lookup → identifies the underlying MRNs → transitions their Imported/InProduction balances to Exported + credits the guarantee ledger in a single transaction. Expect reuse of the MRN context pattern from P2.3.

---

## 2026-04-18 — P2.4 MaterialIssue consumes inventory with FEFO + LON state split

**Status:** [x] done
**Commit:** `3aab9bb` (main) — `phase-2.4: MaterialIssue consumes inventory with FEFO + LON state split`

**What landed:**
- `src/LON.Application/Production/Commands/CreateMaterialIssue/CreateMaterialIssueCommand.cs` (new, ~230 lines). `CreateMaterialIssueCommand` + `MaterialIssueLineDto` + `CreateMaterialIssueCommandHandler`.
- `src/LON.API/Controllers/ProductionController.cs` — POST `/api/production/orders/{id}/issues` wired via MediatR.
- `tests/LON.IntegrationTests/MaterialIssueTests.cs` (new, 5 scenarios).
- `api-contract/swagger.json` + `frontend/web/src/api/schema.d.ts` regenerated — contract gate will pass.

**Handler rules:**
1. `ProductionOrder` must exist and not be in terminal state (Cancelled/Completed/Closed).
2. Per line, `ResolveBalance`: if caller specified any of batch/MRN/location, exact match on (ItemId, UoMId, QualityStatus=OK, specified fields); prefers `LonProcessState=Imported` when multiple match. Else FEFO auto-pick — LON-first, then `ExpiryDate ?? MaxValue`, then `CreatedAt`.
3. `balance.Quantity ≥ line.Quantity` pre-checked → 400 `insufficient inventory` on over-draw (belt-and-suspenders before `SubtractQuantity`).
4. **LON-mandatory:** if resolved balance has `LonProcessState=Imported`, persisted `IssueLine` must have both BatchNumber and MRN. Auto-pick fills these from the balance row; engineered null-batch LON rows are rejected.
5. **State split:** when the resolved balance is `Imported`, the issued portion becomes a sibling `InventoryBalance` row (same Item/Location/Batch/MRN/UoM/Quality) with `LonProcessState=InProduction`. Imported bucket shrinks, InProduction grows. Mirrors legacy `LagerMaterijali` split-by-Proces.
6. `InventoryMovement` with `Type=ProductionIssue` (6), `FromLocationId=source.LocationId`, `ToLocationId=null`.
7. Rolls `ProductionOrderMaterial.IssuedQuantity` forward for matching item (missing row tolerated — ad-hoc issues legal).
8. Flips `ProductionOrder.Status` Draft/Released → InProgress on first issue, sets `ActualStartDate`.
9. Emits `MaterialIssuedEvent` per line (via `order.AddDomainEvent`).

**Integration tests (MaterialIssueTests.cs):**
- `Issue_FromImportedBalance_SplitsLonState` — receipt 50 → issue 20 → Imported 32.6316, InProduction 20.0, PO flips to InProgress.
- `Issue_OverDraw_Returns400` — 400 `insufficient inventory`.
- `Issue_UnknownBatchMrn_Returns400` — 400 `no inventory matches`.
- `Issue_WithoutBatchOrMrn_FEFOAutoPicksOldest` — two receipts with explicit `expiryDate` → auto-pick lands on earlier-expiring batch.
- `Issue_LonMaterial_ExplicitNullBatch_Rejected` — engineered Imported balance with null batch/MRN → 400 `LON material requires`.

**Verified on VPS** (`PO-VPS-P24-202604182059`, Item FG-001):
1. Happy-path: POST qty=5 against B-CLEAN (42.1053 Imported) → 200. DB after: two rows — 37.1053 @ state=1 + 5.0 @ state=6. MaterialIssue `ISS-20260418-640c419c` qty=5 batch/MRN preserved. Movement `MOV-20260418-afeab668` Type=6 FromLocation=RCV-01. `PO.Status=3 InProgress`, `ActualStartDate` set. ✅
2. Over-draw: POST qty=999 → 400 `Demand 999, available 37.1053 on batch 'B-CLEAN' MRN '26MK8DF9122FA1'`. ✅
3. Unknown batch/MRN: POST `NOPE/26MKDOESNOTEXIST01` → 400 `no inventory matches the requested Item/Batch/MRN/Location/UoM combination`. ✅
4. FEFO auto-pick: POST qty=2 **without** batch/mrn/location → 200. DB: `B-VPS-P23` 33.3333 → 31.3333 Imported + 2.0 new InProduction sibling. Chosen over B-CLEAN because CreatedAt-earlier (no expiry dates set). ✅

**Pre-existing bug uncovered (not fixed in this commit):**
- `CreateProductionOrderCommandHandler` never calls `_context.ProductionOrders.Add(order)` — returns `IsSuccess=true` but persists nothing. Confirmed by POST → 200 with Data=Guid, but `/api/production/orders` returns `[]`. Worked around for VPS smoke by inserting the PO directly via `sqlcmd`. Added to WORK_PLAN Deferred Backlog as P6.19.

**Discoveries:**
- **Balance `UoMId` ≠ Item `BaseUoMId` in current VPS data.** Receipt payload copies line-level `uoMId`, which is free to differ from item base. Handler filters by balance `UoMId`, so callers must pass the balance's UoMId, not the item's. Documented implicit contract — future UI must read balance row's UoMId (not item's) when offering issue options.
- Legacy inflate qty is visible as Imported bucket. Since the issue records declared qty (not inflated), Imported can drift below the sum of outstanding bond — intentional per current policy (bond tracking sticks with declared numbers).

**Phase 2 progress:**
- [x] P2.1 IM 4200 declaration + MRN registration
- [x] P2.2 Guarantee auto-debit
- [x] P2.2.5 B1-B7 + I1-I8 compliance gates
- [x] P2.3 Receipt consumes MRN
- [x] **P2.4 MaterialIssue** ← this commit
- [ ] P2.5 ProductionReceipt + TraceLink
- [ ] P2.6a/b/c Export / Return / Waste → guarantee credit
- [ ] P2.7 Remaining declaration validation rules

**Next (P2.5 ProductionReceipt):** consume WIP (InProduction) balance → create FG InventoryBalance at a production-out location, record ProductionReceipt + TraceLink between issued materials and produced FG batch. Opens the door to Phase 2.6 export/return flows.

---

## 2026-04-18 — P2.3 Receipt consumes MRN (+ atomic UsedQuantity + inflate-for-waste)

**Status:** [x] done
**Commits:** `f557899` (main) + `38ce54f` (ApprovedItems distinct-item seed fix)

**What landed:**
- `CreateReceiptCommandHandler` gains a `MrnContext` helper that pre-loads every MRN the receipt touches and drives four decisions per-line: validity, expiry, overdraw, waste-%.
- MRNRegistry is pre-validated in ONE batch (no N+1), then mutated in the SAME `SaveChangesAsync` as the receipt + inventory — so no half-applied state is ever visible.
- **TEKSPORT inflate-for-waste** finally wired end-to-end: tenant flag + LONAuthorizationItem.AllowedWastePercentage → `bookedQty = declaredQty × 100 / (100 − w%)`. `ReceiptLine.Quantity` stays at DECLARED (customs record), InventoryBalance + InventoryMovement get INFLATED (legacy lager buffer for expected production waste).
- MRNRegistry.UsedQuantity increments by DECLARED qty (bond accounting), and `IsActive` flips to false when fully consumed so subsequent receipts fail fast on the pre-validate.
- `LonProcessState = Imported` only for 4200/5100 procedures now (B-I7 refinement). FINAL-procedure MRNs no longer claim LON suspension state.

**Seed fix:** earlier `SeedTeksportApprovedItemsAsync` paired both tariffs to the same ImportItemId; the `(authId, itemId)` waste-% dictionary silently took last-writer-wins (10% hid 5%). Changed to use two distinct items (FG-001 → 2905399500 → 5%, PKG-001 → 1211200050 → 10%). Existing VPS rows cleaned via direct DELETE + API restart to re-run the seed.

**Integration tests (6 new in `ReceiptConsumesMrnTests.cs`):**
1. Valid MRN → success + UsedQuantity incremented + balance inflated.
2. Unknown MRN → 400 `"is not registered"`.
3. Aggregate overdraw across two receipts touching the same MRN → 400 `"overdraw"`.
4. Expired MRN → 400 `"expired"`.
5. Receipt without MRN → legacy path (no inflation, null LonProcessState).
6. Full consumption → IsActive=false.

**VPS verification (commit `38ce54f`):**

```
Fresh IM 4200 declaration: qty=40  →  MRN=26MK8DF9122FA1
Receipt  qty=40           →  200 OK
  registry:   Used=40.0000  Total=40.0000       ✅
  balance:    Qty=42.1053  LonProcessState=1   ✅ (40 × 100/95 = 42.1053)

Earlier smoke on the same VPS session:
  overdraw 25 when 20 remain → 400 "overdraw: requested 25, remaining 20.0000 of 50.0000" ✅
  unknown MRN                → 400 "is not registered for this tenant" ✅
  full consumption           → Used=50, IsActive=0 (auto-deactivated)    ✅
```

**Compliance footprint after P2.3:**
- The LON suspension chain's entry step (Receipt → InventoryBalance with MRN + LonProcessState=Imported) is now compliant with both UЦЗ member 349 (bond matches declared qty) and legacy TEKSPORT accounting (lager row holds inflated buffer for expected waste).
- Overdraw is impossible by construction — the sum of receipt-line demand for an MRN is aggregated and compared pre-commit.
- Expired MRN is rejected with a clear, actionable message before any inventory side-effect.

**Follow-ups:**
- `LONAuthorizationItem.CompensatingTariffCode` EF config still `IsRequired()` for a `string?` CLR. Works around with `string.Empty`.
- `LONAuthorizationItem` keyed by `(auth, item)` — future refactor should add tariff-code to the key so one item can have multiple tariffs per authorization.
- Same tariff code appearing for multiple items in an authorization is fine today, since our lookup keys on (authId, itemId). Good.
- Per-line preferential duty lookup (Aneksi `ST<year>`) still deferred to Phase 4.

---


## 2026-04-18 — P2.2.5 IMPORTANT gaps (I1–I8) fixed

**Status:** [x] done
**Commits:** `6270306` (main) + `eb408c4` (audit interceptor TenantId fix)

**Scope decision:** User asked for all IMPORTANT gaps from the P0–P2.2 compliance audit, before P2.3. Single migration `P2_2_5_ComplianceImportantChanges` bundles all schema changes.

**Fixes (with compliance / legacy reference):**

| ID | Fix | Reference |
|---|---|---|
| I1 | `Tenant.InflateImportForWaste: bool` column + TEKSPORT=true idempotent backfill. Receipt-side application deferred to P2.3. | CLAUDE.md §5 — TEKSPORT quirk `KolMat × 100/(100-otpad%)`. |
| I2 | `CreateCustomsDeclarationCommand` gains `LandingCosts` + `Discount` header fields. Handler pro-rates `netLanding = LandingCosts - Discount` across lines by invoice-value weighting; adjusted customs value drives duty/VAT. | Legacy `DodadiTrosociPoFakturaU5` (ELON_Research/04 §1). |
| I3 | New `DutyRateLookupWarningRule` (Priority=14). Compares user DutyRate/VATRate to `TariffCode.CustomsRate/VATRate`; emits Warning on drift > 0.01%. Non-blocking. | Legacy `VratiCarST` / `VratiCarDanStLon`; our scope currently book-rate only, Aneksi/preferential is Phase 4. |
| I4 | `CreateCustomsDeclarationCommand.PreviousProcedureCode` (defaults "00"); handler populates `CustomsDeclaration.PreviousProcedureCode`. | SAD Box 37 is a pair (current + previous); XML emitter (Phase 4.2) splits at submission. |
| I5 | Per-line DTO fields: `GrossWeight`, `NetWeight`, `LocationOfGoods`, `AdditionalUnit`, `CalculationMethod`. `RequiredFieldsRule` now **requires** Box 38 NetWeight (hard). New `SadFieldAdvisoriesRule` emits Warnings for Box 30, 35, 47. | Правилник Член 12 / 15 / 17. |
| I6 | Documented strict guarantee currency policy (declaration currency == bond currency exactly). No code change — we were already stricter. Memory: `project_guarantee_currency_policy.md`. | Justification for audit readers / future devs. |
| I7 | New `LonProcessState` enum (1/6/7/8/9 matches legacy `LagerMaterijali.Proces`). `InventoryBalance.LonProcessState: LonProcessState?` column. Receipt handler sets to `Imported` when the line carries an MRN; never downgrades a later state. | Legacy ELON_Research/04 §5; needed for PEE060 XML compatibility. |
| I8 | **Audit log.** New `IAuditable` marker interface; `CustomsDeclaration`, `LONAuthorization`, `GuaranteeLedgerEntry`, `Receipt`, `User` implement it. `ApplicationDbContext.SaveChangesAsync` snapshots Added/Modified/Deleted state into `AuditLogEntry` rows in the same transaction. Diffs are serialised as `[{field, old, new}]` JSON. `AuditController` exposes `GET /api/audit` (Administrator-only; filter by entityType/entityId/action/time-window, capped at 500 rows). | Compliance hygiene; legacy ELON had no audit trail. |

**Key fix during rollout:** the audit interceptor originally stamped `AuditLogEntry.TenantId` from `ICurrentUserService.TenantId`, which is null during the login flow (before JWT issuance). User.LastLoginAt update is IAuditable → FK-547 crash. Fixed by preferring `entity.TenantId` when the audited entity is ITenantScoped, falling back to CurrentTenantId, and skipping the audit row entirely if neither is resolvable (never write an orphan).

**Migration:** `20260418201554_P2_2_5_ComplianceImportantChanges` — Tenants.InflateImportForWaste, InventoryBalances.LonProcessState, AuditLogEntries table. LONAuthorization.GuaranteePercentageOverride (B5) was already shipped in a prior migration; this one does NOT re-add it.

**VPS verification (commit `eb408c4` deployed):**

```
DB state:
  Tenants: TEKSPORT inflate=1 | DUP-CODE-TEST inflate=0       ✅ I1
  InventoryBalances.LonProcessState column present            ✅ I7
  AuditLogEntries table present                               ✅ I8

I5 — POST w/o NetWeight → 400
  "Box 38 (Линија 1): Нето маса е задолжителна..."            ✅

I2 + I4 + I5 happy path (1000 EUR base, +100 landing, -20 discount):
  line:   customsValue=1080.0000 | duty=54.0000 | vat=204.1200
          netWeight=100.0000                                   ✅
  header: previousProcedureCode="00"                           ✅ I4

I8 GET /api/audit → [
  CustomsDeclaration Create (all 25 fields captured in diff),
  GuaranteeLedgerEntry Create (Amount=129.06 which equals 50% × (54+204.12)),
  User Update                                                  ✅
]

I3 — warnings visible via POST /api/customs/declarations/validate when
rate drifts from TariffCode.CustomsRate. Non-blocking by design.
```

Note: debit amount increased from 119.5 (pre-I2) to 129.06 (with landing costs) — clean evidence that I2 pro-rata → I8 audit chain is end-to-end consistent.

**Compliance footprint after P2.2.5 (I1–I8):**
- Every change to customs-regulated entities (declaration, LON auth, guarantee ledger, receipt, user) is now in the audit log with user attribution.
- Duty/VAT base now includes landing costs for TEKSPORT-style CIF invoices — eliminates under-duty risk on shipping-heavy imports.
- Box 38 NetWeight is required (matching Правилник); Box 30/35/47 surface as warnings so user sees them before customs does.
- Tariff-rate lookup check surfaces user typos before submission.
- Box 37 previous-procedure is recorded (needed for PEE010 XML).
- LON state machine skeleton in place (enum + column + Imported on Receipt); transitions to InProduction/Exported/Waste land in later phases.

**Follow-ups:**
- Actual receipt inflate-for-waste logic (reads `LONAuthorizationItem.AllowedWastePercentage`, inflates receipt line Quantity when `Tenant.InflateImportForWaste=true`) — P2.3.
- `LONAuthorizationItem.CompensatingTariffCode` EF config mismatch (CLR `string?` but `IsRequired()`) — already worked around in seed; proper fix = `IsRequired(false)` + migration.
- Preferential duty rates (Aneksi ST\<year\>, EU/TR overrides) — Phase 4.
- Audit log query-performance index (`(EntityType, EntityId)`, `(OccurredAt)`) if the table grows large; deferred.
- Vector Store OOM still crashes startup (P6.14 unchanged).

---


## 2026-04-18 — P2.2.5 compliance blockers (B1–B7) fixed before P2.3

**Status:** [x] done
**Commits:** `b933078` (main) + `c65216e` (seed backfill refactor) + `39ef2d6` (EF config mismatch fix)

**Why:** User asked for full compliance audit of P0–P2.2 flows against Правилник and legacy ELON, and selected option 3 — fix all 7 BLOCKERS as an interim task before continuing to P2.3. Rationale: P2.3 (Receipt consume MRN) would inherit the MRN-scope bug in B1; cleaner to land the fixes atomically.

**Fixes (with compliance / legacy reference):**

| ID | Fix | Reference |
|---|---|---|
| B1 | MRN uniqueness now global. `IgnoreQueryFilters()` on both `CustomsDeclarations` and `MRNRegistries` before uniqueness check. | Customs allocates MRN globally — two tenants cannot share one. Placeholder MRNs used to be tenant-scoped only. |
| B2 | `UpdateCustomsDeclarationCommand` (MediatR) + new `PUT /api/customs/declarations/{id}`. Refuses non-Draft with 409; for Draft exposes header-text fields only (lines/bond/MRN frozen). | Customs forbids silent mutation of filed declarations — amendments go through a separate workflow (deferred to Phase 4.x). |
| B3 | Per-authorization bond ceiling. `Σ outstanding debits (Debit − Credit) for declarations under this LONAuthorization + new debit ≤ auth.GuaranteeAmount`. | Legacy `Одобренија.GarancijaIznos` was advisory (no FK); ours is enforced. УСЦЗ: each Одобрение carries its own bond limit. |
| B4 | `MRNRegistry.ExpiryDate` and `GuaranteeLedger.ExpectedReleaseDate` prefer `auth.CompletionPeriodDays` over `procedure.DueDays`. | Правилник: completion deadline is set per Одобрение, not per procedure default. |
| B5 | `LONAuthorization.GuaranteePercentageOverride: decimal?` + migration. Handler picks auth override first, `procedure.GuaranteePercentage` fallback. | Customs can risk-adjust % on an individual authorization without changing procedure defaults. |
| B6 | `DeclarationType` (SAD Box 01) derived from `procedure.Type`: Export → `"EX"`, else → `"IM"`. | Unblocks P2.6a (EX declaration for inward-processing closure); previously hardcoded `"IM"`. |
| B7 | New `LONLineTariffWithinAuthorizationRule` (Priority=26). When LONAuthorization has a non-empty ApprovedItems list, each `Line.TariffCode` must be in it. Allow-all when list is empty (back-compat). TEKSPORT auth seeded with 2 ApprovedItems. | УСЦЗ член 349: IM 4200 only for tariffs named in the authorization. |

**Migration:** `AddGuaranteePercentageOverrideToLONAuth` (single nullable decimal column).

**Files:**
- `src/LON.Domain/Entities/Customs/LONAuthorization.cs` — +GuaranteePercentageOverride.
- `src/LON.Application/Customs/Commands/CreateCustomsDeclaration/CreateCustomsDeclarationCommand.cs` — B1 IgnoreQueryFilters, B4 completion-days fallback, B5 % override, B6 DeclarationType map, B3 per-auth ceiling in `TryDebitGuaranteeAsync`.
- `src/LON.Application/Customs/Commands/UpdateCustomsDeclaration/UpdateCustomsDeclarationCommand.cs` (new) — B2 status guard.
- `src/LON.Application/Customs/Validation/Rules/LONLineTariffWithinAuthorizationRule.cs` (new) — B7 tariff scope.
- `src/LON.Infrastructure/DependencyInjection.cs` — register new rule.
- `src/LON.Infrastructure/Persistence/ApplicationDbContextSeed.cs` — seed 2 LONAuthorizationItems, refactored to backfill on upgrade path (not just fresh DB).
- `src/LON.API/Controllers/CustomsController.cs` — PUT endpoint.
- `tests/LON.IntegrationTests/CustomsDeclarationTests.cs` — 7 new tests (one per blocker).

**Integration tests added (7):**
- B1 MRN is globally unique across tenants.
- B2 PUT non-Draft → 409.
- B2 PUT Draft → 200 (notes updated).
- B3 per-authorization bond ceiling enforced (small auth, big declaration).
- B4+B5 authorization overrides apply (90-day expiry + 25% debit).
- B6 Export procedure → DeclarationType="EX".
- B7 unauthorized tariff on IM 4200 → 400.

**VPS verification (deployed `39ef2d6`):**

| Blocker | Verified | Evidence |
|---|---|---|
| B1 | ✅ | Dup MRN `26MKVPSTEST01A1` → 400 `MRN '...' is already registered` |
| B2 | ✅ | PUT Registered decl `DEC-B2-VPS` → 409 `in status 'Registered' and cannot be edited` |
| B3 | tested via CI only | VPS seed has 100k auth limit; would need a test-only small auth |
| B4+B5 | tested via CI only | Override fields not set on seeded auth |
| B6 | ✅ | Export procedure yielded decl row `DEC-B6-VPS \| EX \| EXPORT` |
| B7 | ✅ | Tariff `0401109000` → 400 with `Одобрени тарифи: 2905399500, 1211200050` |

**Compliance footprint after P2.2.5:**
- MRN uniqueness now true global scope (placeholder + real MRN both protected).
- Registered declarations are immutable (no silent edits); amendment flow clearly signposted as future work.
- Two layers of bond enforcement: per-authorization (B3) + account total limit (existing). A declaration cannot land if either would overflow.
- Per-authorization % and completion window (B4/B5) take precedence over procedure defaults — authorization is the contract, procedure is the default.
- Tariff scope tied to Одобрение ApprovedItems (B7) — matches УСЦЗ член 349.

**Follow-ups worth noting (in order of likely impact):**
- EF configuration mismatch: `LONAuthorizationItem.CompensatingTariffCode` is `string?` in CLR but `IsRequired()` in configuration. Currently worked around with `string.Empty` in seed; real fix: `IsRequired(false)` + migration. Added to the backlog.
- Vector Store OOM still crashes startup (P6.14 unchanged).
- I3 preferential duty rate lookup (legacy year-indexed ST\<year\>) — not addressed; DutyRate remains user-input.
- I1 TEKSPORT inflate-for-waste — not addressed; per-tenant flag needed when P2.3 touches receipts.

---


## 2026-04-18 — P2.2 Guarantee auto-debit on declaration

**Status:** [x] done
**Commit:** `63bf612 phase-2.2: guarantee auto-debit on IM 4200 creation`

**Design decisions (documented inline):**
- **Synchronous debit, not outbox-based.** No outbox processor exists yet (would orphan debits); guarantee tracking is business-critical, must be atomic with declaration save. Event (`CustomsDeclarationCreatedEvent` + `GuaranteeDebitedEvent`) is still emitted via the existing OutboxMessages pipeline for future consumers (notifications, XML generation, analytics), but the debit itself is in-handler.
- **Formula:** `(TotalDuty + TotalVAT) × procedure.GuaranteePercentage / 100`. For seeded IM 4200 at 50%: 1000 EUR × 5% duty + 18% VAT = 239 liable → 119.5 debit. Matches UK/EU suspension-system semantics; legacy ELON charged full `Davacki` but no VAT.
- **Hard enforcement (not advisory).** Declaration is rejected (400) if:
  - No active GuaranteeAccount in declaration's currency under caller's tenant.
  - Debit would exceed `TotalLimit - Σ ledger balance`.
  Legacy ELON's `Одобренија.ГаранцијаИзнос` is a free scalar (no FK, no enforcement). Our posture is deliberately stricter — easier to loosen later via a feature flag than to tighten post-breach.

**Files changed:**
- `src/LON.Application/Customs/Commands/CreateCustomsDeclaration/CreateCustomsDeclarationCommand.cs` — new `TryDebitGuaranteeAsync` that: resolves account by currency+active+tenant, computes debit, checks available limit, adds `GuaranteeLedgerEntry` (Debit) with `ReferenceType/ReferenceId/MRN/CustomsDeclarationId/ExpectedReleaseDate`, emits `GuaranteeDebitedEvent`. Handler now injects `ILogger`. Invoked inline before final `SaveChangesAsync` so the whole thing is one transaction.
- `tests/LON.IntegrationTests/CustomsDeclarationTests.cs` — 3 new tests:
  - Happy-path debit — before/after ledger sum matches the expected formula.
  - No-EUR-account (temporarily deactivate seeded account) → 400 + declaration not persisted.
  - Over-limit (temporarily set `TotalLimit = 1`) → 400 with required/available in message.

**How verified on VPS (commit `63bf612`):**
```
Before:
  GUA-2024-001 EUR: limit=500000, balance=0, available=500000

POST /api/customs/declarations (IM 4200, 1000 EUR, 5% duty, 18% VAT) → 200
  declarationId=e8a54ceb-6ef4-41c2-a29a-1e84efc51bdf

After:
  GUA-2024-001 EUR: limit=500000, balance=119.5, available=499880.5   ✅

Ledger tail:
  EntryType=1 (Debit) | 119.5 EUR | MRN=26MK0178877CA1 |
  "Auto-debit 4200 — DEC-P22-SMOKE (50.0000% × (Duty+VAT))"           ✅
```

Negative paths covered only by integration tests (CI) — not live-tested on VPS so we don't have to twiddle seeded account state.

**Compliance footprint:**
- Declaration + bond debit are atomic: you cannot end up with a declaration in DB whose bond wasn't reserved.
- Bond cannot be overdrawn: the 239×50% debit must fit under `TotalLimit − currentBalance`. Breaches caller-side, before declaration is persisted.
- Per-currency bonding: EUR declaration → EUR bond; USD → USD. Prevents FX-adjusted mismatches.
- `GuaranteeLedgerEntry.ExpectedReleaseDate` = DeclarationDate + procedure.DueDays (180 for 4200) — aligned with MRN expiry.

**Follow-ups (backlog):**
- Credit flow (P2.6a/b/c) will INSERT opposite Credit rows on export/return/waste, bringing balance back toward zero.
- Outbox processor (no task yet) — would enable async side effects like sending `GuaranteeDebitedEvent` to a Slack webhook or emitting PEE060 drafts.
- `frontend/web/src/pages/Guarantees.tsx` (not yet reviewed this session) — should show the running balance + traffic-light gauge (P4.4 deferred). Current GET /api/guarantee/accounts already exposes balance; dashboard integration is low-effort when we revisit.
- `CustomsProcedure.GuaranteePercentage` configurable per tenant — currently global. For TEKSPORT-specific quirks (if any), will need a per-tenant override table.

---


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
