# ELON Customs/Guarantee/Export System

## Executive Summary

ELON is a **inward-processing customs** system. Importers bring fabric under a customs guarantee bond, distribute to producers, manufacture garments, export. On export, the duty tied to consumed materials is **released** from the guarantee.

**The core state machine is `LagerMaterijali.Proces`**:
- 1 = entered/distributed
- 6 = in production
- 7 = exported
- 8 = final domestic import
- 9 = waste/otpad

**NO `xbk_CalculateCustomsExpenses`** function exists in legacy! Duty calc is 3-tier VBA:
1. `PresmetajDavackiPoNaim` → NaimU5 rollup
2. `RaspredeliDavackiPoStavki` → pro-rata back to FakturiU5
3. `IzednaciDavackaPoLager` → propagate to LagerMaterijali

## 1. Duty Calculation (Davacki)

### Formula per NaimU5 row (PresmetajDavackiPoNaim.vba):
```
dCarOsn  = Vrednost * Kurs               ' customs base
dStapka  = VratiCarST(TarBr)             ' book rate from KnigaNai
dCarina  = dStapka * dCarOsn / 100       ' customs duty
dDanOsn  = dCarOsn + dCarina             ' VAT base
dDanok   = dDanOsn * dDanStapka / 100    ' VAT
IF bDDV:     dVkupno = dCarina + dDanok
IF NOT:      dVkupno = dCarina
```

### Average-rate override:
If `ProsecnaSTDaNe=True`: bypass per-tariff lookups, apply single fixed rate, VAT=0.

### Pro-rata spread to FakturiU5 lines:
```sql
UPDATE FakturiU5 SET
    Davacki  = Vrednost / dVrednost * dDavacki,
    Carina   = Vrednost / dVrednost * dCarina,
    Danok    = Vrednost / dVrednost * dDanok,
    DavackiEdinica = ... / Kol,
    StatVred = Vrednost * dKurs,
    CarSt    = dCarSt
WHERE ...
```

### Rate Lookups:
- `VratiCarST(sTB)` — splits 10-digit tariff, reads `KnigaNai.ST`
- `VratiCarDanStLon(sTB)` — defaults VAT to 18%, overrides from `CarTarPovlasteniDDV.DDV`

### Preferential origin (VratiCarSTPref, VratiElementiPref):
Consults `Preferencijal` → `AneksiTB` → `Aneksi.ST<year>`. Only EU and TR have separate paths.

Dynamic column names per year: `ST2026`, `FST2026`, `PV2026`, `KontST2026`, `Asy2026`. Adding a new year = ALTER TABLE!

### Trosoci/Rabat (landing costs):
`DodadiTrosociPoFakturaU5` proportionally redistributes `(Trosoci - Rabat)`:
- Standard: `Vrednost += Round(trosok * (Vrednost/VrednostVK), 2)`
- DREKKV: goes to `VrednostBruto` separately, scales `Davacki * VrednostBruto/Vrednost`

## 2. Guarantee (Garancija)

### Where it lives:
On `Odobrenija` row (one per approval):
- `GarancijaBroj`, `GarancijaDatumOd`, `GarancijaDatumDo`
- `GarancijaIznos` (Double) — total bond amount
- `GarancijaKorekcija`, `GarancijaKorekcijaD` — manual corrections

### Form:
`frmAzurGarancija` is almost a stub — just `cmdIzlez_Click`, `cmdKorekcija_Click`. **Bond entered once per authorization, not auto-generated.**

### No enforced referential integrity!
`Odobrenija.GarancijaIznos` is a free scalar. Nothing prevents SUM(debits)-SUM(credits) from exceeding it. Balance reporting is advisory.

### Balance calculation (VratiSaldoNaDenDenesen.vba):
```
dZadolzuvanje  = SUM(qryFakturiU5ZG20.Davacki) WHERE ZaverkaDatum<dDatumOd   ' charges
dRazdolzuvanje = SUM(qryIspratnici.Davacki)    WHERE ZaverkaDatum<dDatumOd   ' releases
Saldo = Zadolzuvanje - Razdolzuvanje + VratiSaldoKorekcija(...)
```

- `qryFakturiU5ZG20` = all U5 lines where `ZaverkaBroj` filled (charged)
- `qryIspratnici` = LagerMaterijali rows with `Proces IN (7,8,9)` JOIN Ispratnici

Month-end snapshots → `tblSostojbaNaGarancija`.

## 3. Guarantee Release (Razdolzuvanje)

### Implicit via ledger arithmetic
Release is NOT a single event — it's an aggregate difference:

```sql
-- Entry: what was imported
SELECT SUM(Kol), SUM(Davacki), SUM(Vrednost) FROM FakturiU5
WHERE OdobrenieRBr=? AND FakturaU5Broj=? AND ArtKatBrMat=?

-- Exit: what was exported (Proces 7,8,9)
SELECT SUM(Kol), SUM(Davacki), SUM(Vrednost) FROM LagerMaterijali
WHERE Proces IN (7,8,9) AND ... AND ArtKatBrMat=?

-- Remaining = Entry - Exit
```

### Formally ratified via FakturiU5Z.RazdolzenaDaNe
Set by user in `frmZaveriRazdolzeniU5`:
```vba
If IsNull([VidRazdBrS]) Or IsNull([RazdBroj]) Or IsNull([RazdDatum]) Then
   [RazdolzenaDaNe] = False
Else
   [RazdolzenaDaNe] = True
End If
```

**Manual flip** — system does NOT auto-mark closed when balance hits 0.

### Linkage material-consumed → guarantee-released:
Every LagerMaterijali row preserves:
- OdobrenieRBr, ZaklucokBroj — closure keys
- GotovProizvodRBr — which finished product consumed this
- FakturaU5Broj, FakturaU5Datum — which import funded it
- ArtKatBrMat — which material
- LagerRBrGP → LagerGotoviProizvodi.LagerRBr (actual GP row)
- Davacki, DavackiEdinica — duty carried

On export, NEW row inserted with `Proces=7` preserving all these keys.

### frmRazdolzuvanjeZak
Main release screen. Buttons:
- `cmdRazdolzi_Click` → opens `frmZadolzuvanjeZ`
- `cmdXML_PEE060_Click` → builds PEE060 XML
- `cmdPecatiTarBr/Cont/Zbirno` — printouts
- `cmdPrivremenUvoz` — temp import
- `cmdPecatiVrakanje` — return

Subform `subRazdolzuvanjeZakEX` shows exports (Proces ≥ 7) per U5 line.

## 4. Distribution to Producers (Podelba)

### Data model
Nothing physical moves. System inserts `LagerMaterijali` rows with `Proces=1` for producer.

### Main form: `frmPodeliBaranjaBrz`

### Per-size tracking: NormativiVelicini
```
OdobrenieRBr, ZaklucokBroj, GotovProizvodRBr, NormativRBr
VelicinaRBr    ← size id
VelicinaNaziv  ← "S","M","L","XXL",...
Kol            ← pieces of that size
Normativ       ← per-unit material for this size
KolMat         ← qty of this material for that size
```

### Izdatnici vs Ispratnici:
- **Izdatnici** = internal material issue (Proces=6, produced)
- **Ispratnici** = shipping/export (Proces=7, exported)

`cmdKreirajIspratnica_Click` does BOTH in one click:
```vba
'Proizvedeno - Proces=6 clone
INSERT INTO LagerGotoviProizvodi ... Proces=6 ... FROM ... Proces=1

'Izvoz - Proces=7 clone
INSERT INTO LagerGotoviProizvodi ... Proces=7 ...
INSERT INTO LagerMaterijali ... Proces=7, 
   Kol=Kol-KolOtpad,
   Davacki=Davacki-(KolOtpad*DavackiEdinica)
```

### Per-Zaklucok/Producer/Size:
- `LagerMaterijali.ZaklucokBroj`, `LagerGotoviProizvodi.ZaklucokBroj` — required
- `LagerMaterijali.Proizvoditel` (Long FK) + `ProizvoditelN` (text) — producer
- `NormativiVelicini.VelicinaRBr/Naziv` — size (only normatives carry size, ledger aggregates)

## 5. Stock Ledgers (Lager)

| Table | What | Proces codes |
|-------|------|--------------|
| `LagerMaterijali` | Raw materials | 1=distributed, 6=production, 7=exported, 8=final import, 9=waste |
| `LagerGotoviProizvodi` | Finished products | 1=planned, 6=produced, 7=exported |
| `LagerMaterijaliVoOtpad` | VIEW — LagerMaterijali with Proces=9 | 9 only |
| `LagerMaterijaliOtpad` | Link: LagerRBrOtpad→LagerRBr/NormativOtpad/KolOtpad | n/a |

### When each row is written:
- **Proces=1**: on import (frmFakturiU5 cmdPresmetaj) AND on podelba
- **Proces=6**: producer receiving material (PriemnicaPodelbaOdFazaVoFaza)
- **Proces=7**: on export (ZapisVoIspratnica)
- **Proces=9**: on waste declaration (ZapisVoMagacinOtpad)
- **LagerGotoviProizvodi Proces=1/6/7** — on Podelba, Proizvodstvo, Izvoz

### Balancing: IzednaciDavackaPoLager
Averages `LagerMaterijali.Davacki` per material per U5 invoice — triggered manually from frmFakturiU5.

### Reconciliation query:
`queries/LagerMaterijali Without Matching FakturiU5Z.sql` — flags orphan LagerMaterijali rows referencing non-existent U5.

## 6. Export Invoice Flow

### The core chain: frmPodeliBaranjaBrz.cmdKreirajIspratnica_Click (14-149 lines)
1. Create Izdatnica: `INSERT INTO Izdatnici (IzdatnicaRBr, IzdatnicaDatum, OdobrenieRBr)`, `IzdatnicaBroj="<n>/<yyyy>"`
2. Clone GP as Proces=6 (produced)
3. Create Ispratnica: `INSERT INTO Ispratnici ... VidUIS='EXA3', VidRegBr='R', VrakanjeDaNe=False`
4. Clone GP as Proces=7 (exported)
5. Insert LagerMaterijali Proces=7 with net quantities + pro-rata duty:
   ```
   Kol = LagerMaterijali.Kol - LagerMaterijali.KolOtpad
   Davacki = Davacki - (KolOtpad * DavackiEdinica)
   Vrednost = Vrednost - (KolOtpad * Cena)
   Tezina = Tezina - ((KolOtpad/Kol) * Tezina)
   ```
6. Link `LagerRBrGP = <new LagerRBr of GP>` — parent-child pointer

### How export triggers release:
**Automatic and implicit**: Proces=7 row in LagerMaterijali with positive `Davacki` → drops running balance via `qryIspratnici`.

**Formally ratified** when user completes `frmGotoviProizvodiIzvozZaverka` and sets `Ispratnici.ZaverkaBroj/Datum/VidUIS/CarOE`.

### ZapisVoIspratnica.vba (older granular version):
```
dKoefKol = dKolZaVnesU5 / dKolU5          ' fraction being exported
dDavZaVnesU5 = (dKolZaVnesU5 - dOtpadKolZaVnesU5VK) * dDavackiEdinica
```
Then INSERT INTO LagerMaterijali with Proces=7, and `ZapisVoMagacinOtpad` for Proces=9 waste.

### IzramniIzvozOtpad.vba — reconcile export+waste
When sum(Proces=7) = Proces=1 (material fully consumed), subtracts proportional Proces=9 from Proces=7 rows to avoid double-count.

## 7. KnigaNai / Carinska Tarifa

### Tables:
- **KnigaNai** — master tariff (PK=TARSIF 24-char; TARBR+TAROZ1+TAROZ2+TAROZ3, NAI, ST rate, EDMER, FI/FU form codes, PV specific duty, DDV VAT, Ex exceptions, NaiS short name)
- **CarTarPovlasteniDDV** — preferential VAT table
- **Preferencijal** — country → pref code (EU, TR, CH, UA, ...)
- **Aneksi + AneksiTB** — annex rates per year (ST2024, FST2024, KontST2024, Asy2024 — dynamic column names!)
- **PrefST** — preferential base rate per commodity group
- **PrefIsk** — preference exceptions
- **RezimiTB** — regime overrides for FI/FU

### Usage:
- `VratiCarST(TarBr)` → KnigaNai.ST (basic rate)
- `VratiCarDanStLon(TarBr)` → CarTarPovlasteniDDV.DDV (default 18)
- `VratiNAIS(TarBr)` → trade name
- `Preferencijali(TarSif)` → human-readable preferential summary

### Maintenance:
`frmOdobrenijaKnigaNai` links authorization to allowed tariffs. Each year = ALTER TABLE Aneksi + PrefST + PrefIsk with `ST<year>` columns.

## 8. Customs Communication (VGS)

### Protocol: file-based XML (!)
`KreirajXML` writes `<project>\XML\<name>.xml` via ADODB.Stream UTF-8, then **opens in Notepad** — user uploads manually to customs portal.

### XML generation is metadata-driven via PEE_XML:
- Each row: PEETag (name), PEENivo (indent), PEETip (0=raw, 1=open, 99=close, 2=repeat-over-table)
- Field suffixes control generation:
  - `_Tag01` → open tag
  - `_Tag99` → close tag
  - `_Tbl` → recurse via `VratiXMLStringOdTabela`
  - `ID01`, `ID02` → skip (linkage only)

### PEE060 example (`cmdXML_PEE060_Click.vba`):
```
sVidRegBr = "R_S"
sImeXML = "PEE060_" & sVidRegBr & sZaverkaBroj & "_" & sCarOE & "_" & lGodina
```

Envelope: sender = UvoznikPodatoci(1), recipient = Odobrenija.OdobrenieCarOE, 
Recipient_identification_code_qualifier='C5', hardcoded credentials 
Interchange_control_reference='9999', Recipients_reference_password='111111'.

Body table `PEE060_ZadolzuvanjeRazdolzuvanjeTarifnaOznaka`:
```sql
SELECT TarBr, ZemjaPoteklo AS Poteklo,
       Sum(Kol), First(EdMer), Sum(M2) AS KolCar, 0 AS KolCarRazd,
       Sum(Davacki), 0 AS DavackiRazd, Sum(Vrednost), 0 AS VrednostRazd
FROM FakturiU5 GROUP BY OdobrenieRBr, FakturaU5Broj, TarBr, ZemjaPoteklo
```

Then walks LagerMaterijali Proces>6 to update the `_temp` columns with actual released qty.

### NO inbound
Only outgoing. Operator manually transcribes ZaverkaBroj/ZaverkaDatum from customs response into `FakturiU5Z` / `Ispratnici` via forms.

### FunkciiVGS is NOT customs-specific!
Just general utility library (VGS = developer/company initials). Has `BrisiTabela`, `PostoiTabela`, `Belo/Plavo/Zolto` (colors), `NumerirajJCS`, etc.

## Cross-cutting Observations

1. **Proces state machine is everything** — understand it = 90% of the app
2. **Davacki flows as unit cost** (`DavackiEdinica`) from NaimU5 → FakturiU5 → every LagerMaterijali row — always preserved per-unit
3. **No enforced guarantee ceiling** — free scalar on `Odobrenija.GarancijaIznos`
4. **Importer-specific branches** for DREKKV, MAKSTIL in cost distribution + export
5. **"_nov" forms** suggest in-progress rewrite (simpler variants exist)
6. **Year-indexed tariff columns** — adding new year needs ALTER TABLE
