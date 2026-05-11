# ELON Normatives (BOM), Waste, Finished Products

## 0. Cast of Tables

### Primary:
- **GotoviProizvodi** (PK: OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr)
- **Normativi** (PK: +NormativRBr) — bill of materials per GP
- **NormativiVelicini** (PK: +VelicinaRBr) — per-size breakdown

### Supporting:
- **NormativTemplO** — reusable template header (keyed on GP ArtKatBr)
- **NormativTemplS** — template lines under NormativTemplORBr
- **tblArtikli** — article master with default waste fields
- **FakturiU5Skart** — scrap against a U5 invoice line
- **TransferGotoviProizvodi**, **TransferNormativi** — staging from external ERPs

## 1. Flow Overview

### Entry points from frmAzurZak:
- `cmdNormativi_Click` → `subNormativOtpad` (lightweight)
- `cmdNormativiVred_Click` → `frmNormativiVred` (rich)
- `cmdDodeliNormativi_Click` → `frmDodeluvanjeNormativi` (distribute template)
- `cmdDodeliNormativiOdU5_Click` → `frmDodeluvanjeNormativiOdU5M` (distribute from U5)

### Path A — Manual Entry
1. `frmGotoviProizvodi` for closure (OdobrenieRBr, ZaklucokBroj in header)
2. `subGotoviProizvodi.Form_BeforeInsert` auto-generates `GotovProizvodRBr = MAX+1`
3. `ArtKatBr_Exit` copies from tblArtikli: NazivORG/MK, TarBr, EdMer, EdMerCar, KoefEDM, ArtCENA (falls to frmPomosZaArtikli or frmNovArtikal if missing)
4. `cmdNormativOsnoven_Click` → `frmNormativiVred` for that GP
5. `subNormativiVred.Form_BeforeInsert` auto-increments `NormativRBr`
6. `ArtKatBrMat_Exit`:
   - Checks `ImaNemaVoNormativi()` — prevents duplicate material+phase under same GP
   - Copies defaults from tblArtikli (SpecTez, ArtFaza, ArtKatBrMatOtpad/1/2, ArtOtpadProc/1/2, ArtOtpadZaguba) — only if row's own values empty
7. `Normativ_Exit`: `KolMat = GP.Kol * Normativ`, `NormativNalog = Normativ`
8. `KolMat_Exit`: inverse computation
9. `Vrednost_Exit`: `Cena = Vrednost / KolMat`
10. `Tezina_Exit`: `SpecTez = Tezina / KolMat`, offers update to tblArtikli

### Path B — Import from external ERP (DELPHI, LEARGV, GENTHERM, DREKKV, TEKSPORT...)
Files staged into `TransferGotoviProizvodi` + `TransferNormativi`.

User opens `frmVnesNaNoviProizvodi` / `frmVnesNaNoviNormativi` / combined `frmVnesNaNoviProizvodiNormativi`.

`frmVnesNaNoviProizvodi.cmdNovMaterijal_Click`:
- First register missing catalog articles (frmNovArtikal pre-filled)
- When `lstMatZaVnes.ListCount = 0`: `INSERT INTO GotoviProizvodi ... SELECT ... FROM TransferGotoviProizvodi`
- For LEARGV/DELPHI/GENTHERM: **auto-applies NormativTemplO template** if exists for the GP ArtKatBr:
  - Loops each transferred GP, if matching template → INSERTs NormativTemplS lines into Normativi
  - `KolMat = Normativ * dKolGP`

`frmVnesNaNoviNormativi.cmdNovMaterijal_Click`:
- INSERT Normativi from TransferNormativi
- **DREKKV and TEKSPORT** have "inflate-for-waste" correction:
```
((TransferNormativi.KolMat * 100) / (100 - qryMaterijali.ArtOtpadProc)) / KolMat * Normativ AS Normativ
((TransferNormativi.KolMat * 100) / (100 - qryMaterijali.ArtOtpadProc)) AS KolMat
```
Planner's clean Normativ is inflated by `100/(100-ArtOtpadProc)` to get gross warehouse draw. Original preserved as NormativNalog.

## 2. Normativi Structure

### Identity
- `OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr` — ties to one finished product
- `NormativRBr` — auto-incremented

### Material reference
- `ArtRBrMat, ArtKatBrMat, ArtNazivMat, ArtNazivMKMat, EdMerMat` — snapshot from tblArtikli
- `TarBr, EdMerCar, KoefEDM, KolCar` — customs + EDM conversion
- `ZemjaPoteklo` — material origin

### Quantities & Value
- **`Normativ`** — EFFECTIVE norm used downstream (material per 1 unit of product). Keeps sync with `KolMat = GP.Kol * Normativ`.
- **`NormativNalog`** — PLANNED norm (literally "norm on the work order"). Seeded = Normativ at creation. After execution Normativ may diverge (real waste added). `cmdVratiPlaniran` restores from NormativNalog:
```sql
UPDATE Normativi SET Normativ=NormativNalog, KolMat=NormativNalog*Kol, Vrednost=(NormativNalog*Kol)*Cena
```
- **`NormativRaspredeli`** — DISTRIBUTED norm, used when `frmDodeluvanjeNormativiOdU5*` redistributes physical U5 qty across GPs. Average across selected: `dNormativProsek = dKolMatVk / dKolVk`
- **`KolMat`** — total material qty (= Kol_GP * Normativ)
- **`Cena, Valuta, Vrednost`** — price/currency/value

### Size flag
- **`VeliciniDaNe`** (Yes/No): When True, creates initial NormativiVelicini row, **locks** Normativ and KolMat on main line, enables `lstVelicini`. Setting False deletes all sizes.

### Phase
- **`ArtFaza`** — integer phase (from tblArtikli.ArtFaza via VratiArtFaza). Phases organize manufacturing order (cutting→sewing→finishing)
- **`ArtFazaIspratnica, ArtFazaIspratnicaDat`** — written when line moved to Ispratnica stage

### Weight
- `Tezina` — net weight
- `SpecTez` — specific weight (per unit). Seeded from tblArtikli.ArtSpecTez or 1. Tezina_Exit offers tblArtikli update.

### Waste slots (4 columns, 4 lots)
See section 4.

### User
- `User` — from global lKorisnik

## 3. Size-specific Normatives (NormativiVelicini)

### Activation
Per-line opt-in via `VeliciniDaNe=True`:
```vba
If [VeliciniDaNe] = True Then
   ' seed NormativiVelicini row VelicinaRBr=1
   [Normativ].Locked = True
   [KolMat].Locked = True
   Forms!frmNormativi!lstVelicini.Enabled = True
Else
   DELETE * FROM NormativiVelicini WHERE this NormativRBr
End If
```

### Population
Subform `subVelicini`:
- `Form_BeforeInsert`: auto-increment VelicinaRBr, pre-fill `Kol = GP.Kol - KolVk` (last size absorbs remainder)
- `Kol_Exit`: guards `KolVk > GP.Kol` → "ПРЕГОЛЕМА КОЛИЧИНА!!"
- `Normativ_Exit`: `KolMat = Kol * Normativ`, back-propagate WEIGHTED AVERAGE onto parent:
```vba
NormativPros = SumOfKolMat / SumOfKol
UPDATE Normativi SET Normativ=NormativPros, KolMat=KolMatVK WHERE ...
```

Fields: VelicinaRBr (1,2,3...), VelicinaNaziv ("S","M","L","40","XXL"), Kol, Normativ, KolMat, User.

### Display
`lstVeliciniVK` — grouped by size, shows count of normative lines per size.

## 4. Waste Types (4 slots + zaguba)

### The 4 slots:
| Slot | Material | % | Meaning |
|------|----------|---|---------|
| 0 | ArtKatBrMatOtpad | ArtOtpadProc | Primary saleable waste (e.g. fabric cuttings as "scrap" customs article) |
| 1 | ArtKatBrMatOtpad1 | ArtOtpadProc1 | Secondary waste class (different tariff/channel) |
| 2 | ArtKatBrMatOtpad2 | ArtOtpadProc2 | Tertiary waste class |
| Zaguba | ArtKatBrMatZaguba | ArtOtpadZaguba | **Non-recoverable loss** (dust, vapor, mass unreturnable) |

### Critical difference:
- **ArtOtpadProc** = % of input material that becomes waste-class-0 (Double)
- **ArtKatBrMatOtpad** = catalog number of the waste material it's booked as (Text)

So each slot says "how much" AND "what it is now".

### subNormativiVred.chkZaguba_AfterUpdate: group toggle
Enabling Zaguba arms all 3 otpad slots too. Otpad alone arms slot 0 only. Slots 1, 2, zaguba are **advanced mode**.

### Seeding from tblArtikli (ArtKatBrMat_Exit, lines 61-95):
```vba
If Not IsNull(rst!ArtOtpadProc) And rst!ArtOtpadProc > 0 Then
   If IsNull([ArtOtpadProc]) Or [ArtOtpadProc] = 0 Then
      [ArtOtpadProc] = rst!ArtOtpadProc
   End If
End If
```
Only seeds empty/zero, preserves user overrides.

### Downstream usage (frmRaspredeliPoProizvoditeliBrz.cmdKreirajIspratnica):
```vba
dKolZaVnesOtpadU5   = (dKolZaVnesU5 * dArtOtpadProc) / 100
dKolZaVnesOtpad1U5  = (dKolZaVnesU5 * dArtOtpadProc1) / 100
dKolZaVnesOtpad2U5  = (dKolZaVnesU5 * dArtOtpadProc2) / 100
dKolZaVnesZagubaU5  = (dKolZaVnesU5 * dArtOtpadZaguba) / 100
```
All 4 land in `LagerMaterijali.KolOtpad/1/2/KolZaguba`. Feeds weekly/monthly waste forms (frmMaterijaliOtpad, frmZbirnaOtpad, rptRazdolzuvanje), XML PEE040.

### frmAzurNormativOtpad — repartition after the fact
Officer can move qty from Normativ → Otpad post-audit. Form_Current recomputes:
```
NormativVkupno = Normativ + sum(NormativOtpad)
KolVkupno = Kol + sum(KolOtpad)
TezinaVkupno = (KolVkupno/Kol) * Tezina
KolOtpadProcent = KolOtpad / KolVkupno * 100
DavackiOtpad = DavackiEdinica * KolOtpad     ' duty of waste share
VrednostOtpad = Cena * KolOtpad
```
`cmdIzednaci_Click` resets all slots to 0 and consolidates back into main Normativ/Kol.

### Independent: tblArtikli.ArtOtpadZao (Yes/No)
Flag on the article itself (is this article a waste-class catalog entry). Distinct from "a product that produces waste".

## 5. Template System (NormativTemplO/S)

### Purpose
Reusable, closure-agnostic BOM tied to finished-product ArtKatBr. When new closure has same article → apply in one click.

### Schema
**NormativTemplO**: `NormativTemplORBr (PK), ArtRBr, ArtKatBr, NormativTemplON (name), User`

Same ArtKatBr can have multiple templates (different revisions), discriminated by NormativTemplORBr.

**NormativTemplS**: `NormativTemplORBr, NormativTemplSRBr (PK), ArtRBrMat, ArtKatBrMat, Normativ, ArtKatBrMatOtpad, ArtOtpadProc, ArtKatBrMatOtpad1, ArtOtpadProc1, ArtKatBrMatOtpad2, ArtOtpadProc2, ArtKatBrMatZaguba, ArtOtpadZaguba`

### Save current GP BOM as template
`frmNormativi.cmdZapisiNormativTempl_Click`:
```vba
rst.AddNew
rst!ArtRBr = ... : rst!ArtKatBr = ... : rst!NormativTemplON = ArtNazivMK
rst.Update
' Then INSERT SELECT from Normativi (current GP) into NormativTemplS
```

### Apply template to new GP
`frmNormativiTempl.cmdProdolzi_Click`:
1. Check existing Normativi → prompt overwrite/append/cancel
2. If overwrite: `DELETE * FROM Normativi WHERE (O,Z,GP)`
3. Huge INSERT:
```sql
INSERT INTO Normativi ... SELECT ... FROM NormativTemplS 
WHERE NormativTemplORBr=<chosen>
-- with:
-- Normativ from template
-- KolMat = Kol_GP * template.Normativ
-- EDM = VratiArtEDM([ArtKatBrMat])
-- SpecTez = VratiArtSpecTez([ArtKatBrMat])
-- ArtFaza = VratiArtFaza([ArtKatBrMat])
-- All 4 waste slots from template
```

### Administration
- `cmdBrisiTemplO_Click` — delete template + lines
- `cmdAzurTemplS_Click` — open `frmNormativitemplS` for per-line curation
- `cmdImportNormativi_Click` — DELPHI/LEARGV: pull from upstream ERP
- `cmdExcel_Click`, `cmdSmeniNaziv_Click` — Excel export and rename

### AUTO-APPLICATION at transfer!
`frmVnesNaNoviProizvodi.cmdNovMaterijal_Click` (lines 67-97) for LEARGV/DELPHI/GENTHERM:
- For each TransferGotoviProizvodi row, auto-locate MOST RECENT template for ArtKatBr
- `ORDER BY NormativTemplORBr DESC → MoveFirst`
- If found → bulk-insert all template lines into Normativi
- **Zero user keystrokes needed** for repeat products. Biggest productivity win in the whole app.

## 6. Suggest Normatives from U5 (frmDodeluvanjeNormativiOdU5)

### frmDodeluvanjeNormativiOdU5 — single-material view
Lists (3 panels):
- `lstMaterijali` — materials from FakturiU5: KolMatU imported, KolMatN allocated, KolMatR remainder
- `lstGotoviProizvodi` — closure GPs with remaining demand
- `lstNormativi` — existing Normativi (material, GP) pair

`Normativ_Exit` computes `KolMat = Kol * Normativ` + over-allocation guard:
```vba
If [KolMatU] - (([KolMatN] - dKolMatKontrola) + dKolMat) < 0 Then
   MsgBox "ПРЕГОЛЕМА КОЛИЧИНА! ПРОДОЛЖУВАТЕ?"
   If No Then [Normativ] = [NormativKontrola]
```

`cmdProdolzi_Click`:
- Existing → `UPDATE Normativi SET Normativ, KolMat`
- New & both > 0 → `INSERT INTO Normativi` using material's metadata via inline `VratiArtNazivORG/MK/EDM/SpecTez/ArtFaza` (single RunSQL)
- New NormativRBr = `DMax([NormativRBr])+1`

### frmDodeluvanjeNormativiOdU5M — multi-product distribution
User picks ONE material, MANY GPs, one of 3 modes (`fraIzberi`):
1. **lIzberi=1 "Nova raspredelba"** — delete unselected GPs' normatives, redistribute FULL KolMatU by GP.Kol:
   `dNormativProsek = dKolMatVk / dKolVk`
2. **lIzberi=2 "Samo na selektirani bez normativ"** — fill gaps on selected only. Uses KolMatR.
3. **lIzberi=3 "Raspredeluvaj na site"** — add/subtract against existing on all selected.

Second pass: rounds KolMat to 2 decimals, recomputes Normativ=KolMat/Kol, **corrects last row for cumulative drift**:
```vba
If dKolMatVkKoregirana <> Round(dKolMatU, 2) Then
   dRazlikaKolMat = dKolMatVkKoregirana - Round(dKolMatU, 2)
   rst.MoveLast
   dKolMat = Round(rst!KolMat, 2) - dRazlikaKolMat
```

### frmDodeluvanjeNormativMaterijal — lighter
Takes one template material's (NormativSel, KolMat), applies to every selected GP row in GotoviProizvodiDodeliTmp, deletes temp rows.

## 7. Producer Assignment

### Producers = firms from tblKOMMat
`VratisNaziv(43, RBrFirma)` → SNAZIV. No dedicated Proizvoditeli table.

### Where Proizvoditel lives:
- **GotoviProizvodi.Proizvoditeli** (Text 255) — FREE-TEXT LIST of allowed producers, comma-joined! Built by `lstFirmi_DblClick`:
  ```
  sProizvoditeli = [Proizvoditeli] & ", " & [lstfirmi].Column(1)
  ```
- **LagerGotoviProizvodi.Proizvoditel (Long) / ProizvoditelN (Text)** — ACTUAL producer per warehouse batch
- **Propratnici.Proizvoditel** — producer on transport doc

### frmMeniProizvoditel — lean picker (not editor!)
Just Proizvoditel text + cboProizvoditel combo that sync each other. Three action buttons carry producer+closure filter:
- `cmdBaranja_Click` → frmFirmiBaranja
- `cmdIspratnici_Click` → frmIspratnici
- `cmdIzdatnici_Click` → frmIzdatnici

### frmSmeniProizvoditel + subSmeniProizvoditel — ACTUALLY change producer
`cmdOdberiFirma` → firm picker (frmPomosZaFirmi).

Proizvoditel_Exit validates via `VratisNaziv(43, ...)`:
```vba
sProizvoditelN = VratisNaziv(43, [Proizvoditel])
If sProizvoditelN = " " Then
   MsgBox "НЕМА ТАКВА ФИРМА!!!"
```

`cmdKreirajIspratnica_Click`:
- If Kol<=0 or no producer: offer to DELETE the orphan Lager row
- Else close and refresh parent list

### Downstream effects of producer change
Next redistribution run filters by new Proizvoditel:
```sql
INSERT INTO LagerGotoviProizvodiZaPodelba ...
FROM Normativi RIGHT JOIN LagerGotoviProizvodi
WHERE ... AND LagerGotoviProizvodi.Proizvoditel = <new>
```
And pre-tags LagerGP rows:
```sql
UPDATE LagerGotoviProizvodi SET Proizvoditel=<new>, ProizvoditelN=<name>, User=<me>
WHERE ... AND LagerGrupa=<old> AND Proces=1
```

**Normativi unchanged** — BOM is at GP level, not producer level. Only the consuming factory name changes.

## 8. Skart (Scrap/Reject) vs Otpad (Waste)

### Skart = rejected INCOMING U5 material
Same fields as FakturiU5 but semantic: "portion of this import line found defective on intake, cannot enter production at all."

### Form: frmAzurSkart
`Form_Open` from existing FakturiU5Skart or defaults NetoKol=Kol, SkartKol=0.

`Netokol_Exit`:
```vba
If [NetoKol] > [Kol] Then Cancel = True
ElseIf [NetoKol] = [Kol] Then ...  ' nothing rejected
Else
   [SkartKol] = [Kol] - [NetoKol]
   [ProcKol] = [SkartKol] / [Kol] * 100
```

`cmdZapisi_Click`:
```sql
INSERT INTO FakturiU5Skart ... 
SELECT ..., [SkartKol] * FakturiU5.Cena AS Vrednost,
       [SkartKol] * FakturiU5.Sirina AS M2,
       [SkartKol] * FakturiU5.DavackiEdinica AS Davacki,
       [SkartKol] * FakturiU5.SpecTez AS Tezina,
       [ProcKol] AS Prockol
FROM FakturiU5 WHERE (5-col match)
```

`cmdIzmeni_Click` allows amending: delete current + return to empty state.

### Integration with consumption
`frmRaspredeliPoProizvoditeliBrz.cmdKreirajIspratnica` reduces available U5 by Skart BEFORE allocation:
```vba
dKolPoU50 = rstU5!SumOfKol
dSkartKolZaVnes = VratiSkartKol(lOdobrenieRBr, sFakturaU5Broj, ...)
dKolPoU50Mat = dKolPoU50 - dSkartKolZaVnes
```

Skart is removed from customs pool BEFORE touching Normativi. Never appears as waste in production (never entered production).

### Skart vs Otpad — business distinction

| Aspect | Otpad (waste) | Skart (scrap/reject) |
|--------|---------------|----------------------|
| Table | Normativi waste cols, LagerMaterijali.KolOtpad*, MagacinOtpad | FakturiU5Skart |
| When | During production | At U5 receiving |
| Qty basis | % of consumed material per unit GP | Absolute qty off one U5 line |
| Material identity | Becomes NEW catalog item (fabric → fabric-scrap) | Stays same, non-net status |
| Customs | Redeclared → rptRazdolzuvanje, frmMaterijaliOtpad, PEE040 XML | Removed from balance, processed separately |
| Affects production | Yes — reduces effective NormativNalog | No — reduces available KolPoU50Mat before norms |

**Simple**: Otpad = legitimate manufacturing by-product. Skart = defective incoming.
