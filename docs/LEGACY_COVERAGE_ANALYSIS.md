# LON ↔ ELON — Пресек на процесно покривање

> **Цел:** пред да се пишува корисничко упатство end-to-end (увоз → производство → извоз → финансии/извештаи), провери дали LON ги покрива сите legacy ELON процеси **логички**, **податочно** и **математички**. Секое несоодветство е означено како `✅ eq` (еквивалентно), `🟡 partial` (делумно), `❌ gap` (недостасува) или `✳️ by-design` (свесно направено поразлично).
>
> **Извори:**
> - Legacy: `../PdfToExcel/ELON_Research/{00..05}_*.md` (5-агент research од 3053 VBA процедури, 501 табели).
> - Сегашна LON: `src/LON.Domain`, `src/LON.Application/**/Commands|Queries`, `src/LON.API/Controllers`, `frontend/web/src/nav/navGroups.ts`, `ELON_Blueprint.md`.

---

## 0. Executive summary

| Домен | Логички покрив | Податочен покрив | Математички покрив | Забелешки |
|---|---|---|---|---|
| Master data (артикли / партнери / UoM / тарифи) | ✅ | 🟡 | 🟡 | Недостасуваат `TariffCodeRate` + year-indexed preferential rates; `ArtKatBrStara` (стар SKU) не е експлицитен field |
| U5 Invoice → Declaration (увоз) | ✳️ | ✳️ | 🟡 | LON користи `CustomsDeclaration` (единствена декларација + линии). `NaimU5` rollup и `Trosoci`/`Rabat` pro-rata не се имплементирани |
| Нормативи / BOM | 🟡 | 🟡 | ❌ | `BOM`+`BOMLine` постои; нема `NormativNalog` vs `Normativ` (planned vs effective), нема 4 waste slots + Zaguba, нема template auto-apply, нема NormativiVelicini (по големини) |
| Магацин + State Machine | ✅ | ✅ | ✅ | `LonProcessState={1,6,7,8,9}` 1:1 со ELON `Proces`; FEFO/FIFO со MRN/batch функционира |
| Podelba кон производители | ❌ | ❌ | ❌ | Legacy `LagerMaterijali.Proizvoditel` нема еквивалент; LON нема split кон повеќе producers од еден IM. **Blocks** multi-producer workflow |
| Производствен налог (PO) + издавање + прием | ✅ | ✅ | 🟡 | Math е присутна (Required×Qty, Issued cumulative); efficiency factor го има (S5 KW12); нема pro-rata duty transfer од материјал → PO |
| Otpad / Skart / Zaguba | ❌ | ❌ | ❌ | Само еден `ScrapQuantity` + `AllowedWastePercentage` од authorization. Нема 4 слота, нема `Skart` (defective-on-intake), нема inflate-for-waste |
| Izvoz (EX) + Ispratnica | ✅ | 🟡 | 🟡 | `CreateExportDeclarationCommand` постои, MRN discharge работи; нема `VidUIS='EXA3'` form export, нема IzvozIzdatnica + IzvozIspratnica pair |
| Vrakanje (return) | ✅ | ✅ | ✅ | `CreateReturnDeclarationCommand` реверзира MRN + Guarantee + Inventory state |
| Zaverka (царинска сертификација) | 🟡 | 🟡 | — | `CustomsDeclaration.ZaverkaNumber/Date/Office` постојат како поле; нема state-machine (Draft→Registered→Certified→Released) или workflow со инспектор |
| Гаранција (Garancija + Razdolzuvanje) | ✅ | ✅ | ✅ | Ledger-based (`GuaranteeLedgerEntry`) е поцврст од ELON free-scalar; `GetCurrentBalance()` и `GetAvailableLimit()` автоматски |
| Традиционални duty calculations | ❌ | ❌ | ❌ | `PresmetajDavackiPoNaim`, `RaspredeliDavackiPoStavki`, `IzednaciDavackaPoLager` — ниту еден не е преведен. Duty rates се ручно внесени на линија, не се query-ни од `KnigaNai` |
| PEE XML царински пораки | 🟡 | 🟡 | — | PEE060 генериран (`GeneratePee060XmlQuery`); PEE010/020/030/040/050 deferred |
| Финансии (ракирувач → фактури) | ✅ | ✅ | ✅ | Нов subsystem — `ClientContract`+`RateCard`+`Invoice` со PO integration. ELON нема еквивалент |
| HR / Машини / OEE | ✅ | ✅ | 🟡 | ELON нема HR module; LON има сопствен. OEE е proxy (math е placeholder) |
| Reporting pack | 🟡 | 🟡 | 🟡 | 60 list страници + reports hub постојат; сепак легаси `rptRazdolzuvanje`, `rptG20-G30Mesecno`, `rptArtikli` не се 1:1 преведени |

**Вкупно:** од ~16 главни процесни области — **10 се ✅/✳️ кумулативно покриени**, **4 се 🟡 partial** (дополнителна работа < 2 недели), **2 се ❌ missing critical** (Podelba + Otpad-slots). Детали подолу.

---

## 1. Master Data

### 1.1 Артикли (ELON `tblArtikli` → LON `Item`)

| Концепт (ELON) | Статус | LON еквивалент | Gap |
|---|---|---|---|
| `ArtKatBr` (SKU) | ✅ | `Item.Code` | — |
| `ArtKatTip` (True=material, False=FG) | ✅ | `Item.Type` (enum `ItemType`: RawMaterial, FinishedProduct, Packaging, SemiFinished) | Побогата типизација во LON |
| `ArtKatBrStara` (partner's SKU) | ❌ | — | **Gap.** Увозот со partner-invoice mapping бара ова; без него `frmVnesNaNoviMat` flow не може |
| `ArtZemja` + `ArtBezPref` (invert) | 🟡 | `Item.CountryOfOrigin`; нема `IsPreferential` на item |  Преференцијата е на линија (`CustomsDeclarationLine.IsPreferentialOrigin`), не на item |
| `ArtKatEDM` / `ArtCarEDM` + `ArtKoefEDM` | ✅ | `UnitOfMeasure` + `ItemUoMConversion` | Поправилен design (M2M) |
| `ArtSpecTez` / `ArtSpecTezBruto` | ✅ | `Item.UnitWeight` / `GrossWeight` | — |
| `ArtTarBr` (4+2+2+2) | ✅ | `Item.HSCode` (10-digit string) | — |
| `ArtFaza` (phase) | ❌ | — | **Gap.** Нема phase-tag на item за организирање rute (cutting → sewing → packaging) |
| `ArtOtpadProc` + 3 by-product slots + Zaguba | ❌ | — | **Gap.** LON има само `BOMLine.ScrapPercentage` — една линија |
| Variants (A suffix, DREKKV lock bypass) | ✅ | `Item.BaseCode` + `ColorCode` + `SizeCode` + `ParentItemId` | Поцврсто ретемелено од suffix pattern |
| Soft delete | ✅ | `IsDeleted` | ELON физички бриша; LON soft |

### 1.2 Партнери / UoM / Тарифи

- `Partner` vs ELON `tblKOMMat`/`Firmi` — ✅ еквивалентно; LON има тип (Supplier/Customer/Carrier/CustomsBroker/Bank), ELON нема типизирање.
- `UnitOfMeasure` vs `EDMERCP` + `CTEDMER` — ✅ еквивалентно.
- `TariffCode` vs `KnigaNai` — 🟡 partial. LON има table за 10-digit codes, **но нема `TariffCodeRate`** (годишни rate snapshots). ELON `ST2026 / KontST2026 / PV2026 / FST2026` columns се core за duty calc. **Action:** треба нова ентитет `TariffCodeRate(TariffCodeId, Year, DutyRate, VatRate, PreferentialEU, PreferentialTR, SpecificDuty)`.

### 1.3 Уорк-центри + машини

- `WorkCenter` + `Machine` vs ELON `Cosort<Uvoznik>` per-client tables — ✅ поцврсто решение.
- `Shift` + `Employee` vs ELON `tblKorisnik<Uvoznik>` — ✅. LON има expliciten HR module; ELON го нема.

---

## 2. Увоз на материјали (IM)

### 2.1 Logical flow

**Legacy ELON (`frmFakturiU5` + `subFakturiU5` + `frmNovTransferFakturaU5`):**
1. Внесе header: `FakturiU5Z` (invoice #, date, currency, kurs, trosoci, rabat).
2. Внесе линии: `FakturiU5` (артикл, qty, price, value, weight, tarifa).
3. Групира по `(TarBr, EdMerCar, ZemjaPoteklo)` → `NaimU5` rollup.
4. `PresmetajDavackiPoNaim`: по `NaimU5` пресметува Carina + Danok:
   ```
   CarOsn = Vrednost × Kurs
   Carina = CarSt × CarOsn / 100
   DanOsn = CarOsn + Carina
   Danok  = DanSt × DanOsn / 100
   Davacki = Carina [+ Danok ако VAT се применува]
   ```
5. `RaspredeliDavackiPoStavki`: pro-rata назад на линии (`Davacki_line = Vrednost_line / Vrednost_naim × Davacki_naim`).
6. `DodadiTrosociPoFakturaU5`: `Trosoci - Rabat` спредна proportionally across `Vrednost_line` → `VrednostBruto` (lock).
7. `IzednaciDavackaPoLager`: пренесе per-unit davacki на `LagerMaterijali` (ownership line).
8. `LagerMaterijali` rows со `Proces=1` се креираат — тоа е increasing guarantee.

**LON (`CreateCustomsDeclarationCommand`):**
1. Креира `CustomsDeclaration` (header + 47 Box полиња).
2. `CustomsDeclarationLine` по линија со: tariff, country, weight, qty, price, **`DutyRate` + `VatRate` + `DutyAmount` + `VatAmount` рачно внесени**.
3. `MRNRegistry` запис за tracking.
4. `DebitGuaranteeCommand` charge на guarantee ledger.
5. Bulk `CreateReceiptCommand` explode declaration → receipt → inventory со `LonProcessState=Imported`.

### 2.2 Gap matrix

| ELON концепт | LON еквивалент | Статус |
|---|---|---|
| `FakturiU5Z` (invoice header) | `CustomsDeclaration` | ✳️ by-design — customs-first, не invoice-first |
| `FakturiU5` (linije со артикл/qty/price) | `CustomsDeclarationLine` | ✳️ by-design |
| `NaimU5` (rollup by TarBr+EdMer+ZemjaPoteklo) | — | ❌ **Gap.** Needed за PEE060 XML (групирани ставки) и за report за царинска статистика |
| `PresmetajDavackiPoNaim` (customs+VAT calc) | — | ❌ **Gap.** Rate се бара од user; ELON lookup-а од `KnigaNai` + `CarTarPovlasteniDDV` |
| `RaspredeliDavackiPoStavki` (pro-rata) | — | ❌ **Gap.** Неможе да се изведе додека нема `NaimU5` |
| `DodadiTrosociPoFakturaU5` (trosoci/rabat spread) | `CustomsDeclaration.OtherCharges` scalar | ❌ **Gap.** Нема spread algorithm; `VrednostBruto` lock не постои |
| `IzednaciDavackaPoLager` (per-unit davacki пренес) | `BulkReceiptFromDeclarationCommand` | 🟡 — пренесува qty но не пренесува давачки по единица во `InventoryBalance` |
| `ArtKatBrStara` mapping при internal import | — | ❌ **Gap.** ImportMappingProfile system partially решава, но не го замени 1:1 |
| Traffic light за garancija (10% threshold) | — | ❌ **Gap.** `GuaranteeAccount.GetAvailableLimit()` постои; UI dashboard/alert нема |
| Skart (defective on intake) | `QualityStatus=Blocked/Quarantine` | 🟡 — QualityStatus го flag-ира но нема дедициран `FakturiU5Skart` entity |

### 2.3 Mathematical check

Отворени математички gaps пред корисник да може да се потпре на LON duty figures:

1. **Нема server-side duty calc:** Корисникот мора да ги внесе `DutyRate` и `VatRate` рачно на секоја линија. `DutyAmount = CustomsValue × DutyRate / 100` се очекува во Command handler но не е видливо.
2. **Нема VAT base нетаж:** `DanOsn = CarOsn + Carina` е основа на VAT — LON не ја пресметува; `VatAmount` е ручно поле.
3. **Нема exchange-rate вчитување:** `CustomsDeclaration.ExchangeRate` + `Currency`; нема cross-check со `tblKorisnikVGSP` equivalent.
4. **Нема Trosoci spread:** `OtherCharges` scalar постои но не се дели pro-rata на линии.

---

## 3. Нормативи / BOM (дистрибуција на гаранцијата на линии)

### 3.1 Structural gap

**Legacy ELON (`frmNormativi` + `subNormativiVred` + `NormativTemplO/S`):**

```
GotoviProizvodi (OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr, ArtKatBr[FG], Kol)
   └── Normativi (NormativRBr)
        ├── ArtKatBrMat (материјал)
        ├── Normativ         (EFFECTIVE — per unit FG, post-execution)
        ├── NormativNalog    (PLANNED — per unit FG, at order creation)
        ├── NormativRaspredeli (DISTRIBUTED — from U5)
        ├── KolMat = GP.Kol × Normativ
        ├── ArtFaza (phase)
        ├── VeliciniDaNe → NormativiVelicini (per-size)
        ├── 4 waste slots: Otpad, Otpad1, Otpad2, Zaguba
        │    each: ArtKatBrMat<i> + ArtOtpadProc<i>
        └── Template auto-apply (LEARGV/DELPHI/GENTHERM)
```

**LON (`BOM` + `BOMLine`):**

```
BOM (ItemId[FG], Version, PartnerId?, ValidFrom, ValidTo)
   └── BOMLine (ItemId[material], Quantity, ScrapPercentage, Position)
```

### 3.2 Gap matrix

| ELON концепт | LON еквивалент | Статус |
|---|---|---|
| `GotoviProizvodi` + `Normativi` split | `BOM` + `BOMLine` | ✳️ by-design — edinstven BOM |
| `PartnerId` scoping (за customer-specific BOM) | ✅ `BOM.PartnerId` | Поправилно реализирано |
| `NormativNalog` (planned) vs `Normativ` (effective) | ❌ | **Gap.** Нема preservation на original planned quantity на `ProductionOrderMaterial` — само `RequiredQuantity` (текуцки) + `IssuedQuantity` |
| 4 waste slots (Otpad/Otpad1/Otpad2) + Zaguba | ❌ | **Gap.** Само `BOMLine.ScrapPercentage` и `ProductionReceipt.ScrapQuantity`. Не може да се моделира "30% scrap → slot 0 (продажно), 5% → slot 1 (различен tariff), 2% → slot 2, 3% Zaguba (мртва загуба)" |
| `inflate-for-waste` (TEKSPORT/DREKKV): `KolMat × 100/(100-ArtOtpadProc)` | 🟡 | `BulkReceiptFromDeclarationCommand` има коментар "inflate-for-waste" но math не е видлив; треба да се провери регрешински тест |
| `NormativiVelicini` (per-size breakdown со weighted-average) | ❌ | **Gap.** ProductionOrderMaterial нема size breakdown |
| `NormativTemplO/S` + auto-apply | ❌ | **Gap.** Нема template-based BOM; секој PO креира материјали рачно или од default BOM. Legacy ова е najголем productivity win за repeat products |
| `NormativRaspredeli` (redistribute from U5) | ❌ | **Gap.** Кога еден customs declaration ќе се сплита на повеќе POs, нема auto-proportional redistribute |
| `FakturiU5Skart` (defective on intake) | 🟡 | `ReceiptLine.QualityStatus=Blocked/Quarantine` + `PickTask` со `Blocked` location; но нема сопствен `Skart` entity или workflow |

### 3.3 Mathematical check

1. **Нема inflate-for-waste documented:** потребен integration test за да докаже `Receipt.Quantity = Declaration.Quantity × 100/(100-wastePct)` за TEKSPORT tenant.
2. **Нема per-size math:** weighted-average back-propagation (`NormativPros = SumOfKolMat / SumOfKol`) не постои.
3. **ScrapPercentage pathway:** `BOMLine.ScrapPercentage` → `ProductionOrderMaterial.RequiredQuantity`? Треба да се провери: дали Required = BOMLine.Quantity × (1 + ScrapPercentage/100) × PO.Quantity или само линеарно.

---

## 4. Podelba (distribution to producers)

**Ова е најголемиот architectural gap помеѓу ELON и LON.**

### 4.1 Legacy flow

`frmPodeliBaranjaBrz` + `frmRaspredeliPoProizvoditeliBrz`:
- Еден `Zaklucok` има N `Proizvoditeli` (comma-joined string во `GotoviProizvodi.Proizvoditeli`!).
- `LagerMaterijali` добива `Proizvoditel`+`ProizvoditelN` колона — секој ред е "кој производител држи колку од овој материјал".
- `cmdKreirajIspratnica` генерира 6 ledger rows по action:
  - Клонира GP со `Proces=6` (produced)
  - Клонира GP со `Proces=7` (exported)
  - INSERT `LagerMaterijali Proces=7` со net квантитети + pro-rata duty
  - `Izdatnica` + `Ispratnica` записи
- `frmSmeniProizvoditel` менува producer retroactivno.

### 4.2 LON status

❌ **Complete gap.** LON нема:
- Producer assignment на inventory / PO.
- Multi-producer split од еден IM declaration.
- Izdatnica + Ispratnica entities.

**Impact:** TEKSPORT tenant работи со 3+ различни производители (sub-contractors). Без multi-producer support, не можат да се моделираат реалните flows каде една пратка материјал ќе се подели на три шивачки фабрики.

**Препорачан approach:**
- Нова `ProducerAssignment` entity (Tenant-scoped, many-to-many помеѓу `ProductionOrder` и `Partner[Producer]`).
- `InventoryBalance.AssignedProducerId` nullable.
- `CreatePodelbaCommand` што атомично сплити еден Receipt → N inventory balances по producer.

---

## 5. Производство (PO lifecycle)

### 5.1 Mapping

| ELON концепт | LON еквивалент | Статус |
|---|---|---|
| `Zaklucok` (closure/batch) | `ProductionOrder` | ✳️ by-design |
| `GotoviProizvodi` (FG header) | `ProductionOrder.Item` + `Quantity` | ✅ |
| `Normativi` (BOM за Zaklucok) | `BOM` + `BOMLine` (shared) | 🟡 — ELON има snapshotting (`Normativi` е копија per Zaklucok); LON share-ир `BOM` мeѓу POs |
| `cmdRaspredeliNormativiOdU5` (auto-distribute from IM) | — | ❌ **Gap.** Не може да се рече "распредели 1000kg од IM#123 на оваа 3 POs" |
| Material issue (Izdatnica = `Proces=6`) | `CreateMaterialIssueCommand` | ✅ |
| FG receipt (`LagerGotoviProizvodi Proces=6`) | `CreateProductionReceiptCommand` | ✅ |
| Пренос на единечна давачка на PO | — | ❌ **Gap.** `DavackiEdinica` од материјал не се пренесува на FG |
| `KW12 EfficiencyFactor` | ✅ `ProductionOrderMaterial.EfficiencyFactor` (S5) | — |
| `PreAssignedMRN/Batch` (override FEFO) | ✅ (G3 fields) | Actually богатија од ELON — ELON FEFO е hard-coded |
| `ProductionOrderOperation.Status` + routing phases | 🟡 | Operations exist но без Cutting/Sewing типизирано tag (P8.6–P8.7 planned) |
| `OperationTimeLog` (piece-level time tracking) | ❌ | Планирано во P8.9 |

### 5.2 Mathematical verification

**Materijal issue flow:**
- LON: `InventoryBalance.Quantity -= issuedQty` + `ProductionOrderMaterial.IssuedQuantity += issuedQty` + `InventoryMovement(Type=ProductionIssue)`. ✅ audit trail OK.
- ELON: Паралелно `LagerMaterijali` row со `Proces=1 → 6` transition + updated `Kol`. 1:1 еквивалент.

**FG receipt flow:**
- LON: `InventoryBalance` new row (FG item, batch stamp, `LonProcessState=InProduction` or `Imported` depending on policy) + `ProductionOrder.ProducedQuantity += receivedQty`.
- ELON: `LagerGotoviProizvodi Proces=6` + linked to materials via foreign keys.
- 🟡 **Gap:** LON batch-to-material lineage (traceability) постои (P5.3.X) но не преку `InventoryBalance.SourceMaterialIds`; треба провера за `Traceability` entity integration.

---

## 6. Извоз (EX)

### 6.1 Legacy flow

`frmGotoviProizvodiIzvoz` → `cmdKreirajIspratnica_Click`:
1. INSERT `Izdatnici` (IzdatnicaRBr, "<n>/<yyyy>").
2. Клонира GP со `Proces=6` (produced).
3. INSERT `Ispratnici` со `VidUIS='EXA3'`, `VidRegBr='R'`, `VrakanjeDaNe=False`.
4. Клонира GP со `Proces=7` (exported).
5. INSERT `LagerMaterijali Proces=7` со:
   - `Kol = LagerMaterijali.Kol - KolOtpad`
   - `Davacki = Davacki - (KolOtpad × DavackiEdinica)`
   - `Vrednost = Vrednost - (KolOtpad × Cena)`
   - `Tezina = Tezina - (KolOtpad/Kol) × Tezina`
6. `LagerRBrGP = <new LagerRBr of GP>` (parent-child pointer)
7. По Zaverka (inspector), `Ispratnici.ZaverkaBroj/Datum/VidUIS/CarOE` се наполнуваат.

### 6.2 LON status

**`CreateExportDeclarationCommand` (P2.6a):**
- ✅ Креира `CustomsDeclaration` со `DeclarationType="EX"`.
- ✅ Декрементира `MRNRegistry.DischargedQuantity`.
- ✅ Транзитира `InventoryBalance.LonProcessState: Imported → Exported`.
- ✅ `CreditGuaranteeCommand` proportional release на bond.

**Gaps:**
- ❌ Нема `Izdatnica` entity / форм — само `Shipment` + `ShipmentLine`.
- ❌ Нема `EXA3` form output (PEE050 XML стандард — deferred).
- ❌ Нема pro-rata на `Otpad` при EX (`Kol = Kol - KolOtpad`) — не е applicable додека нема waste slots.

**Mathematical:**
- LON: `GuaranteeLedgerEntry (Type=Credit, Amount = ProportionalBond)` — `Proportional = (DischargedQty / TotalImportedQty) × OriginalBond`. ✅ OK.
- 🟡 Недостасува `Izrednuvanje` пресметка (`IzramniIzvozOtpad.vba`): кога sum(Proces=7) = Proces=1 (материјалот е целосно потрошен), ELON одзема proportional Proces=9 за да избегне double-count.

---

## 7. Враќање (Return) и Отпад (Waste)

### 7.1 Return (Vrakanje, Proces=8)

**Legacy:** `frmMaterijaliVrakanje` → `cmdPecatiVrakanje` → `LagerMaterijali.Proces=8` + `Ispratnica VidUIS='VS7'` (final domestic import).

**LON:** `CreateReturnDeclarationCommand` ✅:
- Реверзира EX → Imported.
- Un-discharge MRN.
- Re-debit guarantee.

**Status:** ✅ покриено.

### 7.2 Waste (Otpad, Proces=9)

**Legacy:** `frmMaterijaliOtpad` + `frmMaterijaliOtpadZaverka` + `frmZbirnaOtpad` + PEE040 XML.
- 4 waste slots se pull-ани од Normativi по `KolOtpad/1/2 + KolZaguba`:
  ```
  dKolZaVnesOtpadU5   = (dKolZaVnesU5 × dArtOtpadProc)  / 100
  dKolZaVnesOtpad1U5  = (dKolZaVnesU5 × dArtOtpadProc1) / 100
  dKolZaVnesOtpad2U5  = (dKolZaVnesU5 × dArtOtpadProc2) / 100
  dKolZaVnesZagubaU5  = (dKolZaVnesU5 × dArtOtpadZaguba)/ 100
  ```

**LON:** `CreateWasteDeclarationCommand` ✅ (single-slot):
- Консумира waste allowance од `LONAuthorizationItem.AllowedWastePercentage`.
- Транзитира `LonProcessState=Waste`.

**Gaps:**
- ❌ **4 waste slots + Zaguba не постојат.** Еден slot е едноставно недоволно за реален flow: различни waste типови имаат различни tariff codes и различни destinations (продажно vs изгорено vs изнесено).
- ❌ Нема PEE040 XML.
- ❌ Нема `frmMaterijaliOtpadZaverka` workflow — нема separate certification pass за отпад.

---

## 8. Гаранција (Garancija + Razdolzuvanje)

### 8.1 Mapping

| ELON | LON | Статус |
|---|---|---|
| `Odobrenija.GarancijaIznos` (free scalar) | `GuaranteeAccount.TotalLimit` | ✅ — LON е ledger-based (многу поцврсто) |
| `VratiSaldoNaDenDenesenZavereni` (certified) | `GuaranteeAccount.GetCurrentBalance()` | ✅ |
| `VratiSaldoNaDenDenesenSite` (incl. uncertified) | — | 🟡 — LON не разликува certified vs total pending |
| `tblSostojbaNaGarancija` (month-end snapshots) | — | ❌ **Gap.** Потребно за audit reports |
| Traffic light (10% threshold, Red/Yellow/Green) | — | ❌ **Gap.** Дополнување на UI component `GuaranteeTrafficLight.tsx` |
| `RazdolzenaDaNe` (manual ratify checkbox) | `GuaranteeLedgerEntry.IsReleased` | ✅ |
| Koreksija (manual corrections) | — | 🟡 — треба да се додаде `GuaranteeLedgerEntry(Type=Adjustment)` |
| `frmRazdolzuvanjeZak` dashboard | `/customs/traceability` + `/finance/guarantees` | ✅ |

### 8.2 Mathematical check

- ✅ `Balance = Σ Debit - Σ Credit` — еднакво на ELON `Zadolzuvanje - Razdolzuvanje + VratiSaldoKorekcija`.
- ✅ Proportional release: `Credit = (DischargedQty / TotalImportedQty) × OriginalBond` — ekvalentно на ELON `Davacki × (KolIzvoz / KolU5)`.
- 🟡 Нема `Zaverka` filter — LON credit entries се immediate при `CreateExportDeclarationCommand`; ELON чека за `Ispratnici.ZaverkaBroj != null` пред release да стапи на сила. **Препорака:** додади `GuaranteeLedgerEntry.IsActualRelease` или state machine.

---

## 9. Царинска комуникација (PEE XML)

| PEE | Што | ELON | LON | Статус |
|---|---|---|---|---|
| PEE010 | IM submission (декларација) | `cmdXML_PEE010` | CertifyDeclarationCommand ("future") | ❌ **Gap** |
| PEE020 | IM clearance confirmation | Manual import на `ZaverkaBroj` | — | ❌ **Gap** |
| PEE030 | Amendment | — | — | ❌ |
| PEE040 | Waste declaration | `rptOtpad` + XML | — | ❌ **Gap** |
| PEE050 | EX submission | `cmdXML_PEE050` | — | ❌ **Gap** |
| PEE060 | Razdolzuvanje report (mandatory monthly) | `cmdXML_PEE060` | ✅ `GeneratePee060XmlQuery` | ✅ |

---

## 10. MozniMinusi (negative stock reconciliation)

**Legacy:** `cmdMozniMinusi` на `frmIzberiZak` — открива negative stock discrepancies (закажано vs реално).

**LON:** ✅ `MozniMinusiQuery` (P4.3) го врати.

---

## 11. Reporting

| Legacy report | LON | Статус |
|---|---|---|
| `rptRazdolzuvanje` | `/customs/traceability` + PEE060 | 🟡 partial |
| `rptArtikli` | `/master-data/items` + CSV export | ✅ |
| `rptG20-G30Mesecno` | `/finance/reports` hub + monthly-pack | 🟡 — има dashboard но нема 1:1 legacy layout |
| `rptOtpad` | — | ❌ **Gap** (зависен од waste slots) |
| `rptAnalizaZak/GP` (closure analysis) | `/customs/open-items` + `/production/completed` | 🟡 |
| Traffic light dashboard | — | ❌ **Gap** |

---

## 12. Финансии (нов модул)

Финансискиот subsystem во LON е **нова функционалност** — ELON нема модул за:
- Rate cards по customer (PerPiece / PerMinute).
- Automated invoice generation од completed POs.
- Invoice lifecycle (Draft → Issued → Paid).

Ова е додадено од corollary (Phase 14.9) и не конкурира со legacy. Покрив: ✅ логички + податочно + математички.

---

## 13. Заклучок пред упатството

### 13.1 Што е подготвено за end-to-end manual

Ако упатството се пишува за **happy-path** (увоз → производство → извоз) со **TEKSPORT tenant**, тогаш следните чекори **се функционални во LON**:

1. ✅ IM declaration + bulk receipt + guarantee debit (P2.1–P2.7).
2. ✅ Inventory by MRN + batch + location (P5.x).
3. ✅ Production order release + material issue + FG receipt (P6.x).
4. ✅ EX declaration + guarantee credit (P2.6a).
5. ✅ Return declaration (P2.6b).
6. ✅ Single-slot waste declaration (P4.6).
7. ✅ Invoice generation од PO (Phase 14.9).

### 13.2 Што треба да се документира како познат gap

Упатството мора експлицитно да ги нагласи deferred / missing делови:

- ❌ **Podelba (multi-producer distribution)** — TEKSPORT има 3+ subcontractors; manual workaround = multi-PO за исти материјали.
- ❌ **4 waste slots + Zaguba** — само еден scrap %. Non-trivial за фирмите што имаат otpad/otpad1/otpad2/zaguba tariff split.
- ❌ **Duty auto-calculation** — rates се рачно внесени; треба internal training за операторот.
- ❌ **Traffic light на guarantee** — user must check `/finance/guarantees` manually; нема automated alert.
- ❌ **PEE010/020/040/050 XML** — user тековно submit-и преку стариот ELON или рачно преку царинскиот портал.
- ❌ **ArtKatBrStara mapping** — за internal partner SKUs, import mapping profile е workaround.
- 🟡 **NormativTemplO/S auto-apply** — BOM копирање рачно; нема "апликани template за DELPHI/LEARGV/GENTHERM" shortcut.

### 13.3 Препорачани наредни чекори (пред финален go-live)

| Приоритет | Task | Effort |
|---|---|---|
| P0 | `TariffCodeRate` entity + duty auto-calc service | L (3-5 d) |
| P0 | 4-slot waste model + migration | L |
| P1 | `ProducerAssignment` + `CreatePodelbaCommand` | XL (1-2w) |
| P1 | Traffic-light component + guarantee alerts | S |
| P2 | PEE010/PEE050 XML generation | M |
| P2 | NormativTemplate auto-apply | M |
| P3 | PEE040 Waste XML (зависен од 4-slot model) | M |
| P3 | `ArtKatBrStara` expliciten field + partner SKU UI | S |

---

## Анекс A: Мапирање ELON форма → LON страница

| ELON | LON route | Статус |
|---|---|---|
| `frmGlavnoMeni` | `/` (Dashboard) | ✅ |
| `frmIzberiZak` (central hub) | `/management/dashboard` | ✅ |
| `frmAzurZak` | `/production/orders/{id}` | ✅ |
| `frmFakturiU5` + `subFakturiU5` | `/customs/declarations/{id}` | ✅ |
| `frmNovTransferFakturaU5` | `/import/presets/kw12` (P6.34) | ✅ |
| `frmGotoviProizvodi` + `subGotoviProizvodi` | `/production/orders` | ✅ |
| `frmNormativi` + `subNormativiVred` | `/master-data/boms` | 🟡 (no template auto-apply) |
| `frmAzurArtikli` | `/master-data/items` | ✅ |
| `frmNovArtikal` | `/master-data/items/new` | ✅ |
| `frmPomosZaArtikli` (picker) | `ItemsSelect` component | ✅ |
| `frmPodeliBaranjaBrz` | — | ❌ **Gap** |
| `frmRaspredeliPoProizvoditeliBrz` | — | ❌ **Gap** |
| `frmMaterijaliOtpad` | `/customs/declarations/new?type=waste` | 🟡 (single slot) |
| `frmMaterijaliVrakanje` | `/customs/declarations/new?type=return` | ✅ |
| `frmGotoviProizvodiIzvoz` | `/customs/declarations/new?type=export` | ✅ |
| `frmRazdolzuvanjeZak` | `/finance/guarantees` + `/customs/traceability` | ✅ |
| `frmZaveriRazdolzeniU5` | — | ❌ no Zaverka workflow |
| `frmInspektor` | — | ❌ no inspector role + form |
| `frmAzurGarancija` | `/finance/guarantees/{id}` | ✅ |
| `frmAzurSkart` | — | ❌ no Skart entity |
| `frmArtKatBrStara` | — | ❌ no old-SKU disambiguation |
| `frmVnesNaNoviMat` | `/import/sessions` (generic importer) | ✅ (generic) |
| `rptRazdolzuvanje` | PEE060 XML | ✅ |
| `rptG20-G30Mesecno` | `/management/monthly-pack` | 🟡 |

---

*Пресек направен: 2026-04-23 (P15 pre-manual audit).*
