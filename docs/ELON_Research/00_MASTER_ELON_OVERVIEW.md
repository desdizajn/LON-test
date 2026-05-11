# ELON — MASTER OVERVIEW
## Консолидиран преглед на 30-годишна Access апликација за царински workflow

> Овој документ е консолидација на 5 параленли истражувања + мои директни наоди.
> Секоја секција има reference кон детален извештај во истиот фолдер.

---

## 🎯 Што е ELON

**Hybrid Access front-end + SQL Server back-end** систем за **inward processing (aktivno obletuvanje)** царински workflow, направен за мал конфекциски производител во Северна Македонија кој произведува за 50+ странски клиенти (Uvoznici).

**Обем**: 272 форми, 27 модули, 501 табела, 443 queries, 200 извештаи, 3053 VBA процедури класифицирани.

**Business flow грубо**:
```
IMPORT (fabric) → GUARANTEE (bond loaded)
   ↓
DISTRIBUTE to producers → PRODUCE garments
   ↓
One of 3 outcomes:
   a) EXPORT finished products (guarantee released)
   b) RETURN materials (guarantee released)
   c) DECLARE AS WASTE (guarantee released)
   ↓
CERTIFICATION by customs inspector (Zaverka)
   ↓
GUARANTEE RELEASED (Razdolzuvanje)
```

---

## 🏛️ Три централни концепти (хиерархија)

| Концепт | Број | Што е |
|---------|------|-------|
| **Odobrenie** | ~10-100 per client | Царинско одобрение (approval). Централен контејнер. Содржи **GarancijaIznos**. |
| **Zaklucok** | Многу по Odobrenie | Затварање/batch. Логички циклус: увоз + производство + извоз. |
| **FakturaU5** | По Zaklucok | U5 увозна фактура за материјали. |

**Сè виси од оваа хиерархија**. Секоја табела има `OdobrenieRBr + ZaklucokBroj` како основа на клуч.

---

## 🔑 THE MASTER SWITCH: `Uvoznik`

**Најпрваливна концепт во апликацијата — референциран 434 пати во VBA.**

`Uvoznik` е string (TEKSPORT, DREKKV, MAGNA, JONSON, LEARGV, HAVEP, KNAUF, GENTHERM, DELPHI...). Го диктира:

1. **Кои табели се линкуваат** — `tblKorisnik<Uvoznik>`, `Invoice<Uvoznik>`, `Cosort<Uvoznik>`
2. **Кој SQL Server** — `Serveri` table has per-Uvoznik credentials
3. **Кое import UI** — 26 посебни `frmTransfer<Uvoznik>` форми
4. **Custom logic branches** во DodadiTrosociPoFakturaU5, ZapisVoIspratnica, cmdNovTransferFakturaU5
5. **License/FTP** — `<Uvoznik>@dckdata.mk`, `C:\PREV\<first4>\<Uvoznik>`
6. **Startup form** — KNAUF → `frmIzberiZakKNAUF`, else `frmIzberiZak`

Се менува преку **Ctrl+F12** (AutoKeys → `SmeniUvoznik()`).

---

## 🔄 THE CORE STATE MACHINE: `Proces`

Сè во магацинот е **ЕДЕН integer** (`LagerMaterijali.Proces`, `LagerGotoviProizvodi.Proces`):

| Код | Значење |
|-----|---------|
| **1** | Propratnica / распределено кон производител |
| **6** | Izdatnica / во/испратено од производство |
| **7** | Ispratnica izvoz / извезено |
| **8** | Vrakanje / конечен увоз (враќање) |
| **9** | Otpad / отпад |

**Release calculation** = `Proces IN (7, 8, 9)` — сите три се "consumed" против гаранцијата.

---

## 🧠 Моите клучни откритија

### 1. Материјалите имаат 3 излези (не 2)
Корисникот мислеше дека има само "извоз на готов производ". Всушност:
- **Извоз на готов производ** (Proces=7) → `frmGotoviProizvodiIzvoz`
- **Враќање на материјал** (Proces=8) → `frmMaterijaliVrakanje`
- **Декларација за отпад** (Proces=9) → `frmMaterijaliOtpad`

Сите 3 носат **Zaverka** (царинска сертификација) — засебни форми: `frmMaterijaliOtpadZaverka`, `frmMaterijaliVrakanjeZaverka`, `frmGotoviProizvodiIzvozZaverka`.

### 2. Ispratnica е паралелен систем — 457 code refs!
Секој материјален "movement" има Ispratnica (отпратница). Централна форма: `frmIspratnici`. Multiple modes преку `VidUIS` ('EXA3' за извоз).

### 3. ECD интеграција — автоматски преземани декларации
`frmTransferECD` + `frmVnesNaNoviMatECD` — `cmdPrevzemanjeECD_Click` преземa од Electronic Customs Declaration систем, calls `NovUpdate` first.

### 4. MozniMinusi — анализа на недостатоци
`cmdMozniMinusi` на `frmIzberiZak` открива "negative stock discrepancies" — закажано vs она што е реално во магацин.

### 5. Zaverka patterns — inspector валидација
Buttons `cmdZaverka` на `frmAzurZak`, `frmInspektor` form. Контролирано со 117 code references.

---

## 📋 Детален преглед по домен

Клик на секој линк за длабински преглед:

### [01_Faktura_Flow.md](./01_Faktura_Flow.md) — Внес на фактури
- **Два patha**: Manual (frmNovaFakturaU5) vs Bulk Transfer (frmNovTransferFakturaU5 — 678 lines)
- **subFakturiU5** — срцето на per-line логиката, 33 процедури
- **Line events**: ArtKatBrMat_Exit, Kol_Exit, TARBR_Exit, EdMerCar_Exit... сите прават катaлог-реконилијација во живо
- **20 gotchas**
- **Traffic light** на гаранција (semafor: црвен/жолт/зелен)

### [02_Articles_Materials.md](./02_Articles_Materials.md) — Каталог tblArtikli
- **ArtKatTip**: True=материјал, False=готов производ
- **ArtBezPref INVERTED**: True = БЕЗ преференција (non-EU)!
- **ArtKoefEDM полиморфно**: unit coefficient ИЛИ width (sirina)
- **TAROZ3="00" hardcoded** во auto-prefill
- **"A" суфикс** за варијанти
- **Cancel-new физички брише** (no soft-delete)
- **Edits locked after first use** — DREKKV bypass

### [03_Architecture.md](./03_Architecture.md) — Архитектура + модули
- **Startup**: AutoExec → CheckRefs (self-repair) → frmPas login → tblKorisnik<Uvoznik>
- **Security**: Plain-text passwords, SQL injection surface, `AllowBypassKey=False`
- **Backdoor admin password**: `VratiAdminPass()` — daily key from `Now()` + "VELIBOR" + per-month char
- **PEE010-060 XML** → Notepad → manual upload (no SOAP!)
- **FTP to ftp.dckdata.mk** — plaintext creds, license/tariff/updates
- **Top 20 functions** — `VratiDecZaSQL` (589 uses), `Uvoznik` (434), etc.

### [04_Customs_Guarantee_Export.md](./04_Customs_Guarantee_Export.md) — Царина/Гаранција/Извоз
- **Davacki calc 3-tier**: NaimU5 → RaspredeliDavackiPoStavki → IzednaciDavackaPoLager
- **Preferential origin**: year-indexed columns (`ST2026`, `KontST2026`...)
- **Guarantee**: free scalar, no enforcement
- **Razdolzuvanje**: implicit via ledger subtraction
- **Export chain**: 6 steps, Izdatnica + Ispratnica in one click
- **KnigaNai**: customs tariff book, every year = ALTER TABLE

### [05_Normatives_Waste_FinishedProducts.md](./05_Normatives_Waste_FinishedProducts.md) — Нормативи/Отпад/ГП
- **Normativ vs NormativNalog**: effective vs planned (cmdVratiPlaniran restores)
- **4 waste slots + Zaguba** (non-recoverable loss)
- **NormativTemplO/S templates** — auto-applied for LEARGV/DELPHI/GENTHERM
- **Size-specific normatives**: NormativiVelicini with weighted-average back-propagation
- **Skart vs Otpad**: defective incoming vs manufacturing by-product
- **Producer assignment**: GotoviProizvodi.Proizvoditeli = comma-joined text list!
- **Inflate-for-waste**: DREKKV/TEKSPORT `KolMat * 100 / (100-ArtOtpadProc)` at import

---

## 🚨 Најкритични business rules / gotchas

### Database
1. **Empty = single space " "** (not NULL) во текст колони
2. **ArtKatBr НЕ е DB-unique**, само (ArtKatBr, ArtKatTip) UI-enforced
3. **DMax+1** за сите auto-increments (race condition risk)
4. **Magic `ZaklucokBroj="00000"`** — server staging marker

### Business logic
5. **ArtBezPref INVERTED** — True = БЕЗ преференција!
6. **VrednostBruto as lock** — non-zero блокира промени, `cmdTrosokRabat` отклучува
7. **Traffic light thresholds** — hardcoded 10% на гаранција
8. **DREKKV special cases** навсекаде (has override permissions others don't)
9. **Cancel-new физички брише** — no soft-delete
10. **"A" суфикс** за варијанти на ист артикал со различна tarifa

### Integration
11. **XML отвора Notepad** — нема автоматско submission
12. **Clear-text passwords** + **SQL injection** surface
13. **Inflate-for-waste** (DREKKV/TEKSPORT): `KolMat * 100/(100-otpad%)` при import
14. **Template auto-apply** (LEARGV/DELPHI/GENTHERM): zero keystrokes за repeat products — НАЈГОЛЕМИОТ productivity win

### Tariff
15. **Year-indexed columns** (`ST2026, KontST2026`): секоја година = ALTER TABLE
16. **TarBr = 4+2+2+2 chars**, concatenation при save, split при read
17. **TAROZ3="00" hardcoded** во auto-prefill (не се derive-ира 10-цифровна од 8-цифровна партнерска)

### Files
18. **File-based XML**: `<project>\XML\<name>.xml`, отвора Notepad
19. **File-based FTP**: `ftp.exe` + batch script, plaintext
20. **Per-customer folders**: `C:\PREV\GENT\GENTHERM\DDMMYYYY\`

---

## 🗂️ 10 главни процеси (од cards metadata)

| # | Process | Cards | What |
|---|---------|-------|------|
| 1 | Declaration Lifecycle | 1168 | Фактури, Normativi, Производство, Izvoz |
| 2 | Customs Warehouse | 350 | Lager, складирање |
| 3 | Utilities / Maintenance | 298 | Админ, прегледи |
| 4 | Guarantees | 290 | Garancija, Razdolzuvanje |
| 5 | Production | 281 | Distribution, Proizvoditeli |
| 6 | Master Data / Setup | 231 | tblArtikli, Firmi |
| 7 | WMS / Warehouse Operations | 200 | Ispratnici, Izdatnici |
| 8 | Reporting & Printing | 124 | Reports |
| 9 | Tariffing & Duties | 98 | KnigaNai, CarTar |
| 10 | Customs Communication | 13 | XML, PEE |

Tags (subprocesses): odobrenie (812), proizvodstvo (567), faktura (484), ispratnica (457), zaklucok (400), magacin (373), transfer (355), normativ (316), otpad (276), artikli (218), tarifa (158), zaverka (117), davacki (110), razdolzuvanje (93), presmetka (51), ecd (45), xml (17), garancija (10), zadolzuvanje (7).

---

## 🎭 Customer matrix (обрасци)

### Special behaviors per Uvoznik:

| Uvoznik | Specifičnost |
|---------|--------------|
| **DREKKV/ODW** | Trosoci → VrednostBruto (не Vrednost); F1 tariff dialog; preserves manual TarBr/Zemja; can edit used articles |
| **TEKSPORT** | Inflate-for-waste on import; deletes Invoice staging |
| **DELPHI** | Auto-apply NormativTemplO on transfer |
| **LEARGV** | Auto-apply template; separate module LearGV |
| **GENTHERM** | Auto-apply template; vehicle metadata fields |
| **JONSON** | Flags Invoice.Preneseno=True on import |
| **HAVEP** | Special ImaNemaVoArtikliMat4 |
| **KNAUF** | Different startup form (frmIzberiZakKNAUF), frmAzurArtikliKnauf |
| **GLOBAL** | Slash-separated codes (ImaNemaVoArtikliDveSifri) |
| **ETERNA** | VratiImaNemaVoArtikliMatBezEdMer (returns matched code) |

---

## 🛠️ Клучни алатки/форми по задача

### Навигација
- **frmGlavnoMeni** — min main menu (just Maximize + switch)
- **frmIzberiZak** — central hub (88 procedures, 55 buttons)
- **frmAzurZak** — closure manager (central for ops)

### Внес податоци
- **frmFakturiU5 + subFakturiU5** — U5 invoice
- **frmNovTransferFakturaU5** — bulk transfer (678 lines!)
- **frmGotoviProizvodi + subGotoviProizvodi** — finished products
- **frmNormativi + frmNormativiVred + frmAzurNormativOtpad** — BOM
- **frmAzurArtikli + frmNovArtikal + frmArtIzmeni** — catalog CRUD

### Поподобри
- **frmPomosZaArtikli** — article picker (mode via `frmMat`)
- **frmArtKatBrStara** — legacy SKU disambiguation
- **frmVnesNaNoviMat / MatECD** — staging-to-catalog bridge
- **FrmPrvaPomos** + 3 variants — edit-by-delta popups for LagerMaterijali

### Царина
- **frmRazdolzuvanjeZak** — main release screen (+ _nov simpler variant)
- **frmPodeliBaranjaBrz** — distribution (creates 6 ledger rows per action)
- **frmRaspredeliPoProizvoditeliBrz** — per-producer distribution
- **frmMaterijaliOtpad**, **frmMaterijaliVrakanje**, **frmGotoviProizvodiIzvoz** — 3 material outcomes
- **frmInspektor** — customs inspector interaction

### Извештаи и печат
- **frmAnalizaZak/GP** — closure analysis
- **rptArtikli**, **rptRazdolzuvanje**, **rptG20-G30Mesecno** — core reports

---

## 📝 Моите препораки за иднината

### Што да се подобри во ELON (priorities):
1. **Parameterized queries** — SQL injection surface огромна
2. **Password hashing** — plain-text е критично
3. **Transactional writes** — DMax+1 без lock → race conditions
4. **Referential integrity** — недостига на FKs
5. **Auto-XML submission** — да не се користи Notepad manual flow
6. **Year-indexed columns refactor** — `Aneksi.ST<year>` → долга табела

### Можности за нашата Python апликација:
1. **Внес на материјали (missing grid)** — replace `frmVnesNaNoviMat + frmArtKatBrStara + frmNovArtikal` trio
2. **Bulk transfer** — replace `frmNovTransferFakturaU5` (678 lines of Uvoznik-specific mapping)
3. **Duty calculation** — implement proper `CalculateCustomsExpenses` based on real KnigaNai lookup
4. **Guarantee balance monitor** — real-time dashboard (currently advisory only)
5. **MozniMinusi** — stock reconciliation
6. **Template auto-apply** — best productivity feature; worth preserving

### За нови клиенти (adding Uvoznik):
1. Create `tblKorisnik<X>` + `tblLog<X>`
2. Add `frmTransfer<X>` if custom Excel format
3. Add `Case "<X>"` to `UvoznikN` (full company details)
4. Register in `Serveri` with SQL host
5. Set `Uvoznik` DB property in client's MDB copy

---

## 📊 Meta-metrics

- 3053 VBA procedures analyzed
- 501 tables documented
- 443 queries
- 272 forms (88 events on top form!)
- 27 modules
- 200 reports
- 26 per-customer transfer forms
- 6 customs message types (PEE010-060)
- ~30 years of development

Анализата беше направена преку 5 паралелни истражувачки агенти + директна инспекција на metadata.
