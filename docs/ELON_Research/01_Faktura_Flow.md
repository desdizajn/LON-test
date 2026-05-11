# ELON Invoice Entry Flow (Fakturi U5) — Research Report

## 1. Overall Flow — How an Invoice Gets Entered

Two distinct entry paths exist, both ending up writing to `FakturiU5` (line items) and `FakturiU5Z` (header).

### Path A — Manual Entry (one-by-one keystrokes)

1. User opens **`frmIzberiZak`** (closure/Zaklucok selector, the main "navigator" form). They pick an Odobrenie (approval) and a Zaklucok (closure).
2. From there they open **`frmAzurZak`** (closure manager).
3. Click **"Nova FakturaU5"** (`cmdNovaFakturaU5_Click` at `objects/forms/frmAzurZak/procedures/cmdNovaFakturaU5_Click.vba`) — this opens **`frmNovaFakturaU5`**, a tiny "new invoice" dialog where the user types `FakturaU5Broj` (invoice number) and `FakturaU5Datum` (invoice date). The user can also pick an existing IMA5 record from `lstFakturiU5Z` (`fraNovaU5_AfterUpdate`).
4. **`frmNovaFakturaU5.cmdPotvrdi_Click`** validates that header isn't already certified (`ZaverkaBroj`), then `INSERT INTO FakturiU5Z` with `VidUIS = 'IMA5'` (or `'IMA4'` based on `lTipNaProgram`) and `VidRegBrS = 'S'`. It then opens **`frmFakturiU5`** filtered to the new record.
5. **`frmFakturiU5`** is the main edit form — its RecordSource is `FakturiU5Z`, with embedded subforms `subFakturiU5` (line items, RecordSource `FakturiU5`), `subNaimU5`, and `tblListaStavki`. `Form_Open` calculates a "garancija/saldo" traffic light (`Semafor`/`SemaforNZ`) and disables the Print button if guarantee is exhausted.
6. User enters lines in `subFakturiU5` (the datasheet subform). Each new row triggers `Form_BeforeInsert` which auto-numbers `FakturaU5RBr = MAX + 1`, copies last `Valuta` from a module-level `sValuta`, sets `ZaklucokBroj` from parent, sets `User = lKorisnik`.
7. Per-field exits drive material lookup, calculations, popups for missing artikli, and on-the-fly tariff/specific-weight reconciliation.
8. After all lines entered, user runs **`subFakturiU5.cmdVnesiNaim_Click`** to group lines by `(TarBr, EdMerCar, ZemjaPoteklo)` and assign `NaimRBr`. Then **`cmdFormiraj_Click`** rolls them into `NaimU5` (header-of-naimenovanija) and computes customs (`Carina`, `Danok`, `Davacki`).
9. Optionally enter trošoci/rabat (costs/discounts) on the parent and click **`cmdPresmetaj_Click`** — calls module function `DodadiTrosociPoFakturaU5` which spreads the cost proportionally across line `Vrednost` (or into `VrednostBruto` for `Uvoznik = "DREKKV"`).
10. **`cmdIzlez_Click`** closes — if no `FakturiU5` rows remain, deletes the orphan `FakturiU5Z` (only when `ZaklucokBroj <> "00000"`), `NaimU5`, and `tblListaStavki` rows.

### Path B — Bulk Transfer (from importer's electronic invoice)

1. From **`frmAzurZak`**, the user clicks **"Prevzemanje"** (`cmdPrevzemanje_Click`) — opens `frmTransfer<Uvoznik>` matching the global `Uvoznik` constant (e.g., `frmTransferHavep`, `frmTransferGENTHERM`, `frmTransferDomoteks`, `frmTransferZORLU`, `frmTransferCPL`, `frmTransferDREKKV`, `frmTransferTRITEKS`, `frmTransferJONSON`, `frmTransferGLOBAL`, `frmTransferETERNA`, `frmTransferTEKSPORT`, `frmTransferECD`...). For ECD imports there's a dedicated `cmdPrevzemanjeECD_Click` that first calls `NovUpdate`.
2. Each `frmTransfer*` form lets the user pick a row from imported `Invoice<Uvoznik>` table; clicking "Confirm" opens **`frmNovTransferFakturaU5`** with header info pre-populated.
3. **`frmNovTransferFakturaU5.cmdPotvrdi_Click`** is the heavyweight (678 lines): deletes `TransferFakturiU5` staging table, checks for existing `FakturiU5Z` (warns if certified, asks confirmation if not), inserts header, then runs a giant `If/ElseIf` over `Uvoznik` — each branch has a custom `INSERT INTO TransferFakturiU5 SELECT ... FROM Invoice<Uvoznik>` mapping the source schema to ELON's. After insert, iterates each staging row and sets `PostoiDaNe` by calling the right `ImaNemaVoArtikli*` function.
4. After staging is built, opens **`frmVnesNaNoviMatECD`** (or `frmVnesNaNoviMat`) which lists materials that don't yet exist in `tblArtikli`. User either confirms importing them (opens `frmNovArtikal` for each missing), or — when the list is empty — runs the final `INSERT INTO FakturiU5 SELECT ... FROM TransferFakturiU5 LEFT JOIN qryMaterijali ...`, copying enriched data into the real table.

---

## 2. Validation / Triggers

### Form-level events

**`subFakturiU5.Form_BeforeInsert`** — only form-level validation. Auto-numbers `FakturaU5RBr` by querying max+1, copies `sValuta` (module global) into the new row's `Valuta`, copies parent's `ZaklucokBroj`, stamps `User = lKorisnik`.

**`frmFakturiU5.Form_Open`**:
- If `ProsecnaSTDaNe = True` enables `ProsecnaST` field (average customs rate).
- Computes `SaldoZavereni` and `SaldoSite` then sets two traffic-light colors:
  - Red and disable print: if `GarancijaIznos <= Saldo`.
  - Yellow if remaining ≤ 10% of guarantee.
  - Green otherwise.
- If `ArhivaDN = "D"` appends `(АРХИВА)` to caption.

**`frmFakturiU5Z.ZaverkaDatum_AfterUpdate`** — sets `PrijavaDatum = ZaverkaDatum`, computes `DatumRokDo = DateAdd("m", VratiOdobrenieRokDo(OdobrenieRBr), ZaverkaDatum)`, sets `PominatRokDaNe` flag if past due.

### Control-level events on `subFakturiU5`

**`ArtKatBrMat_Exit`** — the keystone validator:
- Empty → opens `frmPomosZaArtikli` (material mode, locked).
- Filled → looks up `tblArtikli WHERE ArtKatBr=... AND ArtKatTip=True`. If found, populates `ArtNazivMat`, `TarBr`, `EdMer`, `EdMerCar`, `Sirina=ArtKoefEDM`, `ZemjaPoteklo`, `ArtBezPref`, `SpecTez`, auto-fills `Tezina = Kol * SpecTez`, `M2 = Kol * Sirina`.
- For `Uvoznik = "DREKKV"` or `"ODW"` it does NOT overwrite manual `TarBr`/`ZemjaPoteklo`.
- NOT found → cancels exit and opens **`frmNovArtikal`** with `frmMat = True`, prefilled `ZBOR1 = sArtKatBrMat`.

**`Kol_Exit`** — calculates `Vrednost = Kol*Cena`, `DavackiEdinica = Davacki/Kol`, defaults `Sirina = 1`, computes `M2 = Kol*Sirina`. Special: if `EdMerCar = "KGM"`, `Tezina = Kol*Sirina`; else `Tezina = Kol*SpecTez`.

**`Kol_AfterUpdate`** (and `Vrednost_*`, `Sirina_*`, `M2_*`, `Tezina_*`) — if `NaimRBr > 0` (line already grouped), propagate delta to `NaimU5` via `UPDATE NaimU5 SET Kol = Kol + delta`.
**Hard guard**: if `VrednostBruto <> 0` (rabat distributed), blocks with "НЕДОЗВОЛЕНА ПРОМЕНА".

**`M2_Exit`** — back-computes `lNovaSirina = M2/Kol` and if differs from catalog, prompts user to update `tblArtikli.ArtKoefEDM` and conditionally `ArtSpecTez` when `ArtCarEDM = "KGM"`.

**`Tezina_Exit`** — symmetric for `SpecTez`: back-computes `lNovaSpecTez = Tezina/Kol`, prompts catalog update.

**`TARBR_Exit`** — branches on `Uvoznik`:
- `"DREKKV"`/`"ODW"`: validates tariff via `VratisNaziv(12, ...)`, fetches `EdMerCar = VratiEdMer(TarBr)`, prompts catalog update.
- Else: looks up `(ArtKatBr, ArtTarBr)` with `ArtKatTip=True`. If not found, opens `frmNovArtikal` with tariff split `TarBr/TAROZ1/TAROZ2/TAROZ3` (4/2/2/2 chars) and `ZBOR1 = ArtKatBr & "A"` (variant suffix "A"!).

**`EdMerCar_Exit`** — validates `(ArtKatBr, ArtTarBr, ArtCarEDM)` triple; if missing, opens `frmNovArtikal` with "A" suffix.

**`TarBr_KeyDown`** — F1 opens `frmTarifiranje` (DREKKV/ODW only).

**`Valuta_Exit`** — defaults `"EUR"`, remembers in module-global `sValuta`.

---

## 3. Popups / Helpers Triggered Automatically

| Trigger | Popup | Why |
|---|---|---|
| `ArtKatBrMat_Exit` empty | `frmPomosZaArtikli` | Article picker, material mode |
| `ArtKatBrMat_Exit` unknown | `frmNovArtikal` | Register new material |
| `TARBR_Exit` mismatch | `frmNovArtikal` | Register variant (ZBOR1 + "A") |
| `EdMerCar_Exit` mismatch | `frmNovArtikal` | Register variant (ZBOR1 + "A") |
| `cmdArtikli_Click` | `frmAzurArtikli` | Edit catalog |
| `cmdZemiOdIsp_Click` | `frmIzberiIspratnicaMat` | Pull lines from dispatch note |
| `cmdKoletiPoNaim_Click` | `frmVnesNaKoletiONaim` | Package counts per item |
| `cmdNetoNaim_Click` | `frmVnesNaTezina` | Total net weight |
| `frmNovTransferFakturaU5` after staging | `frmVnesNaNoviMatECD` | List of unknown materials |
| `frmVnesNaNoviMat` material matches deprecated `ArtKatBrStara` | `frmArtKatBrStara` | Disambiguate |
| `TarBr_KeyDown` F1 | `frmTarifiranje` | Tariff lookup |

`frmDuplikatiU5.Command2_Click` suffixes `FakturaU5Broj` with `ZaklucokBroj` across `FakturiU5Z`, `FakturiU5`, `LagerMaterijali` to break collisions.

---

## 4. Data Transformations / Calculations

### Per-line calculations
- `Vrednost = Kol * Cena` and inverse `Cena = Vrednost / Kol`
- `M2 = Kol * Sirina` (Sirina = EDM→EDMcar coefficient, NOT literal width)
- `Tezina` two paths:
  - If `EdMerCar = "KGM"` → `Tezina = Kol * Sirina` (kg/unit factor)
  - Else → `Tezina = Kol * SpecTez` (specific weight)
- `DavackiEdinica = Davacki / Kol` and inverse

### Catalog reconciliation back-writes
- `Tezina_Exit`: `UPDATE tblArtikli SET ArtSpecTez=new, ArtKoefEDM=IIf(ArtCarEDM='KGM', new, old)`
- `M2_Exit`: analogous for `ArtKoefEDM`
- `TARBR_Exit` (DREKKV/ODW): `UPDATE tblArtikli SET ArtTarBr=newTar, ArtCarEDM=VratiEdMer(newTar)`

### Line grouping → naimenovanija (`cmdVnesiNaim_Click`)
1. Wipes line-level Davacki (`= 0`)
2. Deletes `NaimU5` and `tblListaStavki`
3. Picks distinct `(TarBr, EdMerCar, ZemjaPoteklo)` and assigns sequential `NaimRBr`

### Naimenovanije aggregate roll-up (`cmdFormiraj_Click`)
1. `INSERT INTO NaimU5 SELECT ... FROM FakturiU5 GROUP BY NaimRBr` summing Tezina, TezinaBruto, Vrednost, Kol, M2
2. For each `NaimU5` row:
   - `dCarOsn = Vrednost * Kurs`
   - Average-rate mode: `dStapka = ProsecnaST`, `Carina = ProsecnaST*CarOsn/100`, `Danok = 0`
   - Book mode: `dStapka = VratiCarST(TarBr)`, `dDanStapka = VratiCarDanStLon(TarBr)`, `Carina = dStapka*CarOsn/100`, `dDanOsn = CarOsn + Carina`, `Danok = dDanOsn*dDanStapka/100`
   - If `fraDDV = 1`: `dVkupno = Carina + Danok`, else just `Carina`
3. Updates `NaimU5.{CarSt, Carina, Danok, Davacki, DavackiEdinica, Kurs, Naim}`

### Cost/discount distribution — `DodadiTrosociPoFakturaU5`
- `dTrosociVK = Trosoci - Rabat`
- For each line: share proportional to `Vrednost/VrednostVK`
- Most Uvoznik: `Vrednost += round(trosok*ratio, 2)`, `VrednostBruto := newVrednost` (locks further edits!)
- DREKKV: writes into `VrednostBruto` separately, scales `Davacki` by `VrednostBruto/Vrednost`

### Balance/guarantee functions
- `VratiSaldoNaDenDenesenZavereni(OdobrenieRBr, datum)` — sums certified `qryFakturiU5ZG20Zavereni.Davacki` minus `qryIspratniciZavereni.Davacki`
- `VratiSaldoNaDenDenesenSite` — same but for all (not just certified)

---

## 5. Side Effects

| Table | Where | Why |
|---|---|---|
| `FakturiU5` | All line-edit events, cmdPresmetaj, cmdVnesiNaim | Main detail |
| `FakturiU5Z` | cmdPotvrdi, cmdIzlez (delete orphan) | Header |
| `NaimU5` | cmdFormiraj INSERT, live maintenance via `UPDATE NaimU5 SET Kol += delta` | Grouped items |
| `TransferFakturiU5` | cmdPotvrdi DELETE+INSERT per Uvoznik | Staging |
| `tblArtikli` | TARBR/M2/Tezina_Exit user-confirm back-writes | Catalog reconciliation |
| `tblListaStavki` | Various | Derived listing |
| `FakturiU5ZaSRV` / `FakturiU5SRV` | cmdNaServer / cmdOdServer | SQL Server sync staging |
| `Fakturi_VGS`, `Stavki_VGS` | cmdIspratiNaSped | Spediter/forwarder XML file |
| `Invoice<Uvoznik>` | frmVnesNaNoviMat: Preneseno=True (JONSON) or DELETE (TEKSPORT) | Mark source consumed |
| `Zaklucoci` | cmdNovZaklucok | New closure |

**NOT directly written** from invoice entry: `Garancija`, `DavackiStavki`, Transport tables.

---

## 6. Integrations

- **SQL Server**: `cmdNaServer_Click` / `cmdOdServer_Click` sync via linked-table DAO (no stored procs). Uses magic `ZaklucokBroj='00000'` for server-side marker.
- **XML export (IspratiPredmet)**: Creates `C:\PREV\<Loc4>\<LocFull>\<DDMMYYYY>\` tree, creates temp MDB via `CreateDatabase(... dbVersion40)`, exports each table as XML, renames MDB to `.VGS`.
- **FTP**: `FtpFunkcii` writes classic `ftp.exe` script + `.bat`, plaintext credentials.
- **Tariff book**: `VratiCarST`, `VratiNAIS`, `VratisNaziv`, `VratiCarFI`, `VratiCarFU`, `VratiCarDanStLon` — query `KnigaNai` keyed by split (TarBr, TarOz1/2/3).
- **Excel import**: `ImportXLSU5` uses `DoCmd.TransferSpreadsheet acImport`.
- **Text export**: `ExportFakturiU5` builds semicolon-separated `.txt` files named `<Odobrenie>_<Datum>-<Zaklucok>_<Datum>-<FakturaBroj>.txt`.

---

## 7. 20 Gotchas / Business Rules

1. **`ArtKatTip` is Yes/No**: True = material ("M"), False = finished product ("P"). `frmPomosZaArtikli` switches mode via `frmMat = True`.

2. **Composite PKs**: `FakturiU5` = 6 columns, `FakturiU5Z` = 5 columns. `FakturaU5Datum` always `#MM-DD-YYYY#` literal.

3. **Magic `ZaklucokBroj='00000'`**: Server staging marker — prevents deletion of server-side headers.

4. **`VidUIS` values**: "IMA5" default or "IMA4" by global `lTipNaProgram`. `VidRegBrS = "S"` (Standard) hardcoded.

5. **Article-variant "A" suffix**: When `(ArtKatBr, TarBr)` or `(ArtKatBr, TarBr, EdMerCar)` doesn't exist but `ArtKatBr` does → `frmNovArtikal` prefills `ZBOR1 = sArtKatBr & "A"`. Users register variants by appending "A".

6. **Tariff format 4+2+2+2**: `TarBr + TarOz1 + TarOz2 + TarOz3`. Code is full of `Left(TarBr,4)`, `Mid(TarBr,5,2)`, etc.

7. **Uvoznik drives everything**: Global string. Special behaviors for DREKKV/ODW:
   - Preserve manual TarBr/ZemjaPoteklo on lookup
   - Costs go to VrednostBruto not Vrednost
   - F1 tariff dialog only for them
   - Validate tariff against KnigaNai first

8. **VrednostBruto as lock**: Non-zero means "costs distributed" → edits to Kol/Vrednost raise "НЕДОЗВОЛЕНА ПРОМЕНА!" Must press `cmdTrosokRabat_Click` first to unlock.

9. **Default Sirina=1, SpecTez=1**: Missing catalog data silently degrades to identity conversions.

10. **`EdMerCar = "KGM"` privileged**: Sirina literally = SpecTez, Tezina = M2. Several events mirror-copy these fields.

11. **Valuta defaults to "EUR"**: Hardcoded in `Valuta_Exit`, cmdPotvrdi, several SQLs.

12. **Auto-incrementing FakturaU5RBr** per (OdobrenieRBr, FakturaU5Broj, FakturaU5Datum): `MAX+1` with NO locking — concurrent edits would collide.

13. **Traffic light thresholds**: Hardcoded 10% of GarancijaIznos. Red = `BackColor = 255`, disable print.

14. **cmdIzlez auto-deletes empty headers**: If user exits with zero `FakturiU5` lines, deletes the `FakturiU5Z` row (except '00000').

15. **4-digit importer folder prefix**: `C:\PREV\<First4>\<FullName>\<DDMMYYYY>\` e.g. `C:\PREV\GENT\GENTHERM\22042014\`.

16. **`ImaNemaVoArtikli*` family** (8 variants): different matching semantics for different Uvoznik — MatSamoSifra, Mat (4-tuple), Mat4 (Havep), MatBezEdMer, DveSifri, Stara (old catalog), etc.

17. **`VratiEdMerKor`** — normalizes 6 possible supplier EdMer variants to canonical; returns "XXX" on miss.

18. **Module globals**: `sValuta`, `lKorisnik`, `Uvoznik`, `ArhivaDN`, `lTipNaProgram`, `BojaV`/`BojaI` — all invoice flow reads/writes them.

19. **Div-by-zero guards**: `NaimU5.DavackiEdinica = Davacki / IIf(IsNull(Kol) Or Kol=0, 0.0001, Kol)`. Silently wrong for zero Kol.

20. **DodadiTrosociPoFakturaU5 div-by-zero**: Uses 0.0001 if invoice sum is null/zero.

---

## Key File Paths

- `objects/forms/frmFakturiU5/procedures/` (18 procs)
- `objects/forms/subFakturiU5/procedures/` (33 procs — heart of per-line logic)
- `objects/forms/frmFakturiU5Z/procedures/` (8 procs)
- `objects/forms/frmNovTransferFakturaU5/procedures/cmdPotvrdi_Click.vba` (678 lines)
- `objects/forms/frmAzurZak/procedures/cmdNovaFakturaU5_Click.vba`
- `objects/forms/frmDuplikatiU5/procedures/Command2_Click.vba`
- `objects/modules/Funkcii/procedures/DodadiTrosociPoFakturaU5.vba`
- `objects/modules/Update/procedures/IspratiPredmet.vba`
- `objects/modules/Ftp/procedures/FtpFunkcii.vba`

Tables:
- `FakturiU5.md` (52 fields, 6-col PK)
- `FakturiU5Z.md` (142 fields, 5-col PK)
- `tblArtikli.md` (46 fields, PK = ArtRBr; key flag `ArtKatTip Yes/No`)
