# ELON Articles/Materials Catalog (tblArtikli) — Comprehensive Analysis

## 1. Schema Walkthrough

### Identity / Naming
- **ArtRBr** — surrogate Long, primary key. Allocated via `DMax("ArtRBr", "tblArtikli") + 1` in `frmNovArtikal!cmdNov_Click` (**race condition risk** — no locking).
- **ArtKatS** — integer "status"/series code. Always `0` on insert. Dormant today.
- **ArtKatBr** — business SKU (up to 50 chars). **NOT unique at DB level!** Uniqueness enforced only in UI per `(ArtKatBr, ArtKatTip)` combo. Duplicate query exists: `queries/Find duplicates for tblArtikli.sql`.
- **ArtKatBrStara** — legacy/partner SKU. Crosswalk between partner's code and ELON's.
- **ArtNazivORG / ArtNazivMK** — original name and Macedonian transliteration. Empty strings normalized to `" "` (single space).

### Type / Classification
- **ArtKatTip** (Yes/No):
  - `False / 0` = готов производ (finished product)
  - `True / 1` = материјал (material)
- **ArtKatSurovina** — 0/1 flag, only for materials. Toggles whether waste fields enabled. Driven by `chkSurovina_AfterUpdate`.
- **ArtFaza** — production phase code (Long). Material only.

### Origin / Preference
- **ArtZemja** — 2-char ISO country code (DE, IT, MK). Validated live against `Drzava.DrzavaS`.
- **ArtBezPref** (Yes/No) — **INVERTED semantics!** `True = WITHOUT preference (non-EU)`. `False = EU-preferential origin`. Auto-set via `[chkArtBezPref] = Not VratiEUDaNe([ArtZemja])`.
- At GP (finished-product) level, `ArtBezPref` is recomputed per closure comparing non-EU % against threshold (`ProcNonEU`) in `NapolniArtBezPrefGP`.

### Units of Measure / Weights
- **ArtKatEDM** — operational unit (3 chars, InputMask "`>CCC`" uppercase). Validated against `EDMERCP.EdMerCPS`.
- **ArtCarEDM** — customs unit (often differs from ArtKatEDM). Cross-referenced with `CTEDMER` and `KnigaNai` on `TAROZ3_Exit`.
- **ArtKoefEDM** — **POLYMORPHIC**: conversion coefficient from catalog EDM to customs EDM, AND reused as "Sirina" (width) for textiles. Default 1.
- **ArtSpecTez** — specific gravity (kg per catalog-unit). Default 1. Used for `Tezina = Kol * SpecTez`.
- **ArtSpecTezBruto** — gross specific gravity (separate form `frmVnesNaTezinaBruto`).

### Prices
- **ArtCENA** — unit price. Used mainly for finished products. Sanitized: `If IsNull Or < 0 Then 0`. For materials ignored (per-invoice `Cena` authoritative).

### Tariff
- **ArtTarBr** — Text(10) = `TarBr(4) + TAROZ1(2) + TAROZ2(2) + TAROZ3(2)`. InputMasks "0000" / "00" / "00" / "00".
- Concatenation: `rst("ArtTarBr") = [TarBr] & [TAROZ1] & [TAROZ2] & [TAROZ3]`.
- Reverse split: `Left(ArtTarBr,4)`, `Mid(ArtTarBr,5,2)`, etc.
- **⚠️ TYPO BUG** in `frmPomosZaArtikli/procedures/cmdPotvrdi_Click.vba` lines 142-145 — all three tariff parts assigned to `TAROZ1` (should be TAROZ1/2/3).
- F1 on TarBr opens `frmTarifiranje`.

### Waste-related (Materials only with ArtKatSurovina=1)
- **ArtOtpadProc** — main waste % during processing
- **ArtKatBrMatOtpad / 1 / 2** — pointers to up to 3 by-product article codes
- **ArtOtpadProc1 / 2** — corresponding %s
- **ArtKatBrMatZaguba / ArtOtpadZaguba** — loss material + %
- **ArtOtpadZao** — Yes/No, "waste handled off-book"
- **ArtOtpadTarBr** — tariff of the waste material

### `fraSurovina` 3-value radio group (business mode switch):
- `1` = simple waste-% under own tariff
- `2` = "malo kej" zero-waste (all %s cleared, ArtOtpadZao=True)
- `3` = structured waste (with up to three by-product material pointers)

### Auxiliary / Client-specific
- **ArtGlavenPoluPr** — "main semi-finished product" pointer (Long → another ArtRBr)
- **User** — `lKorisnik` who last wrote the record
- **Arhivirano** (Yes/No) — archive flag. `ArhivaDN = "D"` adds `(АРХИВА)` to captions.
- **GodProizv, KV, CCM, Vrati, Nosivost, Sedista, Boja** — vehicle/capacity metadata for GENTHERM/HAVEP/DELPHI/ARCELORMITTAL Cosort modules.
- **MinKol** — reorder threshold.

### Indexes
Only `ArtRBr` (PK_tblArtikli) is DB-unique. `ArtKatBr` uniqueness UI-only.

---

## 2. Creation Flow (frmNovArtikal)

### Form Characteristics
Popup modal, bound to `tblArtikli`. Opened from other forms via `txtForma` + `txtKontrola` threading.

### Flow
**A. `Form_Open`** defaults fields from module-level persisted vars (`pArtNazivORG`, `pArtZemja`, etc.) — remembers user's last entries.

**B. Caller pre-fill** (e.g. from `frmVnesNaNoviMat.cmdNovMaterijal_Click`):
```vb
Forms!frmNovArtikal!frmMat = True
Forms!frmNovArtikal!ZBOR1 = sArtKatBrMat
Forms!frmNovArtikal!txtArtKatBrStara = sArtKatBrMat
Forms!frmNovArtikal!ArtKatEDM = sEdMer
Forms!frmNovArtikal!TarBr = Left(sTarBr, 4)
Forms!frmNovArtikal!TAROZ1 = Mid(sTarBr, 5, 2)
Forms!frmNovArtikal!TAROZ2 = Mid(sTarBr, 7, 2)
Forms!frmNovArtikal!TAROZ3 = "00"       ' HARDCODED!
Forms!frmNovArtikal!ArtZemja = sZemjaPoteklo
Forms!frmNovArtikal!chkArtBezPref = Not VratiEUDaNe(sZemjaPoteklo)
```

**C. "Нов" button** (`cmdNov_Click`):
```vb
lNovBroj = DMax("ArtRBr", "tblArtikli")
If IsNull(lNovBroj) Then lNovBroj = 1 Else lNovBroj = lNovBroj + 1
rst.AddNew
rst("ArtRBr") = lNovBroj
rst("ArtKatS") = 0
rst("ArtKatBr") = " "        ' placeholder
rst("ArtKatBrStara") = " "
...
rst("ArtKatTip") = [frmMat]
rst.Update
[KORISNIK] = lNovBroj       ' store row id
[ZBOR1].SetFocus
```
Row inserted immediately with placeholders, UI edits it in-place.

**D. Field entry order**: ZBOR1 → ZBOR2 → ZBOR3 → ArtKatEDM → ArtZemja → TarBr → TAROZ1-3 → ArtCarEDM → ArtSpecTez → ArtKoefEDM → txtArtKatBrStara → chkSurovina → ArtOtpadProc

**E. "Запиши" button** (`cmdZapisi_Click`):
1. Duplicate check same `ArtKatBr + ArtKatTip`
2. `rst.Edit`, write every field with `Len=0 → " "` normalization
3. Callback dispatch based on parent form (frmFakturiU5, frmGotoviProizvodi, frmNormativi, etc.)

**F. "Откажи" button**:
```vb
DELETE FROM tblArtikli WHERE ArtRBr = [KORISNIK] AND ArtKatTip = [frmMat]
```
**Physical delete!** Leaves an ArtRBr gap refilled by next DMax+1.

### Validations Summary
| Field | Validation |
|---|---|
| ArtKatBr | Duplicate vs same ArtKatTip |
| ArtKatEDM | Must exist in EDMERCP.EdMerCPS |
| ArtCarEDM | Must exist in EDMERCP; cross-checked vs CTEDMER on tariff exit |
| ArtZemja | Must exist in Drzava.DrzavaS; drives chkArtBezPref |
| TarBr/TAROZ* | Numeric via InputMask |
| ArtSpecTez/ArtKoefEDM | NULL→1 |
| ArtOtpadProc | Only editable if chkSurovina=True; NULL→0 |

---

## 3. ArtKatBr Generation

**No auto-numbering for ArtKatBr.** Manually typed or copied from partner's invoice via pre-fill.

Only `ArtRBr` (surrogate PK) is auto-generated: `DMax + 1`.

Business convention (NOT coded): materials 7+ chars, products 5 chars.

Bulk migration queries preserve `ArtKatBr` verbatim:
- `queries/AppendTblArtikliOdPROIZVODI.sql` — ArtKatTip=False
- `queries/AppendTblArtikliOdREPROMATERIJALI.sql` — ArtKatTip=True

Both initialize `ArtKatBrStara = ArtKatBr`.

---

## 4. ArtKatBrStara — Legacy Crosswalk

### Why exists
Partners (HAVEP, JOHNSON, TEKSPORT, DELPHI...) use their OWN SKUs. ELON has its own internal catalog. `ArtKatBrStara` stores partner's SKU, `ArtKatBr` = ELON's internal.

### Detection
```vb
Public Function ImaNemaVoArtikliStara(sArtKatBrMat As String) As Boolean
Set rst = dbs.OpenRecordset(
    "SELECT * FROM tblArtikli WHERE ArtKatBrStara = '" & sArtKatBrMat & 
    "' AND ArtKatTip = Yes")
ImaNemaVoArtikliStara = (rst.RecordCount > 0)
End Function
```

### Decision tree in `frmVnesNaNoviMat.cmdNovMaterijal_Click`:
```vb
If ImaNemaVoArtikliStara(sArtKatBrMat) = True Then
   DoCmd.OpenForm "frmArtKatBrStara"  ' disambiguate existing matches
Else
   DoCmd.OpenForm "frmNovArtikal"     ' create fresh
End If
```

### `frmArtKatBrStara` options:
- **Нов** — Create new ELON SKU with this old SKU (allows multiple ELON articles sharing same ArtKatBrStara)
- **Ажурирај** — Edit existing
- **Потврди** — Accept existing, update `TransferFakturiU5`
- **Излез** — Cancel

---

## 5. Tariff (ArtTarBr) Structure

### Composition
10-char concatenation of 4 segments:
| Segment | Mask | Digits | Meaning |
|---------|------|--------|---------|
| TarBr | "0000" | 4 | HS-4 heading |
| TAROZ1 | "00" | 2 | HS sub-heading |
| TAROZ2 | "00" | 2 | national sub-code / CN8 |
| TAROZ3 | "00" | 2 | statistical/TARIC extension |

### Validations
- Syntactic: InputMask rejects non-digits
- Semantic: `TAROZ3_Exit` cross-references `CTEDMER`/`KnigaNai`. If match exists, enforces prescribed `ArtCarEDM` with warning "ПРОПИШАНА ЕДИНИЦА МЕРА !!!"
- Tariff not verified to actually exist in KnigaNai
- F1 → frmTarifiranje

### Related
- `ArtOtpadTarBr` — tariff of associated waste (own 10-char string)
- `VratiCTEdMer(ArtTarBr, ArtKatEDM)` — auto-derives ArtCarEDM during migration

---

## 6. Material vs Product Flow Differences

### `[frmMat]` option group (0=product, 1=material) written into `ArtKatTip`.

### Visibility (frmAzurArtikli.Form_Open)
When `frmMat=0` (product mode), these controls are hidden:
- chkSurovina, lblSurovina
- ArtFaza, lblFaza, cboFazi
- lblOtpad, lblProcent
- ArtOtpadProc

### Data-entry Semantics
| Concept | Material | Product |
|---------|----------|---------|
| ArtKatBr | 7+ chars practice | 5 chars practice |
| ArtKatBrStara | Populated (partner SKU) | Typically = ArtKatBr |
| ArtKatSurovina | 0 or 1 | Always 0 (hidden) |
| ArtFaza | Production phase | Always 0 |
| ArtOtpadProc | Waste %s | 0 |
| ArtKatBrMatOtpad | Points to by-products | n/a |
| ArtCENA | Ignored (price on invoice) | Unit sell price |
| Normatives | Referenced FROM normatives | OWNS normatives |

### Type-filtering Helpers
- `ImaNemaVoArtikli(sArtKatBr, sEdMer)` — `ArtKatTip = No` (products only)
- `ImaNemaVoArtikliStara(sArtKatBrMat)` — `ArtKatTip = Yes` (materials only)
- Variants: `ImaNemaVoArtikliMat`, `...Mat4/6/N/S`, `...DveSifri`, `...BezEdMer`

### Query Selectors
- `qryMaterijali` — `HAVING ArtKatTip=True`
- `qryArtGP` — `WHERE ArtKatTip=False`

### Price/Weight Treatment
- **Price**: `ArtCENA` pushed into `frmGotoviProizvodi!Cena`. For materials, invoice is authoritative.
- **Weight**: auto-seed `Tezina = Kol * ArtSpecTez` only for materials.

### Normatives (BOM)
`frmAzurNormativOtpad` drives product→material relationship:
- `Normativ` (qty of material per product)
- `NormativOtpad/1/2` (waste quantities)
- `NormativVkupno = Normativ + sum(NormativOtpad)`
- Proportional derivations of `Kol`, `Tezina`, `Davacki`, `Vrednost`, `StatVred`, `Carina`, `Danok`

---

## 7. Popup Routing Logic

### The Trio Pattern
- Empty field → `frmPomosZaArtikli` (finder)
- Unknown code → `frmNovArtikal` (creator)
- Known code → stay

### frmArtIzmeni.ArtKatBrMatOtpad_Exit example:
```vb
If Len([ArtKatBrMatOtpad]) = 0 Then
   ' Empty → finder:
   DoCmd.OpenForm "frmPomosZaArtikli"
   Forms!frmPomosZaArtikli!frmMat = True
   Forms!frmPomosZaArtikli!txtForma = Me.Name
   Forms!frmPomosZaArtikli!txtKontrola = "ArtKatBrMatOtpad"
Else
   SELECT * FROM tblArtikli WHERE ArtKatBr = '<code>' AND ArtKatTip = True
   If Not Found Then
      ' Unknown → creator:
      DoCmd.OpenForm "frmNovArtikal"
      Forms!frmNovArtikal!txtForma = Me.Name
      Forms!frmNovArtikal!txtKontrola = "ArtKatBrMatOtpad"
      Forms!frmNovArtikal!ZBOR1 = sArtKatBrMat
   End If
End If
```

### Summary Table
| Situation | Popup |
|-----------|-------|
| New invoice material, partner SKU unknown | frmNovArtikal |
| New invoice material, partner SKU matches | frmArtKatBrStara |
| Empty ArtKatBrMatOtpad | frmPomosZaArtikli |
| Unknown ArtKatBrMatOtpad | frmNovArtikal |
| Any form needs article pick | frmPomosZaArtikli |
| All articles listing | frmAzurArtikli |

---

## 8. Sync / Batch Operations

### frmAzurArtikli — per-row CRUD only
Despite "Azur" prefix, NOT a batch tool. Just cmdNov, cmdBrisi, cmdIzmeni, cmdZapisi, cmdLista.

### Critical lock-on-use rule
`cboStavkiPrepis_Click` checks if article has been USED:
```vb
If [frmMat] = 0 Then
   sql = "SELECT DISTINCT ArtKatBr FROM GotoviProizvodi WHERE ArtKatBr = '...'"
Else
   sql = "SELECT DISTINCT ArtKatBrMat FROM FakturiU5 WHERE ArtKatBrMat = '...'"
End If
...
If rst.RecordCount > 0 Then
   If Uvoznik = "DREKKV" Then bEnabled = True Else bEnabled = False
   ' DREKKV CAN edit used articles; others locked!
Else
   bEnabled = True
End If
```
This is why `frmArtKatBrStara.cmdNov` is used to VERSION articles instead of renaming.

### Batch ops live elsewhere
- **Legacy migration**: `AppendTblArtikliOdPROIZVODI.sql`, `AppendTblArtikliOdREPROMATERIJALI.sql`
- **Data quality**: `Find duplicates for tblArtikli.sql`
- **Publish to customs**: `GiNemaVotblArtikliECUS.sql` (with ArtRBr+900000 offset)
- **Preference recompute**: `NapolniArtBezPrefGP(lOdobrenieRBr, lLagerGrupa)` — compares non-EU % vs threshold
- **Material swap**: `frmZameniArtikal.cmdZameni_Click` — bulk-update Normativi rows, conflict-handling
- **Minus cleanup**: `frmArtikliMinusi` — negative balances, pull incoming qty from sister closures
- **Scrap**: `frmAzurSkart.cmdZapisi_Click` → `FakturiU5Skart`

---

## 10 Key Gotchas

1. **ArtRBr auto-allocation race-prone** — DMax+1 no locking
2. **"Empty" = single space** throughout text columns (Len=0 → " ")
3. **ArtKatBr NOT DB-unique**, only UI-enforced per ArtKatTip
4. **ArtBezPref inverted** — True = NO preference (non-EU)
5. **ArtKoefEDM polymorphic** — unit coefficient AND width
6. **TAROZ3 = "00" hardcoded** in auto-prefill
7. **Cancel-new physically deletes row** — no soft-delete
8. **No single "tmpArtikli" staging** — uses TransferFakturiU5/GotoviProizvodi/Normativi + in-place reserved row
9. **Edits locked after first use**; DREKKV bypass
10. **Typo bug** — `frmPomosZaArtikli/procedures/cmdPotvrdi_Click.vba` lines 142-145 all assign tariff to TAROZ1

---

## Key File Paths

Schema: `schema/tables/tblArtikli.md`

Main forms:
- `objects/forms/frmNovArtikal/` — create (Form_Open, cmdNov_Click, cmdZapisi_Click)
- `objects/forms/frmAzurArtikli/` — browse/edit
- `objects/forms/frmArtIzmeni/` — edit with waste structure (fraSurovina_AfterUpdate)
- `objects/forms/frmArtKatBrStara/` — old-SKU disambiguation
- `objects/forms/frmPomosZaArtikli/` and `frmPomosZaArtikliZ/` — pickers
- `objects/forms/frmArtikliMinusi/` — minus-balance fixer
- `objects/forms/frmAzurNormativOtpad/` — normative+waste calculator
- `objects/forms/frmAzurSkart/` — scrap
- `objects/forms/frmVnesNaNoviMat/` — staging-to-catalog bridge
- `objects/forms/frmZameniArtikal/` — material swap

Helper modules (~267 fns):
- `objects/modules/Funkcii/procedures/ImaNema*.vba` (many variants)
- `objects/modules/Funkcii/procedures/VratiArt*.vba` (field lookups)
- `objects/modules/Funkcii/procedures/VratiEUDaNe.vba`
- `objects/modules/ArtBezPref/procedures/NapolniArtBezPrefGP.vba`

Queries:
- `AppendTblArtikliOdPROIZVODI.sql`, `AppendTblArtikliOdREPROMATERIJALI.sql`
- `GiNemaVotblArtikliECUS.sql`
- `qryMaterijali.sql`, `qryArtGP.sql`, `qryArtBezPrefGPMAT.sql`
