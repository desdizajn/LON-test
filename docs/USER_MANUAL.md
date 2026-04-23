# LON — Корисничко упатство (end-to-end)

> **Цел:** целосен работен процес од прва декларација за увоз до финална плаќана фактура. Секоја секција објаснува:
> - **Зошто** (business context)
> - **Како** (UI flow, клик по клик)
> - **API** (за power users / integration)
> - **Чести грешки** + решенија
>
> **Средина:** `https://elon.elbosoft.click/` (VPS production-test). Логирај се со `admin / Admin123!` (seeded), или со акаунт од твојот tenant.
>
> **Опфатени модули:** Master Data · WMS · Царина · Производство · Финансии · HR · Машини · Гаранции · Извештаи · PEE XML.
>
> **Верзија:** 2026-04-23, по Phase 15 (Legacy Parity Closure).

---

## 0. Преглед на бизнис процесот

LON управува со царинска постапка **„увоз за облагородување"** (Inward Processing, IM). Во најкратки црти:

```
┌──────────────────────────────────────────────────────────────┐
│                                                              │
│    1. IM декларација  ──►  2. Гаранција дебитирана           │
│    (4200 или 5100)         (bond reserved од лимит)          │
│         ▼                                                     │
│    3. Receipt  ──►  4. MRN + Batch во Inventory              │
│    (физички прием)    (LonProcessState=Imported=1)           │
│         ▼                                                     │
│    5. Podelba (optional) — разделување кон производители     │
│         ▼                                                     │
│    6. Production Order ──► 7. Material Issue                 │
│    (PO + BOM + Routing)     (Imported → InProduction=6)      │
│         ▼                                                     │
│    8. Production Receipt ──► 9. Finished Goods               │
│    (FG + scrap + waste)      (Готов производ во складот)     │
│         ▼                                                     │
│    10. Избери еден или повеќе исходи:                        │
│        a) EX декларација (извоз)       Proces=7              │
│        b) Return декларација (враќање) Proces=8              │
│        c) Waste декларација (отпад)    Proces=9              │
│         ▼                                                     │
│    11. Zaverka (царинска сертификација)                      │
│         ▼                                                     │
│    12. Razdolzuvanje (bond released)                         │
│         ▼                                                     │
│    13. Фактурирање + наплата                                 │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

Проценти што не завршиле во готов производ мора да се оправдаат (како otpad / zaguba / skart) пред да се затвори циклусот.

---

## 1. Пред да почнеш (preconditions)

### 1.1 Еден-пат setup (prvoto влегување во LON)

**Потребни мастер-дата записи за почеток:**

| Што | Каде | Минимум |
|---|---|---|
| Tenant | seeded од admin | Еден активен (нпр. TEKSPORT) |
| User + Role | `/admin/users`, `/admin/roles` | Барем еден корисник за секоја role што ќе ја користиш |
| UoM (мерни единици) | `/master-data/uom` | KG, KOM, M, M², PCS |
| Warehouse | `/master-data/warehouses` | Еден активен магацин |
| Location | `/master-data/locations` | Барем 3: Receiving, Storage, Production (по тип) |
| Partner (Supplier) | `/master-data/partners` | Твоите надворешни набавувачи (давачи на материјал) |
| Partner (Customer) | `/master-data/partners` | Твоите купувачи (на готов производ) |
| Partner (Producer) | `/master-data/partners` + PartnerType=Producer | **НОВО во P15.8** — подизведувачи |
| Partner (Bank) | `/master-data/partners` | За гарантните сметки |
| CustomsProcedure | `/admin/procedures` (или seed) | 4200, 5100, 3151, 6121 (активни) |
| LONAuthorization | `/customs/authorizations` | Барем едно активно Одобрение |
| GuaranteeAccount | `/finance/guarantees` | Барем една сметка во EUR |
| TariffCode | `/master-data/tariff-codes` | Сите tarife што се користат на твои items |

### 1.2 Мој прв чекор во нова сесија

1. Логирање на `https://elon.elbosoft.click/`.
2. Провери го **guarantee traffic light** на Dashboard (🟢 <60% / 🟡 60-80% / 🔴 80-95% / ⚫ >95%) — ако е црвено, нема простор за нов IM до раздолжување.
3. Кликни **Customs → Open items** за да видиш непокриени IM-и (натпис „отворени обврски").
4. Провери ги **MRN deadlines** (`/customs/deadlines`) — истекуваат ли некои одобренија наскоро?

---

## 2. Master Data — подготовка на каталог

### 2.1 Items (Артикли)

**Two типа:**
- **Raw material** — суровини што се увезуваат.
- **Finished good** — готов производ за извоз.
- **Packaging** — амбалажа.
- **Semi-finished** — полупроизвод.

**Специјален тип:**
- **Waste catalog item** (флаг `isWasteCatalog=true`) — НЕ е производствен материјал; тоа е каталошки запис за отпадот што излегува од некој друг материјал. Легаси `ArtOtpadZao`.

#### 2.1.1 Креирај нов артикл (UI)

1. Оди на `/master-data/items` → клик **„Нов"**.
2. Пополни:
   - **Code** — твоја внатрешна шифра (нпр. `1000020003` за материјал, `18248542XL-1` за готов производ)
   - **Name** — полно име
   - **Type** — избери еден од {RawMaterial, SemiFinished, FinishedGood, Packaging}
   - **UoM** — основна мерна единица
   - **HS Code** — 10-цифрена тарифна ознака
   - **Country of Origin** — ISO-2 (DE, IT, TR...)
   - **Partner SKU (ArtKatBrStara)** — ако овој артикл има шифра кај партнерот (TEKSPORT/JONSON/HAVEP), внеси ја овде. Тоа е legacy crosswalk — кога увозната фактура реферира шифра на партнерот, importer-от ќе го мапира на твојот интерен Code. (**P15.1 нова функционалност**)
   - **Batch Required** / **MRN Required** — за увозни LON материјали обично `true/true`.
3. **Шкарт конфигурација** (collapsible секција, **P15.6 нова функционалност**):
   - **Primary waste (Otpad)** — каталошки item + процент. Пример: кожа за патики → "кожен отпад" 5%.
   - **Secondary waste (Otpad1)** — втор отпаден клас со различна тарифа.
   - **Tertiary waste (Otpad2)** — трет.
   - **Zaguba (non-recoverable loss)** — прашина, испарување; не излегува како физички отпад но мора да се декларира.
   - **Waste tariff code** — тарифата на овој артикл КОГА тој е waste (само ако `IsWasteCatalog=true`).
   - **Is waste catalog entry** — checkbox. Активирај ако овој артикл е наменет како target на waste слот.

Collapsed е по default — отвори само ако конфигурираш материјал со отпад.

4. Клик **„Зачувај"**.

#### 2.1.2 API

- `POST /api/MasterData/items` — креирање.
- `GET /api/MasterData/items?search=<text>` — пребарување по Code, Name, PartnerSKU.
- `GET /api/MasterData/items?wasteCatalogOnly=true` — филтрирај само отпадни каталози (за пикери).
- `PUT /api/MasterData/items/{id}` — измена.
- `DELETE /api/MasterData/items/{id}` — soft delete.

**Пример**: артикл `PANT-1000020` со 5% primary waste „SCRAP-PANT" + 2% zaguba.

```json
{
  "code": "PANT-1000020",
  "name": "Pantaloni material standard",
  "itemType": 1,
  "uoMId": "<uoM kg>",
  "hsCode": "6203421000",
  "countryOfOrigin": "TR",
  "partnerSKU": "TEKS-PANT-1020",
  "isBatchRequired": true,
  "isMRNRequired": true,
  "isActive": true,
  "primaryWasteItemId": "<scrap-pant item id>",
  "primaryWastePercentage": 5.0,
  "zagubaPercentage": 2.0
}
```

#### 2.1.3 Bulk import (importer)

За масовен внес (стотици/илјадници артикли):
1. `/import/sessions` → клик **„Нов"**.
2. Избери target `Items`.
3. Upload .xlsx.
4. Mapping (source column → target field). Клучни:
   - `code` (required)
   - `name` (required)
   - `type` (RawMaterial/SemiFinished/FinishedGood/Packaging)
   - `baseUoMCode` (lookup по Code)
   - `partnerSku` — legacy ArtKatBrStara (**P15.1**)
   - `hsCode`, `countryOfOrigin`, `isBatchTracked`, `isMRNTracked`
   - `standardCost`
5. Dry-run за validation.
6. Commit. Deduplicate по `(TenantId, Code)` — upsert семантика.

### 2.2 Partners (Партнери)

**Типови:**
- **Supplier** (1) — од кого увезуваме.
- **Customer** (2) — кому извезуваме готови производи.
- **Carrier** (3) — транспортер.
- **CustomsBroker** (4) — царински агент.
- **Bank** (5) — за гарантни сметки.
- **Producer** (6) — **P15.8 нова** — подизведувач кому праќаме материјал на обработка.

UI: `/master-data/partners` → **„Нов"** → избери Type.

⚠️ Producer партнери **мора** да имаат `PartnerType=Producer` за Podelba да ги прифати.

### 2.3 Warehouses + Locations

- **Warehouse** = физички магацин со адреса.
- **Location** = поло/полица/bin внатре во магацин, со тип:
  - `Receiving` — за прием
  - `Storage` — за складирање
  - `Picking` — за pick-list
  - `Production` — на производствениот под
  - `Shipping` — за отпрема
  - `Quarantine` / `Blocked` — за quality hold + skart

### 2.4 Tariff codes (царински тарифи)

`/master-data/tariff-codes` ги содржи 10-цифрените TARIC шифри. Дополнително:
- `TariffCodeRate` (year-indexed) — царинска стапка + ДДВ по година + земја. Legacy `Aneksi.ST<year>`. Користи се во **duty lookup warning rule** (валидација при декларација).

### 2.5 BOM (Bill of Materials) — нормативи

**Legacy терминологија: Normativi.**

1. `/master-data/boms` → **„Нов"**.
2. Избери FG item + верзија + base quantity (обично 1 unit FG).
3. Додади линии:
   - Component item (материјал)
   - Quantity per 1 unit FG
   - UoM
   - Scrap percentage (единечен scrap % — компатибилно со legacy)
   - **P15.6b**: и овде можеш да ги override-неш 4-те waste slots од Item default (ако овој BOM користи material со различни % отпад).
4. Optional: **PartnerId** — направи го BOM-от партнер-специфичен (за TEKSPORT + JONSON користат ист материјал со различни scrap norms).
5. Зачувај.

**Auto-apply (legacy NormativTemplO/S)**: кога создаваш нов PO, LON автоматски ќе ја избере најновата активна BOM (prefer partner-scoped); нема потреба од рачно копирање. **P5.3.1/P5.3.2** го решава ова.

### 2.6 Routings (технолошки процес)

`/master-data/routings` — секвенца операции (Cutting → Sewing → Packaging) со:
- Work center
- Standard time + setup time
- Machine (optional)

### 2.7 LONAuthorization (Одобрение)

**Легаси Odobrenie.** Царинската управа издава „одобрение за увоз за облагородување" за период (обично 1-3 години).

1. `/customs/authorizations` → **„Нов"**.
2. Пополни:
   - **Authorization Number** — број од царинскиот документ
   - **Partner** (имател на одобрението — обично тенантот)
   - **Issue Date + Expiry Date**
   - **System Type**: ОдложеноПлаќање (4200) или ВраќањеДавачки (5100)
   - **Operation Type**: Inward Processing / Temporary Import / etc.
   - **Guarantee Amount** — лимит на bond за ова одобрение
   - **Guarantee Percentage Override** (optional) — legacy B5 позиција
   - **Competent Customs Office** — царинарница (MK003, MK007, MK010...)
   - **Completion Period Days** — колку имаш од IM до да исчистиш (бондот)
3. Додади **Authorization Items** (кои стоки се дозволени):
   - Import tariff code (суровина)
   - Compensating tariff code (готов производ)
   - Yield rate (колку готов производ од 1 unit суровина)
   - **Allowed waste percentage** — дозволен otpad за овој материјал по ова одобрение

### 2.8 GuaranteeAccount (Гарантна сметка)

`/finance/guarantees` → **„Нов"** → пополни Bank partner, Account number, Currency, Total limit.

Обично една EUR сметка + една USD сметка по тенант.

---

## 3. Увоз (IM declaration)

### 3.1 Business flow

Кога пристигнува нова пратка од добавувач:

1. Создавам **IM царинска декларација** (со procedure 4200 или 5100).
2. LON го дебитира **guarantee bond** автоматски (`Debit = (TotalDuty + TotalVAT) × GuaranteePercentage / 100`).
3. **MRN** се регистрира (auto-placeholder или пасти рован од porta).
4. Создавам **Receipt** од декларацијата (*bulk-from-declaration* — P5.2.3).
5. Инвентарот е тагнат со `LonProcessState=Imported` + `MRN` + `BatchNumber`.

### 3.2 UI flow за нова IM декларација

1. `/customs/declarations` → **„Нова"** → тип **Import (IM)**.
2. Header:
   - **Declaration Number** — твој интерен број (IMP-001, итн.)
   - **MRN** — ако го знаеш, внеси го (формат 18 char, `26MKIM10150003D7B3`); ако не, LON ќе направи dev placeholder.
   - **Declaration Date** — датум на декларација
   - **Customs Procedure** — 4200 или 5100 (бара LON авторизација!)
   - **Partner** — supplier
   - **LONAuthorization** — избери активно (задолжително за 4200/5100)
   - **Currency** — EUR default
   - **Total Customs Value** — вкупно фактурна вредност
   - **LandingCosts / Discount** (optional) — trošoci/рабат; spread-уваат се pro-rata по линии автоматски (**I2 legacy DodadiTrosociPoFakturaU5 parity**)
   - **SAD Boxes**: Sender / Receiver / Country of Dispatch / Destination / SpecialRemarks.
3. Lines — додади една линија по артикл:
   - **Item** — пикирај од каталог
   - **Tariff Code** — 10-digits (auto-populate од Item.HSCode)
   - **Quantity** + **UoM**
   - **Customs Value** (линиска)
   - **DutyRate** + **VATRate** — манусално (backend лookup warning ако не совпаѓа со KnigaNai)
   - **Country of Origin**
   - **Gross Weight** / **Net Weight** (Box 35 / 38 — задолжителни)
4. Submit. Систем:
   - Пресметува DutyAmount + VATAmount по линија
   - Пресметува guarantee debit + проверува лимити (account + per-auth)
   - Создава **MRNRegistry** запис
   - Враќа declaration ID

**Status**: Draft → Registered (ако има MRN) → Submitted (кога PEE010 испраќаш) → **Cleared** (кога Zaverka се става).

### 3.3 Bulk receipt од декларација

Откако декларацијата е Registered, физички се прима стоката:

1. `/warehouse/bulk-receipt` → избери ја декларацијата.
2. Избери warehouse + target location (optional).
3. Confirm.
4. Систем автоматски создава **Receipt** со **ReceiptLine** по секоја declaration line. Количината се **inflate-ира** за TEKSPORT (`qty × 100/(100-wastePercent)`) ако tenant-от е означен.
5. Inventory се создава: `LonProcessState=Imported`, `QualityStatus=OK`, MRN + Batch stamped.

### 3.4 API

```bash
# Креирај IM
POST /api/customs/declarations
{
  "declarationNumber":"IMP-001",
  "declarationDate":"2026-04-23",
  "customsProcedureId":"<4200>",
  "partnerId":"<supplier>",
  "lonAuthorizationId":"<auth>",
  "totalCustomsValue":10000,
  "currency":"EUR",
  "landingCosts":200,
  "discount":50,
  "lines":[
    {"itemId":"<pant-1000020>","tariffCode":"6203421000","quantity":1000,
     "uoMId":"<kg>","customsValue":9750,"countryOfOrigin":"TR",
     "dutyRate":10,"vatRate":18,"grossWeight":1050,"netWeight":1000}
  ]
}

# Bulk receipt
POST /api/WMS/receipts/bulk-from-declaration
{"customsDeclarationId":"<decl-id>","warehouseId":"<wh>","targetLocationId":"<loc>"}

# NaimU5 rollup (legacy PresmetajDavackiPoNaim)
GET /api/customs/declarations/{id}/naim
```

### 3.5 Чести грешки

| Порака | Причина | Решение |
|---|---|---|
| „LON authorization is required for procedure 4200" | 4200/5100 бара LONAuthorizationId | Избери активно одобрение, или promeni procedure |
| „MRN 'XY' is already registered" | Глобална униkeness нарушена | Провери кај друг tenant или во archive |
| „Guarantee account does not have enough available limit" | Balance над лимит | Плати down некои отворени IM-и, или зголеми лимит на bank |
| „Net weight > Gross weight" | I5 weight sanity rule | Провери внесените тежини по линија |
| „Currency policy requires declaration currency = bond currency" | Guarantee currency policy (P15 memory) | Или користи EUR bond за EUR IM, или направи USD bond |
| „LON authorization ... expired on X" | B4 completion days истечен | Продолжи authorization или создај ново |

---

## 4. Podelba — распределба кон производители (**P15.8 — NEW**)

### 4.1 Зошто

Ако праќаш материјал на повеќе подизведувачи (TEKSPORT има 3+ subcontractors), потребно е логички да ги одвоиш количините. Legacy ELON `LagerMaterijali.Proizvoditel`.

### 4.2 Business flow

1. Имаш материјал во `OK` баланс на рецепискиот dock.
2. Одлучуваш: 60 kg → Producer A, 40 kg → Producer B.
3. Кликнеш Podelba → LON го сплита балансот во per-producer siblings.
4. Сите siblings остануваат на **иста локација** (физички материјал не се движи); само logical tag на `AssignedProducerId`.
5. Понатамошните pick tasks / material issues филтрираат по producer.

### 4.3 API

```bash
# Podelba
POST /api/WMS/podelba
{
  "sourceBalanceId":"<balance-at-receiving-dock>",
  "allocations":[
    {"producerId":"<partner-A-uuid>","quantity":60},
    {"producerId":"<partner-B-uuid>","quantity":40}
  ]
}

# Проверувај што е на кој producer
GET /api/WMS/inventory-by-producer?producerId=<partner-A>
```

**Правила:**
- Σ allocations мора **точно** да е еднаков на source quantity (нема partial). Ова спречува „загубена" количина.
- Сите producers мора да се Partner со `PartnerType=Producer`.
- Не можат да се повтори исти producer во ист Podelba (combine lines instead).
- Idempotent: ако уште еднаш кажеш исти allocation на истиот producer, siblings се merge-уваат (не duplicate-ираат).

### 4.4 Чести грешки

| Порака | Решение |
|---|---|
| „Partner X is not of type Producer" | Измени го партнерот на Type=Producer |
| „Allocation total must equal source exactly" | Провери ги количините — сумата мора да биде еднаква на source |
| „Source balance has zero quantity" | Баланс веќе потрошен, нема што да се распределува |

---

## 5. Производство

### 5.1 Create Production Order

`/production/orders` → **„Нов"**:
- **FG Item** — готов производ
- **Order Quantity** — колку парчиња
- **Customer Partner** (optional) — за кого
- **Main Order Number + Sub Order Number** — за PA/варијанта (KW12 legacy)
- **BOM** — auto-selected (latest active, prefer partner-scoped); може и manual override
- **Routing** — auto-selected слично

PO се создава во **Draft** статус.

### 5.2 Release

1. Клик **„Release"** на PO detail.
2. Систем:
   - Scale-ира BOM квантитети по `OrderQuantity / BOM.BaseQuantity`
   - Создава `ProductionOrderMaterial` по BOM line
   - **P15.6c**: snapshot-ира waste slots (BOMLine override > Item default > null)
   - Ја reserves required qty
   - Променува статус: Draft → Released

### 5.3 Material Issue (Izdatnica)

Издавање материјал на производство:

1. `/production/orders/{id}` → tab **„Materials"**.
2. За секој material клик **„Issue"**.
3. Ако PO-material има **PreAssignedMRN/Batch** (legacy G3), LON го користи exactly; инаку FEFO auto-pick со LON-first ordering.
4. **Inventory transitions**: `Imported → InProduction` (= legacy Proces 1 → 6). Split balance — issued portion со LonProcessState=InProduction на истиот рак.

### 5.4 Production Receipt

Кога готовиот производ излегува од машина:

1. `/production/orders/{id}` → tab **„Receipts"**.
2. Пополни:
   - Batch number (нов, stamped на FG)
   - Quantity produced
   - Scrap quantity (aggregate; per-slot decomposition е future work)
   - Location (каде се пакува)
3. Submit. LON:
   - Создава FG InventoryBalance
   - Ја roll-ира `PO.ProducedQuantity + ScrapQuantity`
   - Создава **TraceLink** запис (FG → consumed materials)
   - Ако `Produced + Scrap ≥ OrderQuantity` → PO status → **Completed**, `ActualEndDate` = now

### 5.5 Skart (дефект на прием) — **P15.3 NEW**

Ако дел од примениот материјал е оштетен **на прием** (НЕ е производствен scrap):

1. `/warehouse/skart` — register на сите шкарт пријави.
2. Кликни **„Пријави"** на receipt line (in future UI); тренутно — POST API:
   ```bash
   POST /api/WMS/skart
   {"receiptLineId":"<...>","skartQuantity":5,"reason":"Torn fabric on 3 rolls"}
   ```
3. Систем: Decrements OK balance, creates/increments Blocked sibling at SAME location.
4. Register страница: филтер open/all, CSV export, inline Resolve button.
5. **Resolve**: кога добавувачот договори што со материјалот:
   - `ReturnedToSupplier` — дебит-нота + враќање
   - `Destroyed` — уништено на лице место
   - `AcceptedAtDiscount` — прифатено со попуст
   
**Правила:**
- Cumulative Σ skart ≤ ReceiptLine.Quantity (не може да шкартираш повеќе од примено; legacy NetoKol).
- Одобрен skart (resolved) не може пак да се отвори.

### 5.6 Waste declaration (Otpad)

На крајот од PO или по batch:

1. `/customs/declarations` → **„Нова"** → тип **Waste**.
2. Избери MRN извор (кој IM материјал е потрошен за овој otpad).
3. Количина отпад + causa.
4. LON transits: `InventoryBalance.LonProcessState → Waste=9`. Ова **го ослободува** bond-от за оваа qty (legacy razdolzuvanje).

---

## 6. Извоз (EX declaration)

### 6.1 Business flow

Готовиот производ е спремен за испорака кон клиент (TEKSPORT кон TexportGmbH, итн.):

1. Create **Shipment** со клиент + carrier.
2. Create **EX declaration** што referencira source MRN(s).
3. LON:
   - Decrements `MRNRegistry.DischargedQuantity`
   - Transitions FG inventory: `InProduction → Exported=7`
   - Proportional **guarantee credit** (release): `credit = (dischargedQty / totalImportedQty) × originalBond`
4. Zaverka од царина (customs inspector) → Status=Cleared.

### 6.2 Create Shipment

`/finished/ready-to-ship` → **„Create shipment"** → пополни customer, carrier, tracking#.
- **ShipmentRegime** (**P15.9 NEW**) — `EXA3` за extern извоз, `VS7` за return, `DOM` за домашен.
- **IsReturn** checkbox за враќање.

### 6.3 Create EX declaration

```bash
POST /api/customs/declarations/export
{
  "declarationNumber":"EXP-001",
  "declarationDate":"2026-04-25",
  "mrn":"26MKEX...",
  "previousMRN":"<source IM MRN>",
  "partnerId":"<customer>",
  "lonAuthorizationId":"<auth>",
  "lines":[{"itemId":"<fg>","tariffCode":"...","quantity":30,"customsValue":1500,...}]
}
```

### 6.4 Ispratnica документ (**P15.9**)

Metadata од P15.9 (ShipmentRegime, IsReturn, ZaverkaNumber, ZaverkaDate) ги носи outbound document. HTML/PDF генерација е future work (`P15.9.1`) — засега може да се print преку browser.

---

## 7. Return declaration (Vrakanje)

Ако извезениот материјал/готов производ се враќа:

```bash
POST /api/customs/declarations/return
{"previousMRN":"<source EX MRN>","returnQuantity":4,...}
```

LON:
- Reverses EX → Imported/InProduction (caller choice)
- FG re-intake
- Decrements `MRN.DischargedQuantity`
- **Re-debit** guarantee (proportional, симетрично со EX credit)
- Re-activates MRN ако било fully-used

---

## 8. Zaverka (царинска сертификација)

Кога инспекторот ќе ја завери декларацијата:

1. `/customs/declarations/{id}` → клик **„Заверка"**.
2. Внеси:
   - ZaverkaNumber (број на заверка)
   - ZaverkaDate
3. Систем:
   - Flips Status → Cleared
   - Stamps ZaverkaNumber/Date + ClearedDate
   - Emits `CustomsDeclarationCertifiedEvent`
   - Dedupe guard (тенант-scope uniqueness)

⚠️ Draft/Registered/Submitted → Cleared сите се дозволени (legacy skipping поддржан); Cancelled → Cleared е одбиен.

---

## 9. Гаранција (Garancija) + Razdolzuvanje

### 9.1 Преглед

`/finance/guarantees`:
- **Traffic light** (**P15.2 / P4.4**): 🟢 <60% · 🟡 60-80% · 🔴 80-95% · ⚫ критично >95%.
- Per-account: total limit, current balance, available, utilisation %.
- Ledger entries (recent 100): Debit / Credit со MRN + declaration link.

### 9.2 Monthly snapshots (**P15.5 NEW**)

Секој месец (или on-demand):

```bash
POST /api/Guarantee/snapshots/run
{"snapshotDate":"2026-04-30","notes":"April month-end"}
```

Snapshot пресметува outstanding debits (`EntryType=Debit AND (!IsReleased OR ActualReleaseDate >= cutoff)`) vs credits, зачувува `NetBalance + AvailableLimit + ActiveDebitCount`. Idempotent — re-run истиот датум ја замени претходна snapshot.

```bash
GET /api/Guarantee/snapshots?accountId=<x>&from=2026-01-01&to=2026-04-30
```

### 9.3 Razdolzuvanje report (**P15.11 NEW**)

```bash
GET /api/customs/reports/razdolzuvanje?authorizationId=<auth>&from=2026-01-01&to=2026-04-30
```

Враќа:
- Per-IM MRN: debit / credit / net outstanding
- FullyDischarged boolean
- Last credit date
- Totals на врв

---

## 10. PEE XML комуникации со царина

LON генерира 5 PEE XML envelopes (legacy Access ги отвораше во Notepad). Тек корисник ги презема и манusлно upload-ira на царинскиот портал.

### 10.1 Поединечни envelopes

| Envelope | За што | Endpoint |
|---|---|---|
| **PEE010** | IM submission | `GET /api/customs/declarations/{id}/pee/PEE010` |
| **PEE020** | IM clearance response (inbound stub) | `GET /api/customs/declarations/{id}/pee/PEE020` |
| **PEE040** | Waste declaration | `GET /api/customs/declarations/{id}/pee/PEE040` |
| **PEE050** | EX submission | `GET /api/customs/declarations/{id}/pee/PEE050` |
| **PEE060** | Monthly razdolzuvanje report | `GET /api/customs/pee/060?authorizationId&from&to` (P4.2) |

### 10.2 Envelope × type validation

- PEE010 бара DeclarationType=IM.
- PEE050 бара DeclarationType=EX.
- PEE040 бара DeclarationType=Waste.

Мismatch → 400 со јасна порака.

### 10.3 Envelope содржина

- Metadata: `InterchangeControlReference=9999`, `SenderCodeQualifier=C5`, `RecipientPassword=111111` (legacy hardcoded).
- Declaration metadata: MRN, број, тип, procedure, date, totals.
- Naimenovanija: grouped by (TariffCode, UoM, Country) — 1 naim row = 1 `<Naim>` element со summed qty + weighted rate (legacy `NaimU5` rollup, **P15.4**).

### 10.4 Пример (PEE010)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<PEE010>
  <Envelope>
    <InterchangeControlReference>9999</InterchangeControlReference>
    <Sender>Texport GmbH</Sender>
    <SenderCodeQualifier>C5</SenderCodeQualifier>
    <Recipient>MK007</Recipient>
    <RecipientPassword>111111</RecipientPassword>
    <GeneratedAt>2026-04-23T06:05:19Z</GeneratedAt>
  </Envelope>
  <PEE010_Body>
    <MRN>26MKIM10150003D7B3</MRN>
    <DeclarationNumber>IMP-D7B3</DeclarationNumber>
    <DeclarationType>IM</DeclarationType>
    <ProcedureCode>4200</ProcedureCode>
    ...
    <Naimenovanija>
      <Naim num="1">
        <TariffCode>3921190000</TariffCode>
        <UoM>M</UoM>
        <CountryOfOrigin>DE</CountryOfOrigin>
        <Quantity>6.0000</Quantity>
        <CustomsValue>26.53</CustomsValue>
        <DutyRate>0.00</DutyRate>
        <DutyAmount>0.00</DutyAmount>
        <VATRate>18.00</VATRate>
        <VATAmount>4.77</VATAmount>
      </Naim>
      ...
    </Naimenovanija>
  </PEE010_Body>
</PEE010>
```

---

## 11. Извештаи (Reports)

### 11.1 Customs reports (**P15.11 NEW**)

- `GET /api/customs/reports/razdolzuvanje?authorizationId&from&to` — per-IM release status.
- `GET /api/customs/reports/monthly-register?year=2026` — grouped by (month, procedure) со counts + totals.
- `GET /api/customs/reports/waste-register?from&to` — сите waste declarations.

### 11.2 WMS reports

- `/warehouse/variance` — cycle count разлики.
- `/warehouse/incoming` — очекувани пратки (MRN без Receipt).
- `/warehouse/stock-by-customer` — inventory групирано по customer.
- `/wms/mozni-minusi` (P4.3) — negative stock reconciliation.

### 11.3 Production reports

- `/production/today` — денешен план.
- `/production/wip` — work-in-progress.
- `/production/completed` — завршени POs.
- `/production/at-risk` — ризик за заостанок.
- `/production/shortage` — недостаток материјал vs required.

### 11.4 Finance reports (`/finance/reports`)

- **Cost accounting** — WorkCenter × Shift cost-per-minute.
- **Margin** — revenue / paid / outstanding по клиент.
- **AP** — supplier invoices + aging.
- **P&L** — monthly rollup.
- **Cash flow** — outstanding invoices by age bucket.

### 11.5 Management reports (`/management/*`)

- `/management/dashboard` — executive КPIs.
- `/management/monthly-pack` — printable executive snapshot.
- `/management/client-scorecard` — on-time + paid ratio per customer.
- `/management/trends` — 3-12 месец time series.

---

## 12. Финансии (нов LON модул — не постои во ELON)

### 12.1 Client contracts (договори)

`/finance/contracts`:
- Partner (customer)
- Valid from / to
- Payment terms days
- Currency
- **Rate card entries**:
  - Type: `PerPiece` или `PerMinute`
  - Item or Operation code
  - Rate per unit
  - Valid from / to

### 12.2 Invoices (фактури)

1. **Generate from PO**: `POST /api/Finance/invoices/{id}/generate-from-po` — collects completed production receipts + applies contract rates → линии автоматски.
2. Manual добавка: `POST /api/Finance/invoices/{id}/lines`.
3. **Lifecycle**: Draft → Issued → Paid (или Cancelled).
   - Draft = edit freely.
   - Issued = number committed, notification sent.
   - Paid = plaќена.

### 12.3 Status flow

```
Draft → (add lines / from PO) → Draft
Draft → (POST /issue) → Issued
Issued → (POST /mark-paid) → Paid
Any → (POST /cancel) → Cancelled (lines preserved for audit)
```

---

## 13. HR

### 13.1 Employees

`/hr/employees`: CRUD + shift assignment + role link.

### 13.2 Attendance

`/hr/attendance-today` — clock in/out, overtime tracking.

### 13.3 Overtime + performance + training

`/hr/overtime`, `/hr/performance`, `/hr/training` — registers.

### 13.4 Operator assignments

`/hr/assignment` — кој вработен на која машина за денес.

---

## 14. Машини

### 14.1 Status + maintenance

- `/machines/status` — current state per машина.
- `/machines/downtime` — downtime events.
- `/machines/maintenance-plan` — планирано одржување.
- `/machines/maintenance-history` — историјат.

### 14.2 KPIs

- `/machines/oee` — Availability × Performance × Quality (P11.3 proxy).
- `/machines/capacity` — утилизација.
- `/machines/setup-time` — changeover Pareto.
- `/machines/bottleneck` — top 3 по downtime минути.

---

## 15. Типични проблеми + решенија

### 15.1 „Нема доволно MRN количина"

- Провери `MRNRegistry.RemainingQuantity`.
- Можеби некоја претходна pratka преку-користила.
- Ако legacy issue, провери inflate-for-waste flag на tenant.

### 15.2 „Нема баланс за issue"

- Проверу го inventory balance филтер (QualityStatus=OK? LonProcessState=Imported?).
- Ако е Blocked, провери skart records. Можеби се потребни resolve пред да се продолжи.

### 15.3 „PO не се завршува"

- Required ≤ Issued? Produced + Scrap ≥ Order qty?
- Ако Status остане InProgress после последен receipt — провери threshold во `CreateProductionReceiptCommand`.

### 15.4 „EX не поминува"

- LONAuthorization активна?
- MRN.UsedQuantity (по IM и issues) колку е?
- DischargedQuantity + sent qty ≤ UsedQuantity?
- Имаш ли Exported inventory за да го extract-иш?

### 15.5 „Guarantee balance е погрешен"

- Ledger-based; пресметува `Σ Debit − Σ Credit` на секој query.
- Ако не се совпаѓа со очекуваното — преброј `GET /api/Guarantee/ledger?accountId=<x>`.
- Pending credits (not yet zaverka-ed) во LON се **immediate** (P2.6a); legacy ги чекаше до zaverka. Видете P15.10.1 за refactor.

---

## Анекс A: Мапирање legacy ELON процес → LON endpoint

| Legacy | LON |
|---|---|
| `frmFakturiU5 + subFakturiU5` | `POST /api/customs/declarations` |
| `frmNovTransferFakturaU5` | `POST /api/import/presets/kw12` |
| `frmGotoviProizvodi` | `POST /api/Production/orders` |
| `frmNormativi` | `POST /api/MasterData/boms` |
| `frmAzurArtikli + frmNovArtikal` | `POST /api/MasterData/items` |
| `frmArtKatBrStara` | `ItemRequest.partnerSKU` field (**P15.1**) |
| `frmPodeliBaranjaBrz` | `POST /api/WMS/podelba` (**P15.8**) |
| `frmRaspredeliPoProizvoditeliBrz` | `POST /api/WMS/podelba` (same) |
| `frmMaterijaliOtpad` | `POST /api/customs/declarations/waste` (P2.6c) |
| `frmMaterijaliVrakanje` | `POST /api/customs/declarations/return` (P2.6b) |
| `frmGotoviProizvodiIzvoz` | `POST /api/customs/declarations/export` (P2.6a) |
| `frmAzurSkart` | `POST /api/WMS/skart` (**P15.3**) |
| `frmRazdolzuvanjeZak` | `GET /api/customs/reports/razdolzuvanje` (**P15.11**) |
| `rptArtikli` | `/master-data/items` + CSV export |
| `rptRazdolzuvanje` | `GET /api/customs/reports/razdolzuvanje` (**P15.11**) |
| `rptG20-G30Mesecno` | `GET /api/customs/reports/monthly-register` (**P15.11**) |
| `rptOtpad` | `GET /api/customs/reports/waste-register` (**P15.11**) |
| `cmdXML_PEE060_Click` | `GET /api/customs/pee/060` (P4.2) |
| `cmdXML_PEE010` | `GET /api/customs/declarations/{id}/pee/PEE010` (**P15.12**) |
| `cmdXML_PEE050` | `GET /api/customs/declarations/{id}/pee/PEE050` (**P15.15**) |
| `frmInspektor / cmdZaverka` | `POST /api/customs/declarations/{id}/certify` (P4.1) |
| `frmAzurGarancija` | `/finance/guarantees` + `POST /api/Guarantee/snapshots/run` (**P15.5**) |
| `tblSostojbaNaGarancija` | `GET /api/Guarantee/snapshots?accountId&from&to` (**P15.5**) |
| `cmdMozniMinusi` | `GET /api/WMS/inventory/mozni-minusi` (P4.3) |

---

## Анекс B: Navigation map (frontend)

**🏭 Магацин (Warehouse):**
- Receipts / Incoming / **QC Hold** / **Skart** (**P15.3**) / Issues today / Transfers / Bulk receipt / Bulk shipment / Stock by customer / Variance / Ready to ship / Search

**🛃 Царина (Customs):**
- Declarations / MRN registry / Authorizations / Open items / Deadlines / Traceability / Import docs / Export docs / Search

**✂️ Производство (Production):**
- Orders / Today / WIP / Completed / At-risk / Shortage / Cutting queue / Sewing queue / Rework / Minutes variance / Search

**📦 Готов производ (Finished goods):**
- Ready to ship / Awaiting pack / Packing lists / Shipped / History by customer / Traceability / Returns / Packaging stock

**👥 HR:**
- Employees / Attendance today / Assignment / Absences / Shifts / Overtime / Performance / Training

**⚙️ Машини:**
- Work centers / Status / Downtime / Maintenance plan / Maintenance history / OEE / Capacity / Setup time / Bottleneck

**💵 Финансии:**
- Contracts / Invoicing / Cost accounting / Margin / AP / Payroll / P&L / Cash flow / Guarantees / Reports

**🎯 Менаџмент:**
- Dashboard / On-time / Alerts / By-customer / Margin / Capacity / Escalations / Risks / Trends / Monthly-pack / Client-scorecard

**🧰 Поставки (admin only):**
- Users / Roles / Tenants / Tenant settings / Audit log

---

*Крај на упатството. За интеграции / API details види `src/LON.API/Controllers/**/*.cs`. За detalini business rules види `ELON_Blueprint.md` + `docs/LEGACY_COVERAGE_ANALYSIS.md` + `../PdfToExcel/ELON_Research/`.*

*Верзија 1.0 — 2026-04-23 (след Phase 15 closure).*
