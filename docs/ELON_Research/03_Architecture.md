# ELON Architecture, Helper Modules, Global State

## 1. Overall Architecture

**2-tier Access + SQL Server with fallback to local Jet tables.**

- Front-end: Access MDB (`E-Lon.mdb`)
- Back-end: per-customer **SQL Server** (linked tables via ODBC)
- Fallback: linked Access (Jet) tables in another shared MDB
- Multi-user: Yes (`Default Record Locking = 2` = Edited-record lock)
- Multi-tenant: Yes, via `Uvoznik` DB property
- Per-customer SQL server info in `Serveri` table
- Archive: separate SQL Server for historical data (own connection)

**Inventory:**
- 27 modules, 272 forms, 200 reports, 501 tables, 443 queries

## 2. Startup Flow

1. **AutoExec macro** → `CheckRefs()` (self-repair COM references, compiles via `SysCmd(504, 16483)`)
2. **StartupForm = frmPas** (login)
3. `AllowBypassKey = False` → SHIFT bypass disabled
4. `frmPas.Form_Open`: `lTipNaProgram = 1` (1=LON, 2=DROWBACK, 3=MAGACIN), `PostaviArhivaN`
5. `cmdProdolzi_Click`:
   - Resolves SQL server/user/pwd from `Serveri` table
   - Authenticates against `tblKorisnik<UVOZNIK>` table by `Korisnik/Lozinka`
   - Sets globals: `lKorisnik`, `sKorisnikImePrezime`, `lTipUser`
   - Inserts login row to `tblLog<UVOZNIK>`
   - If `MDBSQL=1`: calls `AplLinkTablesSQL(...)` to re-link all SQL tables
   - Updates `FakturiU5Z.DatumRokDo`, flags `PominatRokDaNe = Yes` if past due
   - `NovUpdate` (checks `C:\PREV\...` for updates - see below)
   - If `Uvoznik = "KNAUF"` → opens `frmIzberiZakKNAUF`
   - Else: `Kontrola()` (license check) → `frmKluc` (activation) OR `frmIzberiZak` (main menu)

## 3. Login / Security

- **Users in `tblKorisnik<UVOZNIK>`** per customer (columns: KorisnikRBr, Korisnik, Lozinka, Tip, KorisnikIme, KorisnikPrezime)
- **Clear-text passwords** in `Lozinka`
- **SQL Injection vulnerability**: `"SELECT * FROM tblKorisnik" & Uvoznik & " WHERE [Korisnik]='" & ime & "'"` (no parameterization)
- **Backdoor admin password**: `VratiAdminPass()` derives daily password from `Now()`:
  - Sum of DDMMYYYY digits + weekday → index into literal `"VELIBOR"` for lead char
  - Per-month char: Jan→N, Feb→B, Mar/Apr→R, May→J, Jun→N, Jul→L, Aug→G, Sep→P, Oct→T, Nov→E, Dec→K

## 4. Global State

### Module-level `Public` vars (Funkcii module):
- `BojaV = 8454143` — selected color
- `BojaI = 16777215` — white/edited color
- `lKorisnik` (Long) — current user RBr (142 refs!)
- `sKorisnikImePrezime` — name Cyrillic
- `sKorisnikImePrezimeSTR` — name Latin
- `lTipUser` — role (0=ordinary, 1=admin)
- `sLozinka` — current password (for re-auth)
- `BrojacNaSlogovi` — global record counter
- `pArtNazivORG/MK/Zemja/KatEDm/TarBr/TarOzn1-3/ArtCarEDM/SpecTez/KoefEDM` — scratch for multi-form flows
- `lTipNaProgram` — 1=LON, 2=DROWBACK, 3=MAGACIN

### DB Properties (CurrentDb.Properties):
- `Uvoznik` (text) — customer code — THE multi-tenant key
- `Tip` (long) — license type (0=Dev, 1=Rent, 2=Trial, 3=Demo)
- `VGSV`, `VGST` — license validity / trial minutes
- `sVGSKorisnikMBr/RBr/Lokacija` — license identity
- `Arhiva` (D/N) — archive mode flag — adds `(АРХИВА)` to title
- `MDBSQL` (long) — 1=use SQL, else local
- `ZI` — current closure dispatcher lock ID

## 5. Multi-Tenant (Uvoznik)

**THE MOST PERVASIVE CONCEPT** — referenced 434 times in VBA.

50+ customer codes: TEKSPORT, DREKKV, MAGNA, JONSON, LEARGV, HAVEP, VAHOSK, KNAUF, GENTHERM, JOHNSONST, DELPHI, ARCELORMITTAL, VAV1SK_K, VAV1SK_R, VISTSK, ALLIANCE, BMZ, COMFY, KORONA, SUTAS, OLDISK, ZIBARS, IMPERIAL, KADORO, ETERNA, ZORLU, NEKST, KEYSKI, EKSPLO, MAKKOZA, GLOBAL, PIERIK, DEMO...

### How Uvoznik switches behavior:
1. **Per-customer tables**: `AplLinkTables.AplLinkTablesPersonal=True` → appends Uvoznik to table name (`tblKorisnikTEKSPORT`, `InvoiceDREKKV`, `CosortGENTHERM`, etc.)
2. **Per-customer SQL server** via `Serveri` table keyed by `ServerKorisnik=Uvoznik`
3. **26 dedicated `frmTransfer*` forms** — one per customer importing Excel/CSV
4. **Customer-specific modules**: `LearGV`, `Delphi` for their BOM/invoice imports
5. **Special startup branches**: KNAUF opens `frmIzberiZakKNAUF`, others `frmIzberiZak`
6. **License/FTP**: creates `C:\PREV\<first4>\<Uvoznik>` with FTP creds `<Uvoznik>@dckdata.mk` / `<Uvoznik>123#`

### Adding new customer requires:
- Create `tblKorisnik<X>`, `tblLog<X>`
- Optional `frmTransfer<X>`
- Add `Case "<X>"` to `UvoznikN` (full company name/address/EDB)
- Register in `Serveri` with SQL host
- Set `Uvoznik` DB property in customer's MDB copy

## 6. XML/FTP Integrations

### XML — Macedonian Customs Administration
- `objects/modules/XML/KreirajXML(sTipXML, sImeNaDat)` — serializes via ADODB.Stream to `<project>\XML\<name>.xml`, **opens in Notepad** (user manually uploads!)
- Metadata-driven via `PEE_XML` table (tag, level, type 0/1/99/2)

### PEE Message Types (customs declaration XMLs):
| Msg | Purpose |
|-----|---------|
| PEE010 | Razdolzuvanje (re-export clearance / guarantee discharge) |
| PEE020 | Razdolzuvanje via konecno uvozno carinenje (final domestic clearance) |
| PEE030 | Razdolzuvanje via povtoren izvoz (re-export) |
| PEE040 | Razdolzuvanje via unishtuvanje (destruction) |
| PEE050 | Glavno dobien proizvod + upotrebeni materijali |
| PEE060 | Zadolzuvanje/Razdolzuvanje po Tarifna Oznaka |

**NO inbound XML processing** — operator transcribes responses manually from customs portal!

### FTP — `ftp.dckdata.mk` (vendor)
- `Ftp/FtpFunkcii.vba` — classic Win32 `ftp.exe` scripting, plaintext creds
- `Update/PobarajLicenca.vba` — upload license request (.PLC file)
- `Update/NovaLicenca.vba` — download matching `LC*` file, decode activation key
- `Update/NovUpdate.vba` at login, scans `C:\PREV\Tarifa`, `C:\PREV\Kulis`, `C:\PREV\<first4>\<Uvoznik>` for:
  - `.CTR` → new customs tariff
  - `.CKL` → new exchange rate list
  - `LC*` → license file
  - `.DCK` → new customs cases
- `Update/IspratiPredmet.vba` — export customs cases: temp MDB with tables → `.xml` via `Application.ExportXML` → rename to `.VGS`

## 7. License Security

### Homegrown obfuscated keys tied to "fake register number":
- `Tip` ∈ {0=Dev, 1=Rent, 2=Trial, 3=Demo}
- `VGSV` — valid-until day
- `VGST` — trial minutes (start 480, decremented by `NamaluvajTrial`)
- `sVGSRegBr()` generates dashed 12-char code from MB(7) + VGSV(5)
- `Shiftiraj.vba` — alternating digit +1/-1 cipher
- `VratiOdRegBr` — decode RegBr variants
- `KriptirajIme`/`DeKriptirajIme` — obfuscate license filenames on FTP

### Hardening:
- `AllowFullMenus`, `AllowBuiltinToolbars`, `AllowBreakIntoCode`, `AllowSpecialKeys`, `AllowBypassKey` all OFF
- `VkluciIskluciShift` toggles emergency access

## 8. Top 20 Helper Functions

| # | Function | Module | Purpose |
|---|----------|--------|---------|
| 1 | `VratiDecZaSQL` (589 refs) | Funkcii | Format Double → invariant decimal (comma→dot) |
| 2 | `Uvoznik` (434) | Funkcii | Current customer from DB property |
| 3 | `lKorisnik` (142) | Funkcii | Global user RBr |
| 4 | `VratiArtNazivMK` (99) | Funkcii | Article MK description |
| 5 | `UvoznikN` (67) | Funkcii | Full importer name/address/EDB block |
| 6 | `VratiUvoznik` (63) | Funkcii | Wrapper around Uvoznik |
| 7 | `VratiPath` (37) | Funkcii | Path splitter (manual char-by-char) |
| 8 | `UvoznikPodatoci` (37) | Funkcii | Parse UvoznikN by code (1=Naziv, 2=Adresa, 3=EDB...) |
| 9 | `DaliEZakluceno` (23) | Funkcii | Is closure locked? |
| 10 | `ImaNemaVoArtikli` (21) | Funkcii | Article exists? (products, ArtKatTip=No) |
| 11 | `VratiArtRBr` (19) | Funkcii | ArtKatBr → ArtRBr |
| 12 | `VratiOdobrenieBroj` (18) | Funkcii | Approval number |
| 13 | `VratiNAIS` (18) | Funkcii | Tariff description from KnigaNai |
| 14 | `VratiAdminPass` (18) | Funkcii | Daily admin backdoor |
| 15 | `VratiArtPoteklo` (15) | Funkcii | Country of origin |
| 16 | `VratiOdobrenieDatum` (13) | Funkcii | Approval date |
| 17 | `PostoiTabela` (13) | FunkciiVGS | Safe "table exists?" check |
| 18 | `VratiCarST` (10) | Funkcii | Tariff rate from KnigaNai |
| 19 | `VratiArtFaza` (9) | Funkcii | Production phase |
| 20 | `VratiKurs` (8) | Funkcii | FX rate from FakturiU5Z.Kurs |

### Honorable mentions:
- `BrisiTabela` (FunkciiVGS) — safe delete + PostoiTabela check
- `VratiSoZbor` (SoZborovi) — number-to-Macedonian-words
- `VratiKonvertiranString` (Konverzija) — Latin↔Cyrillic via 30+ Replace
- `KojKompjuter` — machine name
- `Belo`/`Plavo`/`Zolto` (FunkciiVGS) — screen colors

## 9. FrmPrvaPomos Family

"First help" modal popups for correcting a single LagerMaterijali row in-place:
- **FrmPrvaPomos** — base: edit Kol/Vrednost/Davacki/Normativ via deltas
- **frmPrvaPomos01** — variant without "Dodadi" button
- **frmPrvaPomosDodadi** — adds `cmdDodadi` which INSERTS new LagerMaterijali row with `Proces=8` (Vrakanje/return)
- **frmPrvaPomosTezina** — weight only

Pattern: `*BCK` hidden controls snapshot current values, `_Exit`/`_AfterUpdate` handlers re-derive each other, `cmdUnDoNormativ`/`cmdUnDoDavacki` restore from snapshots.

## 10. Archiving / Deletion

### Arhiviranje module:
- `SamoBrisenjeNaArhiva(lOdobrenieRBr, bZavrsi, bBrojNaSlogovi)` — push to archive SQL
  - For each root table: link `<Name>A` archive twin, INSERT SELECT WHERE `Arhivirano<>True`, drop link, UPDATE `Arhivirano=True`
  - Then for nalog/deklaracija: archive dependent rows + DELETE locally
  - Progress to `Forms("frmArhiviranje").txtTabela`
- `ProverkaArhiva(lOdobrenieRBr)` — garbage-collector that deletes orphaned parents

### Brisenje module:
- `BrisiTabeliPoZaklucok(lOdobrenieRBr, sZaklucokBroj)` — config-driven cascade delete
  - Table list in `BrisiPoZaklucok` config table
  - Admin-only (hidden if `lTipUser=0`)
- `BrisiTabeliPoU5(sFakturaU5Broj, dFakturaU5Datum)` — same pattern for U5 declarations

## 11. Process Codes (LagerGotoviProizvodi/LagerMaterijali.Proces)

| Code | Meaning |
|------|---------|
| 1 | Propratnica / distributed |
| 6 | Izdatnica / in production / produced |
| 7 | Ispratnica izvoz / exported |
| 8 | Ispratnica materijali vrakanje / final import (return) |
| 9 | Otpad / waste/scrap dispatch |

**Release calculation** = `Proces IN (7, 8, 9)`.
