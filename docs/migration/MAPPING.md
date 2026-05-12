# MAPPING — legacy ELON → LON v1

> Authoritative table-by-table column mapping. Use this alongside `BLUEPRINT.md` §9.1 + `TEKSPORT_WIPE_PLAN.md`. PRE.7 happy-path (Z2779) reads from here; Phase 21.1 dry-run scales from here.
>
> *Created 2026-05-12 — Phase 17.PRE.4.*

---

## §0 — Purpose, scope, conventions

**Source.** Local ELON DB (TEKSPORT-only slice, 31 tables on `localhost\\ELON`). Production ELON DB at Teksport site has ~501 tables — additional tables (master data + tariff catalogues) will arrive at Phase 21 cutover via secure transfer.

**Target.** LON v1 schema (`localhost\\LONDB` for dev; VPS `LONDB` for production-test after PRE.5 wipe).

**Happy-path fixture.** `Zaklucok 2779` (1 IM → 13 import lines → 5-line BOM → 1 Izdatnica → fully razdolzeno). Migration code must succeed end-to-end for this Zaklucok before scaling.

**Conventions in this document.**
- **Bold column name** in target = required (NOT NULL).
- *Italic column name* in target = nullable.
- `[FK→Table.Column]` = foreign-key reference.
- `[seq]` = generated via SQL SEQUENCE per BLUEPRINT §6.6.
- `[derived]` = computed during migration, not directly copied.
- `[—]` = legacy column dropped (no LON equivalent).

**Default migration order** (FK-respecting):
1. Reference data (DrzavaKor, EdMerKor, tblArtikli) → CodeListItem + Item.
2. LON authorization + customs procedures (Odobrenija → LONAuthorization).
3. ClientOrder (Zaklucoci → ClientOrder).
4. Customs declarations (FakturiU5Z + FakturiU5 → CustomsDeclaration + Line).
5. Inventory movements (LagerMaterijali → InventoryMovement; balance recomputed).
6. BOMs (GotoviProizvodi + Normativi → ClientOrderFinishedGood + BOM + BOMLine).
7. Issue/Waste docs (Izdatnici → MaterialIssue + Shipment; Ispratnici → WasteDeclaration).
8. NEW v1 entities (tblIzvozniFakturi → CommercialInvoice; Propratnici → DeliveryNote).
9. Guarantee ledger recomputed (from Odobrenije + completed declarations).

---

## §1 — Reference / lookup tables

### 1.1 `DrzavaKor` → `CodeListItem` (category=Country) + `Item.CountryOfOrigin`

| ELON column | Type | → | LON target | Notes |
|---|---|---|---|---|
| `DrzavaS` | nvarchar(2) | → | `CodeListItem.Code` | ISO 2-char (e.g. "AT", "DE", "CN"). |
| `DrzavaN` | nvarchar(64) | → | `CodeListItem.Label` (mk) | Macedonian name. |
| `DrzavaS1`..`DrzavaS6` | nvarchar(50) | [—] | — | Multi-locale aliases unused in v1; skip. |

**Migration:** SELECT DISTINCT `DrzavaS` FROM `DrzavaKor` → INSERT `CodeListItem` with `Category='Country'`. 240 rows expected.

### 1.2 `EdMerKor` → `UoM`

| ELON column | Type | → | LON target | Notes |
|---|---|---|---|---|
| `EdMerCPS` | nvarchar(3) | → | `UoM.Code` | E.g. "PCS", "MTR", "PRS". |
| `EdMerCPN` | nvarchar(64) | → | `UoM.Description` (mk) | Macedonian name. |
| `EdMerS1`..`EdMerS6` | nvarchar(50) | [—] | — | Multi-locale unused. |

**Migration:** 34 rows; only 3 actively used in lines (PCS, MTR, PRS). Seed all 34 to support cross-tenant migration later.

### 1.3 `tblArtikli` → `Item`

| ELON column | Type | → | LON target | Notes |
|---|---|---|---|---|
| `ArtRBr` | int | [—] | — | Legacy surrogate; LON uses Guid. |
| `ArtKatBr` | nvarchar(50) | → | **`Item.Code`** | Catalog code (unique per tenant). 456 rows have "A" suffix variant. |
| `ArtNaziv` | nvarchar(255) | → | **`Item.Name`** | |
| `ArtKatTip` | int (0\|1) | → | **`Item.Type`** | 0→`Finished`; 1→`Material`. 8,960 materials + 2,154 finished. |
| `TarBr` | nvarchar(10) | → | *`Item.DefaultTariffCode`* | FK to TariffCode (resolved post-migration; staging string until KnigaNai imported in Phase 21). |
| `EdMer` | nvarchar(16) | → | *`Item.DefaultUoMId`* | FK to UoM by `EdMerCPS` match. |
| `ArtOtpadProc` | float | → | *`Item.LegacyWastePct`* | Inflate-for-waste data; **only 4 of 8,960 rows non-zero** (max 2%). See BLUEPRINT §5.4. Tenant feature flag default OFF. |
| `Archived` (bit) | bit | → | `Item.IsActive` | Invert: archived=true → IsActive=false. 80% archived in legacy. |

**Edge cases:**
- "A" suffix variants (456 rows): same logical item, different revision. Migrate as separate `Item` rows; cross-link via `Item.LegacyVariantParentCode` (new optional column).
- `ArtOtpadZao`: 0 rows in TEKSPORT slice; skip column.

---

## §2 — Authorization + ClientOrder

### 2.1 `Odobrenija` → `LONAuthorization`

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `OdobrenieRBr` | → | `LONAuthorization.LegacyOdobrenieRBr` | Preserved for traceability. |
| `OdobrenieBroj` (decree number) | → | **`LONAuthorization.AuthorizationNumber`** | E.g. „19MK00900000014B6". |
| `OdobrenieDatum` | → | **`LONAuthorization.IssuedDate`** | |
| `Uvoznik` | → | `Tenant.LegacyUvoznik` | TEKSPORT-only DB → all rows have same Uvoznik. |
| `GarancijaIznos` | → | **`LONAuthorization.GuaranteeAmount`** | TEKSPORT primary: 77,000,000 MKD. |
| `GarancijaBroj` | → | *`LONAuthorization.GuaranteeReference`* | E.g. „19MK00900000014B6". |
| `RokVazenje` | → | *`LONAuthorization.ValidUntil`* | |

**Edge cases:** 4 rows total; only 2 with `GarancijaIznos > 0`. `OdobrenieRBr=1` is the primary (carries 248 of 269 Zaklucoci).

### 2.2 `Zaklucoci` → `ClientOrder`

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `OdobrenieRBr + ZaklucokBroj` | → | `ClientOrder.CustomerOrderReference` | Compose as `O{OdobrenieRBr}-Z{ZaklucokBroj}` for legibility. |
| `ZaklucokBroj` | → | `ClientOrder.LegacyZaklucokBroj` | Preserved separately for traceability. |
| (no direct counterpart) | [seq] | **`ClientOrder.OrderNumber`** | `CO-{year}-{seq:D6}` generated at migration time. |
| `ZaklucokDatum` | → | **`ClientOrder.OrderDate`** | |
| `Uvoznik` | → | (via Tenant) | TEKSPORT-only slice. |
| `Klient` (or implied via FakturiU5Z.Primac) | → | **`ClientOrder.CustomerPartnerId`** | Resolved via Partner catalog (built from FakturiU5Z.Primac numeric FKs in §3.1). |
| (no field) | [derived] | **`ClientOrder.LONAuthorizationId`** | FK from `Odobrenija.OdobrenieRBr` match. |
| (no field) | [derived] | **`ClientOrder.Status`** | `Closed` if `RazdolzenaDaNe=true` on ALL related FakturiU5Z. Else `Active`. |

**Migration:**
- Skip staging rows: `WHERE ZaklucokBroj <> '00000'` (0 in local snapshot; may exist in prod).
- 269 non-staging rows.

**Edge cases:**
- Z2779: canonical happy-path candidate.
- Z2802: stress test (multi-producer; 3 distinct Izdatnici; 3 orphan ExportRows).
- Z2780: smoke test.

---

## §3 — Customs declarations

### 3.1 `FakturiU5Z` → `CustomsDeclaration`

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `OdobrenieRBr + ZaklucokBroj + DokRBr` | → | `CustomsDeclaration.LegacyCompositeKey` | Preserved string. |
| `FakturaU5Broj` | → | **`CustomsDeclaration.DeclarationNumber`** | Often empty; if so, generate via SEQUENCE `IM-{year}-{seq:D6}` or `EX-...`. |
| `FakturaU5Datum` | → | **`CustomsDeclaration.DeclarationDate`** | |
| `VidUIS` (procedure code 4051/1041/6121/4200) | → | **`CustomsDeclaration.ProcedureCode`** | FK resolved to `CustomsProcedure` via Code match. |
| (derived from VidUIS) | [derived] | **`CustomsDeclaration.DeclarationType`** | 4051 → IM; 1041 → IM (re-import); 6121 → EX; 4200 → IM (release for free circulation). |
| `Primac` | → | **`CustomsDeclaration.PartnerId`** | Numeric FK; resolved via Partner catalog. |
| `Spediter`, `Transporter`, `Depozitor`, `Ispracac` | → | *(snapshotted to columns)* | Other partner FKs preserved as `SpeditorPartnerId`, etc. |
| `ZaverkaBroj` (customs MRN) | → | *`CustomsDeclaration.MRN`* | Set if non-empty; else generate placeholder `YYMK{8-hex}A1`. |
| `ZaverkaDatum` | → | *`CustomsDeclaration.ApprovedAt`* | |
| `RazdolzenaDaNe` (bit) | → | *`CustomsDeclaration.IsRazdolzeno`* | Used for ClientOrder.Status derivation. |
| `Trosoci`, `Rabat`, `Kurs`, `Valuta` | → | (snapshotted) | Stored on declaration; per-line valuations recompute. |
| `Domasno` (smallint) | → | (skip) | Internal flag, unused. |
| `Proizvoditel` (int FK) | → | `ClientOrder.ProducerPartnerId` | Lifted up to ClientOrder if all child declarations agree. Else null. |
| `Zabeleska` (255 chars) | → | `CustomsDeclaration.Notes` | |
| `KoletiS`, `KoletiO`, `KoletiBr` | → | `CustomsDeclaration.PackagingDescription` + qty | |
| `TezinaBrutoVk` | → | `CustomsDeclaration.GrossWeight` | |

**Partner catalog build:** scan all `Primac`, `Spediter`, `Transporter`, `Depozitor`, `Ispracac`, `Proizvoditel` numeric FK values across the DB → emit DISTINCT integers → create `Partner` rows with `LegacyPartnerInt=<n>`, `Name=<placeholder>` (real Names imported from `tblFirmi` in Phase 21).

### 3.2 `FakturiU5` → `CustomsDeclarationLine`

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `OdobrenieRBr + ZaklucokBroj + DokRBr + FakturaU5RBr` | → | `CustomsDeclarationLine.LegacyCompositeKey` | |
| `FakturaU5RBr` | → | **`CustomsDeclarationLine.LineNumber`** | |
| `ArtKatBrMat` | → | (FK lookup → Item.Id) | **`CustomsDeclarationLine.ItemId`** via `Item.Code` match. |
| `ArtNazivMat` | → | `CustomsDeclarationLine.Description` | |
| `TarBr` | → | (FK lookup → TariffCode) | **`CustomsDeclarationLine.TariffCodeId`** if KnigaNai imported; else nullable + `TariffCodeString` staging field. |
| `Kol`, `EdMer`, `EdMerCar`, `KoefEDM` | → | `Quantity` / `UoMId` / `CustomsUoM` / `UomConversionFactor` | |
| `Cena`, `Valuta`, `Vrednost` | → | `UnitPrice` / `Currency` / `LineValue` | TEKSPORT: 99.998% EUR. |
| `ZemjaPoteklo` | → | `CountryOfOrigin` | FK to CodeListItem(Country). |
| `Sirina`, `M2`, `Tezina`, `SpecTez`, `VrednostBruto`, `StatVred` | → | (snapshotted columns) | |
| `Davacki`, `DavackiEdinica`, `CarSt`, `Carina`, `Danok` | → | `CustomsDuty` / `DutyPerUnit` / `DutyRate` / `Tax` | |
| `NaimRBr` | [derived] | (recomputed via NaimU5 aggregation) | Not persisted; see §10 R6. |
| `User` (int) | → | `CreatedBy=migrated-elon-bulk` user; preserved as `LegacyUserId` | Real user resolution at Phase 21.1.1 backfill. |

**Edge cases:**
- ZemjaPoteklo: 30 distinct values; AT 14k, DE 7.6k, BG 7.2k, CN 6k, TR 2k.
- TarBr: 147 distinct values in lines (KnigaNai missing); staging until prod-export.

---

## §4 — Inventory movements

### 4.1 `LagerMaterijali` → `InventoryMovement` + recomputed `InventoryBalance`

`LagerMaterijali` is the main movement ledger (760,645 rows). Each row becomes ONE `InventoryMovement`. `InventoryBalance` is recomputed by replaying movements (FIFO/identity match on Item+Location+Batch+MRN+UoM+QualityStatus).

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `LagerRBr` | → | `InventoryMovement.LegacyLagerRBr` | Preserved. |
| `LagerDatum` | → | **`InventoryMovement.OccurredAt`** | NULL in Z2779 → fall back to related FakturiU5Z.FakturaU5Datum. |
| `Proces` (int) | → | **`InventoryMovement.MovementType`** + `LonProcessState` transition | See DocumentSource resolver §11.1 below. |
| `DokRBr` | → | **`InventoryMovement.RelatedDocumentId`** | Resolved per Proces (see §11.1). |
| `OdobrenieRBr + ZaklucokBroj` | → | **`InventoryMovement.ClientOrderId`** | FK resolved via §2.2. |
| `GotovProizvodRBr` | → | *`InventoryMovement.RelatedFinishedGoodId`* | FK to ClientOrderFinishedGood (resolved in §5). |
| `Proizvoditel` (int) | → | *`InventoryMovement.ProducerPartnerId`* | FK to Partner; **primary source for producer attribution** (NOT GotoviProizvodi.Proizvoditeli text). |
| `FakturaU5Broj + FakturaU5RBr` | → | (FK → CustomsDeclarationLine) | **`InventoryMovement.SourceDeclarationLineId`** |
| `ArtKatBrMat` | → | **`InventoryMovement.ItemId`** | FK via Item.Code. |
| `Kol` | → | **`InventoryMovement.Quantity`** | Sign per `PlusMinus`: +1 → positive, -1 → negative. |
| `Normativ` | → | *`InventoryMovement.NormativUsed`* | Inflate-for-waste applied here in legacy (see ArtOtpadProc note in §1.3). |
| `KolOtpad`, `KolOtpad1`, `KolOtpad2`, `KolZaguba` + their `Normativ*` + `ArtKatBrMatOtpad*` | → | (4 slots) | Mapped to `OtpadSlot1..4` on `InventoryMovement` for waste/scrap tracking. |
| `Cena`, `Valuta`, `Vrednost`, `VrednostBruto`, `StatVred`, `Tezina`, `TezinaBruto` | → | (snapshotted) | Per-movement valuation snapshot. |
| `Davacki`, `DavackiEdinica`, `Carina`, `Danok`, `CarST`, `DanST`, `CarOsn`, `DanOsn` | → | (snapshotted) | Duty allocation per movement. |
| `EdMerMat`, `EdMerCar`, `KoefEDM`, `KolCar` | → | (UoM block) | |
| `SkartKol` | → | (creates child `SkartMovement` if `> 0`) | Per BLUEPRINT §6.3. |
| `LagerVoIzvoz` (bit) | → | (flag preserved) | Movement marked as part of EX flow. |
| `User` (int) | → | `CreatedBy=migrated-elon-bulk`; preserved as `LegacyUserId` | |
| `Uvoznik` | → | (via Tenant) | NULL in local DB. |
| `Domasno` (smallint) | → | (skip) | |

**InventoryBalance recomputation:**
After all movements imported, run:
```sql
INSERT INTO InventoryBalances (TenantId, ItemId, LocationId, BatchNumber, MRN, UoMId, QualityStatus, Quantity, ...)
SELECT TenantId, ItemId, LocationId, BatchNumber, MRN, UoMId, QualityStatus,
       SUM(Quantity * CASE WHEN MovementType IN (Receipt, Return) THEN 1 ELSE -1 END) AS Quantity, ...
FROM InventoryMovements
GROUP BY TenantId, ItemId, LocationId, BatchNumber, MRN, UoMId, QualityStatus;
```
Filter out zero balances.

---

## §5 — BOMs & Finished Goods

### 5.1 `GotoviProizvodi` → `ClientOrderFinishedGood`

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `OdobrenieRBr + ZaklucokBroj + GotovProizvodRBr` | → | (composite key) | Resolves to `ClientOrderId` (via §2.2) + GP number. |
| `GotovProizvodRBr` | → | `ClientOrderFinishedGood.LineNumber` | |
| `ArtKatBr` | → | **`ClientOrderFinishedGood.ItemId`** | FK via Item.Code. |
| `ArtNaziv`, `ArtNazivMK` | → | `Description` | MK variant preferred. |
| `TarBr` | → | `TariffCodeRef` | Staging string. |
| `Kol`, `EdMer` | → | **`Quantity`** / `UoMId` | |
| `Cena`, `Cena1..3`, `Vrednost`, `Valuta` | → | `UnitPrice` / `Currency` / `TotalValue` | |
| `ZemjaPoteklo` | → | `CountryOfOrigin` | Usually MK (processed in-MK FG). |
| `ZatvorenNalogDaNe` | → | `IsClosed` | Per-FG status flag. |
| `KolCar`, `EdMerCar`, `KoefEDM` | → | (UoM conversion block) | |
| `~~Proizvoditeli~~` (comma-text) | [—] | (NOT used for migration) | NULL in all Z2779/Z2802/Z2780 candidates. True producer attribution is via `LagerMaterijali.Proizvoditel` aggregation (§4.1). |
| `NalogBroj` | → | `ProductionOrderRef` | Staging until ProductionOrder migrated. |

### 5.2 `Normativi` → `BOM` + `BOMLine`

`Normativi` carries 319,212 rows — one row per BOM-line per FG instance. LON aggregates to:
- One `BOM` per (Item finished + revision) combination.
- N `BOMLine`s per BOM.
- `BOMLineWasteOverrides` (4 slots) per BOMLine if non-default waste.

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `OdobrenieRBr + ZaklucokBroj + GotovProizvodRBr` | → | (locates ClientOrderFinishedGood) | |
| `NormativRBr` | → | `BOMLine.LineNumber` | |
| `ArtKatBrMat` (material) | → | **`BOMLine.MaterialItemId`** | FK to Item. |
| `Normativ` (qty per FG unit) | → | **`BOMLine.Normativ`** | |
| `EdMer` | → | `UoMId` | |
| `NormativOtpad`, `NormativOtpad1`, `NormativOtpad2`, `NormativZaguba` + `ArtKatBrMatOtpad*` | → | `BOMLineWasteOverrides` (4 rows per line) | Per BLUEPRINT §5.4 BOMLineWasteOverrides. |

**Dedupe:** group by `(ItemFinished, ArtKatBrMat, Normativ, EdMer, waste slots)` → first occurrence wins; later identicals collapse. Track LON.Migration log for collapsed rows.

### 5.3 `NormativTemplO/S` → `BOMTemplate` (post-v1; deferred)

`NormativTemplS` (size templates, 20,434 rows) + `NormativTemplO` (operation templates, 522 rows) are template libraries for auto-applying BOM during new Zaklucok creation. Deferred entirely to v1.1 — for v1 we migrate concrete `Normativi` only. Templates can be re-derived from BOM history.

### 5.4 `NormativiVelicini` → `ProductionOrderMaterialSize`

**0 rows in local TEKSPORT slice.** Schema preserved for prod migration; no rows to migrate today.

---

## §6 — Issue, Waste, and Return documents

### 6.1 `Izdatnici` → `MaterialIssue` (when Proizvoditel != NULL & DocumentType=IssueToProducer) OR `Shipment` (Type=ProducerReturn for return-receipts)

`Izdatnici` (1,119 rows) is the EXIT document from HQ to producer for inward-processing material. Proces=7 movements in `LagerMaterijali` reference these via `DokRBr` (99% match rate per §11.1).

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `IzdatnicaRBr` (numeric) | → | `MaterialIssue.LegacyIzdatnicaRBr` | Preserved. |
| `IzdatnicaBroj` | → | **`MaterialIssue.IssueNumber`** | Often empty; generate via SEQUENCE `MI-{year}-{seq:D6}` if so. |
| `IzdatnicaDatum` | → | **`MaterialIssue.IssueDate`** | |
| `OdobrenieRBr + ZaklucokBroj` | → | (resolves to ClientOrder) | |
| `Proizvoditel` (int FK) | → | **`MaterialIssue.ProducerPartnerId`** | If NULL → not a MaterialIssue, treat as misc movement. |
| `Opis`, `Zabeleska` (ntext) | → | `Notes` | |
| `User` (int) | → | `CreatedBy=migrated-elon-bulk`; preserved as `LegacyUserId` | |

**Auto-generated `DeliveryNote`:** for each migrated `MaterialIssue`, create a `DeliveryNote(DocumentType=ProducerDispatch, Status=Confirmed, ConfirmedAt=IzdatnicaDatum, RelatedDocumentId=MaterialIssue.Id)` per BLUEPRINT §3.8 + D5. Lines copied from related `LagerMaterijali` rows (Proces=7 with matching DokRBr).

### 6.2 `Ispratnici` → `WasteDeclaration` (when Proces=9 origin) or `Shipment` (rare other cases)

`Ispratnici` (776 rows) is the destruction certificate for waste. Per §11.1, this is **NOT** a customer-shipment Ispratnica — it's specifically the legal paperwork accompanying material destruction.

| ELON column | → | LON target | Notes |
|---|---|---|---|
| `IspratnicaRBr` (numeric) | → | `WasteDeclaration.LegacyIspratnicaRBr` | Preserved. |
| `IspratnicaBroj` | → | **`WasteDeclaration.Number`** | |
| `IspratnicaDatum` | → | **`WasteDeclaration.DestructionDate`** | |
| `OdobrenieRBr` | → | (resolves to LONAuthorization) | |
| `Opis`, `OpisOtpaden`, `OpisNaStoka` | → | `WasteDescription` | |
| `VidUIS`, `VidRegBr`, `CarOE` | → | (customs codes preserved) | |
| `OtpadDaNe`, `RegistriranOtpadDaNe`, `VrakanjeDaNe` | → | (flags preserved as bool) | |
| `MestoUnistuvanje`, `NacinUnistuvanje`, `DatumUnistuvanje`, `VremeUnistuvanje` | → | `DestructionLocation` / `Method` / `Date` / `Time` | |
| `BrutoTezina`, `NetoTezina`, `BrojKoleti`, `TarifnaOznaka` | → | (snapshotted) | |
| `StatVred`, `VrednostMaterijal` | → | `Value` / `MaterialValue` | |
| `Proizvoditel`, `Proizvoditel1..5` | → | (multi-producer composite waste; preserve all) | |
| `Kurs`, `FakturaRB`, `DatPresm` | → | (snapshotted) | |
| `Proces` (int) | → | (always 9 in legitimate waste; flag otherwise) | |

---

## §7 — NEW v1 entities backed by legacy

### 7.1 `tblIzvozniFakturi` + `tblIzvozniFakturiStavki` → `CommercialInvoice` + `CommercialInvoiceLine` (D4)

3,239 headers + 57,857 lines. Per BLUEPRINT §3.2.1.

> **Note:** Z2779 has no rows in this table. Phase 21 dry-run on broader Zaklucoci is the meaningful test. Detailed column mapping deferred until §E8.5 entity lands (Phase 17.E8.5) — at that point this section will be expanded with column-by-column lookup. Stub for now:

| Layer | Mapping |
|---|---|
| Header `tblIzvozniFakturi` | → `CommercialInvoice` (Number, ConsigneePartnerId via Primac, ConsignorPartnerId, InvoiceDate, Currency, TotalAmount, CountryOfDestination, Incoterms) |
| Lines `tblIzvozniFakturiStavki` | → `CommercialInvoiceLine` (ItemId via Article catalog, Quantity, UoMId, UnitPrice, LineTotal, CountryOfOrigin, TariffCodeId) |
| `tblIzvozniFakturiRep` | (related report metadata; review at §E8.5 implementation time) |

**Status mapping:** all migrated rows default to `Status=Issued` (legacy implies already issued + sent at the point of being a closed Zaklucok).

### 7.2 `Propratnici` + `PropratniciStavki` → `DeliveryNote` + `DeliveryNoteLine` (D5)

1,658 headers + 295,918 lines. Per BLUEPRINT §3.8.

| Layer | Mapping |
|---|---|
| Header `Propratnici` | → `DeliveryNote` (Number, DocumentType per derivation rule below, RelatedDocumentId, DispatchDate, From/To Location/Partner, DriverName, VehicleRegistration, Status=Confirmed) |
| Lines `PropratniciStavki` | → `DeliveryNoteLine` (ItemId, Quantity, UoMId, BatchNumber, Notes) |

**DocumentType derivation rule for legacy migration:**
- If `Propratnici.IzdatnicaRBr != NULL` → DocumentType=`ProducerDispatch`, RelatedDocumentId = MaterialIssue.Id (mapped from §6.1).
- Else if `Propratnici.IspratnicaRBr != NULL` → DocumentType=`ProducerReturn` (rare; review at PRE.7 + E7.6 time).
- Else if `Propratnici.ExportShipmentRef != NULL` → DocumentType=`CustomerShipment`, RelatedDocumentId = Shipment.Id.
- Else: log + leave RelatedDocumentId=NULL with DocumentType=`Other` (review post-migration).

Note: `PropratniciStavki` schema is fetched at §E7.6 implementation time; column mapping deferred to that prompt.

---

## §8 — Out-of-scope / skipped tables

| Table | Rows | Reason for skip |
|---|---|---|
| `Arhiva` | 6 | Snapshot of archived records; not migrated. Original rows already in main tables. |
| `EURO`, `EURS` | small | Currency lookup; superseded by `FxRate` (Phase 17.X1) seeded fresh. |
| `dtproperties` | small | SQL Server Designer metadata; not data. |
| `tblListaStavki` | ? | Likely combo-box options; unused at runtime. |
| `tblTezinaOtpadPoU5` | ? | Niche weight-of-scrap-per-U5 lookup; deferred to Phase 27. |
| `tmpArtikli`, `tmpRazdolzuvanjeZak` | tmp | Temporary working tables; skip. |
| `Fakturi` (non-U5) | ? | Domestic/commercial invoices unrelated to inward-processing; possibly maps to existing `Invoice` (§5.14.2). Review at PRE.7 or defer to Phase 27. |
| `tblIzvozniFakturiRep` | ? | Report metadata for tblIzvozniFakturi; review at §E8.5 impl time. |
| `LagerGotoviProizvodi` | 15,203 | FG-side inventory ledger. May be redundant with InventoryBalance recomputation from LagerMaterijali + GotoviProizvodi; review at PRE.7. Initially: skip and recompute. Compare with R1 reconciliation. |

---

## §9 — Missing tables (Phase 21 prod-export request)

These tables are **absent in the local TEKSPORT slice**. Phase 21 cutover plan must include export request from prod ELON:

| Table | Purpose | Target |
|---|---|---|
| `KnigaNai` | Tariff codes catalogue (~9k rows expected) | `TariffCode` + `TariffCodeRate` |
| `Aneksi` | Tariff annexes (year-rate matrix) | `TariffCodeRate` (one row per code+year) |
| `Preferencijal` | Preferential origin codes | `CodeListItem` (category=PreferentialOrigin) |
| `tblFirmi` | Partner master | `Partner` (resolves legacy int FKs from FakturiU5Z, Izdatnici, Ispratnici, etc.) |
| `tblKorisnik<TenantName>` | Tenant-specific employees + users | `Employee` + `User` (resolves legacy int FKs in `*.User` columns) |
| `FakturiU5Skart` | Skart catalog | `Skart` (entity exists per BLUEPRINT §6.3; populate from prod) |

PRE.7 happy-path uses staging strings + placeholder user `migrated-elon-bulk` instead of these FKs. Phase 21.1.1 sub-task backfills after prod-export arrives.

---

## §10 — Reconciliation queries (R1–R6 per BLUEPRINT §9.1)

All run against LON DB after migration; compare to legacy ELON queries.

### R1 — Inventory by Proces ↔ InventoryBalance + InventoryMovement

```sql
-- Legacy (ELON)
SELECT Proces, COUNT(*) AS RowCount, SUM(Kol) AS TotalQty
FROM LagerMaterijali GROUP BY Proces;

-- LON (computed)
SELECT MovementType, COUNT(*), SUM(Quantity)
FROM InventoryMovements GROUP BY MovementType;
```
**Tolerance:** 0.01% on count + qty (rounding differences acceptable).

### R2 — Guarantee balance per Authorization

```sql
-- Legacy
SELECT OdobrenieRBr, GarancijaIznos FROM Odobrenija WHERE GarancijaIznos > 0;

-- LON
SELECT a.LegacyOdobrenieRBr, ga.CurrentBalance
FROM LONAuthorization a JOIN GuaranteeAccount ga ON ga.LONAuthorizationId = a.Id;
```
**Tolerance:** exact (currency-aware).

### R3 — Declaration totals (spot-check)

Pick 10 random `FakturiU5Z` headers; sum corresponding `FakturiU5.Vrednost`, `FakturiU5.Davacki`, `FakturiU5.Carina`. Compare to migrated `CustomsDeclaration` summed lines. **Tolerance:** EUR ±0.01.

### R4 — ClientOrder count

```sql
-- Legacy
SELECT COUNT(*) FROM Zaklucoci WHERE ZaklucokBroj <> '00000';
-- Expected: 269

-- LON
SELECT COUNT(*) FROM ClientOrders WHERE IsDeleted = 0;
-- Expected: 269
```
**Tolerance:** exact.

### R5 — BOMLine count

```sql
-- Legacy
SELECT COUNT(*) FROM Normativi;
-- Expected: 319,212

-- LON (with dedupe)
SELECT COUNT(*) FROM BOMLines;
-- Expected: ≤ 319,212 (collapsed identicals)
```
**Tolerance:** LON ≤ legacy (collapse acceptable); log collapsed count.

### R6 — NaimU5 aggregate

```sql
-- Legacy
SELECT NaimRBr, SUM(Kol) AS Qty, SUM(Davacki) AS Duty
FROM NaimU5 GROUP BY NaimRBr;

-- LON (re-aggregated from CustomsDeclarationLine)
SELECT TariffCodeId, UoMId, CountryOfOrigin,
       SUM(Quantity), SUM(CustomsDuty)
FROM CustomsDeclarationLines
GROUP BY TariffCodeId, UoMId, CountryOfOrigin;
```
**Tolerance:** EUR ±0.01 per group; row count exact.

---

## §11 — Edge cases & resolvers

### 11.1 DocumentSource resolver (keyed on Proces)

| Proces | LON `InventoryMovement.MovementType` | DokRBr resolves to | Match rate |
|---|---|---|---|
| 1 | `Receipt` | `null` (no exit doc) | 294,288 rows (38.7%) |
| 6 | `Adjustment` (rare) | `null` | 192 rows (0.03%) |
| **7** | `IssueToProducer` | `Izdatnici.IzdatnicaRBr` | 99% (294,332 / 298,056) |
| 8 | `ReturnFromProducer` | `Izdatnici.IzdatnicaRBr` (return voucher) | Partial (265 / 2,071); orphans → quarantine queue |
| **9** | `WasteDestroyed` | `Ispratnici.IspratnicaRBr` | 100% (166,038 / 166,038) |

**Implementation:** `ResolveExitDocument(LagerMaterijaliRow r)` in `LON.Migration` returns `(LON_MovementType, ResolvedDocumentId?)` tuple; orphan rows logged to migration log.

### 11.2 NaimU5 — computed, not migrated

NaimU5 has 10,885 rows in legacy but LON does NOT persist as a table. R6 reconciliation re-aggregates from `CustomsDeclarationLine` at query time. If migration tries to insert NaimU5 rows directly, abort with error.

### 11.3 `ArtOtpadProc` inflate-for-waste

Only 4 of 8,960 articles (max 2%). Legacy applied `Kol * 100 / (100 - ArtOtpadProc)` at receipt time. LON keeps as `Tenant.InflateForWasteEnabled` feature flag (default OFF; TEKSPORT migration sets `true` to preserve audit).

### 11.4 Producer attribution

Use `LagerMaterijali.Proizvoditel` (numeric int FK) — not `GotoviProizvodi.Proizvoditeli` (comma-text, NULL on all Z2779/Z2802/Z2780). Build `Partner` (type=Producer) catalog from distinct movement-row Proizvoditel values, plus Izdatnici.Proizvoditel + Ispratnici.Proizvoditel + Proizvoditel1..5 columns.

### 11.5 Uvoznik column NULL globally

Local DB is TEKSPORT-only — `Uvoznik` column is NULL on every row of `LagerMaterijali`, `FakturiU5Z`, `Izdatnici`, `Ispratnici`. Migration assigns `TenantId=TEKSPORT.Id` constant. Multi-tenant prod ELON requires per-row Uvoznik discrimination.

### 11.6 Currency in TEKSPORT-only slice

99.998% EUR (43,223 of 43,224 `FakturiU5` lines). Migration validates: any non-EUR row → emit warning, do not abort. Multi-currency support is in schema (per BLUEPRINT §5.14.8 FxRate); just rarely exercised by TEKSPORT data.

### 11.7 LON.Migration entry point convention

```
dotnet run --project src/LON.Migration -- \
  --source "Server=localhost;Database=ELON;Trusted_Connection=True;TrustServerCertificate=True;" \
  --target "Server=localhost;Database=LONDB;Trusted_Connection=True;TrustServerCertificate=True;" \
  --tenant TEKSPORT \
  --happy-path Z2779   # PRE.7
  # OR
  --full                # Phase 21.1 dry-run
```

`--happy-path Z2779` filters all queries to `OdobrenieRBr=1 AND ZaklucokBroj='2779'`; useful for fast iteration. `--full` migrates all 269 Zaklucoci.

---

## §12 — Open questions (file follow-ups before Phase 21)

1. **`LagerGotoviProizvodi`** redundancy: confirmed redundant or has unique data? Test in PRE.7.
2. **`Fakturi`** (non-U5): commercial invoices for domestic activity? Map to `Invoice` (§5.14.2) or skip? Decide before Phase 21.1.
3. **`tblIzvozniFakturiRep`** report metadata: rendered docs or just config? Review at §E8.5 impl.
4. **`PropratniciStavki`** full column schema not fetched in PRE.4 — defer to §E7.6 implementation time.
5. **Composite-key uniqueness** in legacy: do multiple rows ever exist for same `(OdobrenieRBr, ZaklucokBroj, DokRBr, FakturaU5RBr)` in `FakturiU5`? Spot-check before PRE.7.

---

*End of MAPPING.md v1 (2026-05-12 — PRE.4).*
