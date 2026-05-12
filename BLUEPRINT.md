# LON — BLUEPRINT

> **Single source of truth.** Ако нешто конфликтира со `WORK_PLAN.md`, `SESSION_LOG.md`, `ELON_Blueprint.md` (March 2026), или со коментари во код — **BLUEPRINT победува**. Измени на BLUEPRINT се правят преку експлицитна сесија со sign-off од корисникот; не „on the fly" од Claude Code.
>
> *Последна ревизија: 2026-05-11 — иницијална верзија (Phase 16 in-flight).*

---

## §1 — Vision & Scope

### 1.1 Што е LON

LON е multi-tenant SaaS апликација која ги извршува сите чекори на **царинска постапка за активно облагородување (inward processing, „увоз за облагородување", или „LON" од legacy term)** — од прифаќање нарачка од странски клиент, увоз на материјали под банкарска гаранција, производство, до извоз на готови производи и раздолжување на гаранцијата.

LON го заменува **ELON** — 30-годишна Access/VBA апликација (~272 forms, 501 tables, 3053 VBA procedures), користена од македонски конфекциски производител за бизнис со 50+ странски клиенти.

### 1.2 Што LON прави поинаку (и зошто се пишува)

ELON има три недостатоци кои LON решава од прв ден:

1. **Single-user, Access front-end.** LON е web SaaS multi-tenant.
2. **Нема разумни data integrity.** Free-scalar guarantee balance, no foreign keys, no audit, plain-text passwords. LON има enforced FK, RLS tenant isolation, audit trail, JWT auth.
3. **Нема интелигенција.** ELON прави SQL, LON прави препораки. RAG + LLM swept toward the user — види §7.4.

LON **не е** копија на ELON. LON ги извршува истите бизнис операции, но со интелигентен UI кој ја „знае" состојбата на нарачката, предвидува следни чекори и сугерира акции.

### 1.3 v1 Scope — „minimum closed loop"

**v1 = еден целосен затворен цикл, без bug-ови, end-to-end:**

```
ClientOrder (нов налог од клиент X)
   └→ CustomsDeclaration IM (увоз на материјали)
        └→ Receipt (прием во магацин)
             └→ BOM + ProductionOrder (нормативи + готов производ)
                  └→ Podelba (распределба кон еден подизведувач)
                       └→ MaterialIssue (издавање материјал)
                            └→ ProductionReceipt (произведено)
                                 └→ QC + Packaging
                                      └→ CustomsDeclaration EX (извоз)
                                           └→ Razdolzuvanje (раздолжување на гаранција)
```

**Acceptance criterion за v1:** еден производител (Teksport) изврши цел циклус на еден реален клиент со реални податоци, со E2E Playwright тест како proof, и со reconciled balance на гаранцискиот ledger.

**Out of v1** (привремено сокриени со feature flag, кодот останува):
- Втор производител/тенант (multi-tenant capability е изградена, но v1 опслужува **само Teksport** во продукција)
- Втор подизведувач во истиот цикл
- Шпедитер role (login + UI)
- Mobile (Flutter) — за магационер/QC/оператори
- Auto-submit PEE XML до царина (само manual download за v1)
- Целосен ECD intake auto-pull
- 4 јазика во UI — v1 опслужува **MK + EN** само; SQ + SR се out-of-v1
- Сите изветни страници (legacy reports) освен Razdolzuvanje
- AI helper extended (v1 ги има само 3 core recommendations — види §7.4)

### 1.4 Out of LON scope (никогаш)

- Финансиско книговодство (LON испрака CSV за надворешен payroll/accounting систем)
- HR full lifecycle (LON има само attendance/overtime/operator assignment за production tracking)
- E-commerce / клиентски portal (клиентите комуницираат преку email + испратени документи)
- Maintenance management beyond „mark machine down" (CMMS е out)

---

## §2 — Architecture overview

### 2.1 Stack

| Слој | Технологија | Зошто |
|---|---|---|
| Database | SQL Server 2022 | Legacy ELON веќе таму; RLS native; familiar |
| Backend | .NET 8, ASP.NET Core, Clean Architecture (Domain / Application / Infrastructure / API) | Type safety, mature ecosystem, EF Core 8 |
| MediatR | За CQRS commands/queries | Веќе во кодот; clean separation |
| API | REST + OpenAPI (generated TS types via `scripts/gen-api-types.sh`) | Self-documenting, FE контра sourceа од истиот OpenAPI |
| Frontend | React 18 + TypeScript + MUI + react-query + react-hook-form | Industry standard; mature |
| Mobile (post-v1) | Flutter | Cross-platform, веќе започнат во `frontend/mobile/` |
| Deploy | Docker Compose + Contabo VPS + Caddy + Let's Encrypt SSL | Cheap, reliable, fits one-dev ops |
| Auth | JWT (HS256) со tenant_id + role claims | Веќе implementирано |
| RAG | OpenAI embeddings + SQL Server vector store | Веќе implementирано во `KnowledgeBaseController` |
| Testing | xUnit integration + Playwright E2E + Jest unit (React) | Pyramid: many unit, fewer integration, few E2E |

### 2.2 Principles

1. **Done deeper, not wider.** Прв затворен loop end-to-end > 100 страници при 70%. Phase 16 ги чисти лажните; Phase 17 го прави првиот реален loop работен.
2. **Тенант = производител.** Teksport е еден тенант. Производителот може истовремено да биде увозник и/или шпедитер (со соодветни лиценци). RLS изолира.
3. **AI свртен кон корисник.** RAG + LLM = помошник на работниот фолд, не само внатрешен интеграциски слој.
4. **Audit by default.** Сите financial-grade entities имаат IAuditable + soft-delete. Никакво physical DELETE.
5. **Numbering atomic.** SQL SEQUENCE objects (не `DMax+1`). Multi-user safe.
6. **One UI system.** MUI + react-query + DataTable + react-hook-form. Нема hand-rolled tables или inline styles за нови страници (Phase 16.B).
7. **No localStorage as backend.** Бизнис податоци секогаш во DB. localStorage само за UI prefs (`lon.ui.*`).
8. **VPS-or-it-didn't-happen.** Локално работи ≠ готово. Секоја промена deploy на VPS пред да се означи [x].
9. **Migration first.** Без работен ELON→LON migration runbook за полна Teksport data, нема v1 launch.

### 2.3 Repository layout

```
LON-test/
├── BLUEPRINT.md           ← овој фајл — единствен извор на вистина
├── PLAN.md                ← delta + roadmap; референцира BLUEPRINT секции
├── CLAUDE.md              ← оперативен прирачник за Claude
├── AGENT-PROMPTS.md       ← per-task промптови за Claude Code
├── VERIFICATION.md        ← per-task verification checklists
├── SESSION_LOG.md         ← хронолошки append-only
├── WORK_PLAN.md           ← legacy Phase 0–15; deprecated after Phase 16 closes
├── memory/                ← persistent Claude memory (pointers, decisions)
├── docs/
│   ├── ELON_Research/     ← 5 markdown reports на legacy (read-only)
│   ├── ROADMAP.md         ← legacy P7–P13 roadmap; superseded by PLAN.md §3
│   └── USER_MANUAL.md     ← user-facing manual; updated per phase
├── src/
│   ├── LON.Domain         ← entities + enums + events
│   ├── LON.Application    ← MediatR handlers (commands + queries)
│   ├── LON.Infrastructure ← EF Core, configurations, migrations, services
│   ├── LON.API            ← controllers, DTOs, OpenAPI
│   ├── LON.Migration      ← ELON→LON mappers (one-shot import tools)
│   └── LON.Worker         ← background jobs (RAG indexing, snapshots)
├── tests/
│   ├── LON.IntegrationTests          ← xUnit + LonApiFactory
│   └── LON.E2ETests                  ← Playwright (added in Phase 17)
├── frontend/
│   ├── web                ← React/TS (122 pages → consolidated)
│   └── mobile             ← Flutter (post-v1)
├── api-contract/          ← OpenAPI schemas (auto-generated)
└── scripts/               ← gen-api-types.sh, deploy helpers
```

---

## §3 — Domain model

### 3.1 Core aggregate: ClientOrder (Zaklucok analog)

`ClientOrder` е new top-level entity воведен во Phase 17. Тоа е **хабот** на еден циклус бизнис.

```csharp
public class ClientOrder : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; }              // CO-2026-00042 (via SEQUENCE)
    public Guid CustomerPartnerId { get; set; }          // FK → Partner (PartnerType=Customer)
    public Guid LONAuthorizationId { get; set; }         // FK → LONAuthorization (REQUIRED)
    public string? CustomerOrderReference { get; set; }  // нивниот broj
    public DateTime OrderDate { get; set; }
    public DateTime? RequestedShipDate { get; set; }     // когa клиентот сака
    public ClientOrderStatus Status { get; set; }        // Draft / Active / Producing / Shipped / Closed / Cancelled
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    // Backrefs (NavigationLаziLoad disabled; queried explicitly)
    public ICollection<ClientOrderFinishedGood> FinishedGoods { get; set; }
    public ICollection<CustomsDeclaration> Declarations { get; set; }  // IM + EX
    public ICollection<ProductionOrder> ProductionOrders { get; set; }
    public ICollection<Shipment> Shipments { get; set; }
}

public class ClientOrderFinishedGood : BaseEntity, ITenantScoped
{
    public Guid ClientOrderId { get; set; }
    public Guid ItemId { get; set; }                     // FG item from catalog
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public Guid? BOMId { get; set; }                     // assigned BOM (norm template)
    public decimal? UnitPriceForeign { get; set; }       // contract price per piece
    public string Currency { get; set; } = "EUR";
}
```

`ClientOrderStatus` automaticaly transitions based on linked entities (computed, not user-edited):
- **Draft** — нема линкувани декларации/POs/shipments
- **Active** — има барем една IM декларација
- **Producing** — има активни ProductionOrders
- **Shipped** — сите FinishedGoods имаат најмалку една EX shipment
- **Closed** — сите EX shipments имаат `RazdolzenaDaNe=true` И guarantee ledger balance е +0 за оваа нарачка
- **Cancelled** — manually marked; cascades soft-delete

### 3.2 Customs subdomain

| Entity | ELON analog | v1 status |
|---|---|---|
| `LONAuthorization` | Odobrenie | Постои; добра. Додавa `ClientOrders` collection. |
| `LONAuthorizationItem` | OdobrenijaKnigaNai | Постои |
| `CustomsDeclaration` | FakturiU5Z | Постои; додава `ClientOrderId` FK |
| `CustomsDeclarationLine` | FakturiU5 | Постои |
| `CustomsDocument` | разни attachment-и | Постои |
| `MRNRegistry` | tblMRN | Постои |
| `CustomsProcedure` | reference | Постои |
| `TariffCode` + `TariffCodeRate` | KnigaNai + Aneksi | Постои; подобрено |
| `Skart` | FakturiU5Skart | Постои |
| `CommercialInvoice` + `CommercialInvoiceLine` | `tblIzvozniFakturi` + `tblIzvozniFakturiStavki` (3,239 + 57,857 rows) | **NEW v1** (Phase 17 §E8.5) — see below |

Клучна разлика од ELON: **NaimU5 рollup не е separate entity**. Тоа е computed view (SQL view или MediatR query) над `CustomsDeclarationLine` групирано по `(TariffCodeId, UoMId, CountryOfOrigin)`. Никакво insert/delete на header-of-naimenovanija — namesto тоа, наименованијата се производ на агрегација.

#### §3.2.1 — CommercialInvoice (export-value document, distinct from sales Invoice)

`CommercialInvoice` е **царински придружен документ** што го следи физичкиот export shipment — declares commercial value of goods at border crossing. Различен документ од `Invoice` (§5.14.2 = Teksport sales invoice за processing услуги до customer). Two реални documents:

- **`Invoice` (§5.14.2)** — Teksport фактурира customer-от за обработката (sewing labor + overhead, per ClientContract rate card). Revenue side.
- **`CommercialInvoice` (§3.2.1)** — на customs declaration се прикажува trade value of FG that crosses the border. Required by customs authority. Често замислена како "what the customer would invoice their downstream retailer for", но во inward processing на Teksport се generates by Teksport on customer's behalf.

```csharp
public class CommercialInvoice : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable {
    public Guid TenantId { get; set; }
    public string Number { get; set; }                       // SEQUENCE CI-{year}-{seq:D6}
    public Guid? ClientOrderId { get; set; }                 // optional FK
    public Guid? ShipmentId { get; set; }                    // FK; the physical shipment carrying these goods
    public Guid? CustomsDeclarationId { get; set; }          // FK to EX declaration
    public Guid ConsigneePartnerId { get; set; }             // receiver (downstream brand / retailer)
    public Guid ConsignorPartnerId { get; set; }             // sender (usually Teksport's customer brand)
    public DateTime InvoiceDate { get; set; }
    public string Currency { get; set; }                     // 3-char ISO
    public decimal Subtotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string CountryOfDestination { get; set; }         // 2-char ISO
    public string Incoterms { get; set; }                    // FOB / EXW / CIF / DAP / ...
    public string? PaymentTerms { get; set; }                // free text
    public string Status { get; set; }                       // Draft | Issued | Cancelled
    public ICollection<CommercialInvoiceLine> Lines { get; set; }
}

public class CommercialInvoiceLine : BaseEntity, ITenantScoped {
    public Guid CommercialInvoiceId { get; set; }
    public Guid ItemId { get; set; }                         // FG item
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string CountryOfOrigin { get; set; }              // 2-char ISO (usually MK for processed-in-MK FG)
    public Guid? TariffCodeId { get; set; }                  // FK; pulls from FG.DefaultTariffCode
}
```

Workflow:
- Auto-suggest from EX `CustomsDeclaration` lines on hub-action „Креирај commercial invoice" (one click).
- User can edit consignee/consignor (often differs from ClientOrder.CustomerPartner), incoterms, prices.
- Status: Draft → Issued (locks; generates PDF).

Finance integration (post-v1 Phase 27):
- Margin report per ClientOrder will reconcile: commercial export value − cost of production − Teksport's invoice to customer = net trade value through Teksport. Informational, not P&L.
- Cash flow: CommercialInvoice never appears in Teksport's cash flow (it's not Teksport's revenue) — purely informational/regulatory.

UI:
- `/customs/commercial-invoices` list.
- ClientOrder hub → „Commercial invoices" tab.
- Linked from EX CustomsDeclaration detail.

PDF: standardized export-invoice template (consignee, consignor, lines, incoterms, signature block).

### 3.3 Guarantee subdomain (с подобрена интегритета над ELON)

```csharp
public class LONAuthorization { ... 
    public decimal GuaranteeAmount { get; set; }              // ceiling
    public decimal? GuaranteePercentageOverride { get; set; }
}

public class GuaranteeAccount : BaseEntity, ITenantScoped {
    public Guid LONAuthorizationId { get; set; }
    public decimal CurrentBalance { get; set; }               // computed, materialized
    public DateTime LastSnapshotAt { get; set; }
}

public class GuaranteeLedgerEntry : BaseEntity, ITenantScoped, IAuditable {
    public Guid GuaranteeAccountId { get; set; }
    public GuaranteeEntryType Type { get; set; }              // Debit | Credit | Correction
    public decimal Amount { get; set; }
    public Guid? CustomsDeclarationId { get; set; }           // source (IM debit / EX credit)
    public Guid? ProductionReceiptId { get; set; }
    public Guid? ShipmentId { get; set; }
    public string Reason { get; set; }                        // human reason
    public DateTime EventDate { get; set; }
    public Guid CreatedBy { get; set; }
}

public class GuaranteeBalanceSnapshot : BaseEntity, ITenantScoped {
    public Guid GuaranteeAccountId { get; set; }
    public DateTime AsOf { get; set; }
    public decimal Balance { get; set; }
    public decimal Ceiling { get; set; }
    public decimal UtilizationPct { get; set; }
}
```

**Правила:**
- Никаков implicit credit/debit. **Секоја гаранциска промена создава GuaranteeLedgerEntry со reason + actor.**
- IM declaration → debit by `SUM(line.DutyAmount)` где duty calculation е во `DutyCalculation` entity.
- EX shipment → credit by `SUM(consumed_material_duty)` (Davacki of consumed materials, pro-rata).
- **Ceiling enforced** на ниво на handler (не само scalar): create-debit handler refuses ако `CurrentBalance + Amount > Ceiling` — освен ако корисникот има `OverrideGuaranteeCeiling` permission и експлицитно потврди.
- Snapshot задолжителен на крај на месец + на крај на секој ClientOrder (Razdolzuvanje moment).

### 3.4 Inventory subdomain (Proces state machine)

```csharp
public enum LonProcessState
{
    Imported    = 1,  // примено, не дистрибуирано
    Distributed = 1,  // alias for Imported when AssignedProducerId set (легаси Proces=1 покрива и двете)
    InProduction = 6, // кaj подизведувач
    Exported    = 7,
    FinalImport = 8,  // враќање како конечен увоз
    Waste       = 9
}
```

Сите 5 transitions важат за **материјали** (`InventoryBalance` со `Item.Type=Material`):

| Transition | Тригер | Side effects |
|---|---|---|
| (none) → Imported | `ReceiptCommitted` event | Inserts InventoryBalance row со `LonProcessState=Imported` |
| Imported → InProduction | `MaterialIssueCommitted` | Decrement source location, insert MV row, increment producer location со `LonProcessState=InProduction` |
| InProduction → Exported | `ShipmentCommitted` (EX) + pro-rata material consumed | Credit guarantee proportional, decrement producer balance |
| Imported → FinalImport | `ReturnDeclarationCommitted` | Material returned home — credit guarantee partially |
| Imported → Waste | `WasteDeclarationCommitted` | Material declared as scrap — credit guarantee with waste-specific rate |

**Finished goods** (`Item.Type=Finished`) имаат своја паралелна state machine (Planned → Produced → Shipped). Тие живеат во `InventoryBalance` со `LonProcessState=null` (state машината е на FG-level преку `ClientOrderFinishedGood.Status`).

### 3.5 Master data

| Entity | Бележка |
|---|---|
| `Item` | type=Material|Finished|Packaging|Waste; со `ArtBezPref`, `KoefEDM`, `SpecTez`, `ArtFaza` од legacy + `ItemWasteSlots` (4 + Zaguba) |
| `Partner` | type=Customer|Supplier|Producer|Speditor|Carrier|Customs; едн entity, повеќе типови преку flags |
| `UnitOfMeasure` + `ItemUoMConversion` | Со customs UoM mapping |
| `Warehouse` + `Location` | Hierarchy: Warehouse → Zone → Bin |
| `TariffCode` + `TariffCodeRate` | Year-indexed rates (не ALTER TABLE; rows со `Year` column) |
| `CountryOfOrigin` + `Preferential` | Lookup |
| `CodeListItem` | Generic (Currency, IncoTerm, PaymentTerm, etc.) |
| `BOM` + `BOMLine` + `BOMLineWasteOverrides` | Norms |
| `Routing` + `RoutingOperation` | Operations |
| `WorkCenter` + `Machine` + `MachineStateEvent` | Machines |
| `Shift` + `Employee` + `Operator*Assignment` | HR |

### 3.6 Domain events (Phase 17 enforced)

Сите side effects (gaarantee changes, inventory transitions, AI helper triggers) се **domain events** publushed by aggregate roots, handled by event handlers in `LON.Application`. Никаков inline „update GuaranteeAccount inside ProductionReceipt handler".

```csharp
public interface IDomainEvent { DateTime OccurredAt { get; } }
public class ReceiptCommittedEvent : IDomainEvent { ... }
public class CustomsDeclarationApprovedEvent : IDomainEvent { ... }
public class ShipmentCommittedEvent : IDomainEvent { ... }
public class GuaranteeBalanceThresholdEvent : IDomainEvent { ... }  // for AI helper / alerts
public class ClientOrderStatusChangedEvent : IDomainEvent { ... }
```

Event store е простa table `DomainEventLog` (append-only) — за audit и за event replay (тестирање).

### 3.7 Soft-delete & audit

```csharp
public interface ISoftDeletable {
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

public interface IAuditable {
    DateTime CreatedAt { get; set; }
    Guid CreatedBy { get; set; }
    DateTime UpdatedAt { get; set; }
    Guid UpdatedBy { get; set; }
}

public class AuditLogEntry {
    public Guid Id, TenantId;
    public string EntityType;       // "CustomsDeclaration"
    public Guid EntityId;
    public string Action;           // "Update", "SoftDelete"
    public string ChangedFields;    // JSON: [{field, oldValue, newValue}]
    public Guid Actor;
    public DateTime OccurredAt;
    public string? Reason;          // optional user-provided reason
}
```

Entities со `IAuditable + ISoftDeletable` мора (mandatory):
- `ClientOrder`, `CustomsDeclaration`, `CustomsDeclarationLine`, `LONAuthorization`
- `GuaranteeLedgerEntry`, `GuaranteeAccount`
- `ProductionOrder`, `ProductionReceipt`, `MaterialIssue`
- `Shipment`, `ShipmentLine`, `Receipt`, `ReceiptLine`
- `BOM`, `BOMLine`
- `RiskRegisterItem`, `EmployeeCertification` (Phase 16.C entities)

Optional за master data (`Item`, `Partner`) — soft-delete мора, audit pожелно.

EF Core global query filter за `!IsDeleted`. `IgnoreSoftDelete()` extension за admin-restore UI.

### 3.8 Logistics paperwork (DeliveryNote)

**Цел.** Физичкиот придружен документ (cover sheet) што пътува со стоката — нужен за legal/audit/customs purposes, доказ за handover, и legacy continuity (`Propratnici` table во ELON има 1,658 headers + 295,918 lines).

`DeliveryNote` е **polymorphic** — едно entity покрива три legacy flows:

| DocumentType | Кога се генерира | Поврзан со |
|---|---|---|
| `ProducerDispatch` | На Podelba/MaterialIssue Committed | `MaterialIssue.Id` (1:1) |
| `ProducerReturn` | На FinishedGoodReceipt (FG arrives from producer back to HQ) | `Shipment.Id` (1:1, Type=ProducerReturn) |
| `CustomerShipment` | На Shipment (FG → customer) | `Shipment.Id` (1:1, Type=Export) |

```csharp
public enum DeliveryNoteType { ProducerDispatch = 1, ProducerReturn = 2, CustomerShipment = 3 }

public class DeliveryNote : BaseEntity, ITenantScoped, IAuditable, ISoftDeletable {
    public Guid TenantId { get; set; }
    public string Number { get; set; }                        // SEQUENCE DN-{year}-{seq:D6}
    public DeliveryNoteType DocumentType { get; set; }
    public Guid RelatedDocumentId { get; set; }               // polymorphic: MaterialIssue.Id or Shipment.Id
    public DateTime DispatchDate { get; set; }
    public Guid FromLocationId { get; set; }                  // origin warehouse/location
    public Guid? ToLocationId { get; set; }                   // destination (если internal location)
    public Guid? ToPartnerId { get; set; }                    // destination (если external partner — producer / customer)
    public string? DriverName { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? Remarks { get; set; }
    public string Status { get; set; }                        // Draft | Sent | Confirmed | Cancelled
    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public ICollection<DeliveryNoteLine> Lines { get; set; }
}

public class DeliveryNoteLine : BaseEntity, ITenantScoped {
    public Guid DeliveryNoteId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; }
    public decimal Quantity { get; set; }
    public Guid UoMId { get; set; }
    public string? BatchNumber { get; set; }
    public string? Notes { get; set; }
}
```

**Auto-generation.** When the related document commits (`MaterialIssueCommittedEvent` / `ShipmentCommittedEvent` / `FinishedGoodReceiptCommittedEvent`), a `DeliveryNote` is automatically created in `Draft` status, populated from related-doc data. User reviews → adds driver/vehicle → confirms → status flips to `Sent` and PDF generates.

**UI.**
- `/warehouse/delivery-notes` — list page (filter by type, date, partner).
- Auto-prompt on related-doc commit: „Создаден Propratnica DN-2026-000123. Преглед?" toast → opens detail.
- Print/download as PDF (standard legal cover-sheet template; signature block).

**Note vs. CommercialInvoice.** DeliveryNote е internal/logistics; CommercialInvoice е customs-bound (declared value за border). И двата се generated за `CustomerShipment` (Type=Export Shipment) — DeliveryNote придружува стоката internally, CommercialInvoice patut customs declarationот. Различни templates, различни recipients.

---

## §4 — Roles & personas

### 4.1 v1 roles (seeded in `Roles` table)

| Role | Persona | Кој ова е во Teksport-light scenario |
|---|---|---|
| **Administrator** | IT admin | 1 — главно за провизионирање |
| **Manager** | Газда / Operations Manager | 1 — гледа сè, одобрува gradient |
| **Warehouse Operator** | Магационер | 2–4 — прима, дистрибуира, скенира (mobile-friendly UI critical) |
| **Warehouse Manager** | Шеф на магацин | 1 |
| **Production Planner** | Планер на производство | 1–2 — креира POs, нормативи, podelba, scheduling |
| **Production Operator** | Шивачка/оператор | 5–20 — кratko UI, само нивните задачи (mobile post-v1) |
| **Customs Officer** | Царински службеник во фирма | 1 — праве декларации, zaverka, PEE XML |
| **Quality Controller** | QC | 1–2 — pre-shipment проверка, status changes, defect tracking |
| **HR Manager** | HR | 1 — вработени, смени, отсуства, обуки |
| **Finance Clerk** | Сметководство | 1 — контракти, цени, фактурирање, payroll export, маржa, AP, гаранција |
| **Maintenance Tech** | Mеханичар | 1–2 — машини, downtime, maintenance planning |
| **Subcontractor** *(Phase 18)* | Надворешен производител | per-firm — limited view: само нивните налози + материјали + барања |
| **Speditor** *(Phase 19)* | Шпедитерска куќа | per-firm — limited view: само налозите за кои се ангажирани |

### 4.2 Permissions matrix (high-level — детал во §8.3)

Видлив за роли (✓ = full, R = read-only, — = invisible):

| Module / Page | Admin | Mgr | WhOp | WhMgr | ProdPl | ProdOp | Cust | QC | HR | Fin | Maint | SubC | Sped |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Dashboard | ✓ | ✓ | R | ✓ | ✓ | R | R | R | R | R | R | own | own |
| ClientOrders + hub | ✓ | ✓ | R | R | ✓ | own | R | R | — | R | — | own | own |
| Warehouse | ✓ | R | ✓ | ✓ | R | — | — | R | — | — | — | — | — |
| Production (POs, queues, scrap, rework) | ✓ | R | — | — | ✓ | own | — | R | — | — | — | own | — |
| Production scheduling | ✓ | R | — | — | ✓ | R | — | — | — | — | R | — | — |
| Machines + WorkCenters | ✓ | R | — | R | R | own | — | R | — | — | ✓ | — | — |
| Customs | ✓ | R | — | — | — | — | ✓ | — | — | R | — | — | R |
| HR (employees, shifts, attendance, absences, certs, performance) | ✓ | R | — | — | R | own | — | — | ✓ | R | — | — | — |
| Finance (contracts, invoices, AP, margin, cash flow, P&L, FX, payroll) | ✓ | ✓ | — | — | — | — | — | — | — | ✓ | — | — | — |
| Management reporting / alerts | ✓ | ✓ | — | R | R | — | R | R | R | R | — | — | — |
| Master data | ✓ | R | — | R | R | — | R | — | R | R | R | — | — |
| Admin (users, audit, recycle bin) | ✓ | — | — | — | — | — | — | — | — | — | — | — | — |
| AI helper | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Legend: ✓ = full read+write; R = read-only; own = only own scope (e.g. operator sees only their assigned POs); — = invisible.

Endpoint-level enforcement (FluentValidation + custom `[HasPermission(...)]` attribute) — види §8.3. **Никаков relyance on sidebar filtering за security.**

### 4.3 Subcontractor & Speditor login (post-v1 v.s. Phase 18/19)

Кога подизведувач/шпедитер логира:
- JWT claims: `tenant_id` (производителот за кого работат), `role`, `external_partner_id` (нивниот Partner.Id).
- Сите queries дополнително филтрираат `WHERE (Producer.Id = claims.external_partner_id OR Speditor.Id = ...)`.
- UI ги гледа само своите налози. Покажа едно-„single account для multi-tenant" scenario — еден subcontractor може да работи за повеќе производители, со separate JWT per relationship.

---

## §5 — Business flows

> Секој flow секција има: **Намера / Учесници (ролite) / ELON-was (контекст) / LON-spec (што мора v1 да прави) / UI entry points / Business rules / Edge cases / AI helper hooks**.

### §5.1 — ClientOrder приem (нов налог од клиент)

**Намера.** Клиент (Uvoznik) сака X парчиња готов производ Y до дата Z. Производителот ова го регистрира во LON и резервира капацитет.

**Учесници.** Production Planner (главен), Manager (одобрува големи налози), Customs Officer (валидира LONAuthorization).

**ELON-was.** Имплицитно — Zaklucok се „создаваше" кога ќе се отвори frmAzurZak за нова комбинација (Odobrenie, ZaklucokBroj). Нема explicit „order from customer" entity.

**LON-spec.**
- Нов `ClientOrder` со fields од §3.1.
- `OrderNumber` авто-генерирана од SQL SEQUENCE `seq_ClientOrder_<tenantId>` — format: `CO-{year}-{6digit}` (e.g. `CO-2026-000042`).
- **Мора** да биде линкуван со постоен `LONAuthorization` (FK NOT NULL). Ако одобрението не постои, корисникот прво го креира преку `/customs/authorizations` (или Inline „Create authorization" dialog).
- Заедно со ClientOrder, може (но не мора) да се внесат preliminary `ClientOrderFinishedGood` rows — финални FG детали можат да дојдат подоцна.
- Status = Draft на иницијален креат.

**UI entry points (новата „hub" филозофија — §7.1).**
- **Главна точка:** `/orders` (нов route, Phase 17) — листа на сите ClientOrders. Top-right button „Нов налог".
- **Од Dashboard:** quick-create card „+ Нова нарачка" со inline form.
- **Од Customer Partner detail page** (`/master-data/partners/:id`) — button „Нов налог за оваа фирма".
- **Од LONAuthorization detail** — button „Нов налог под ова одобрение".

**ClientOrder detail (hub) page** — `/orders/:id`:
- Header: order number, status badge, customer, authorization, dates.
- **Action launcher** (sticky right-side panel):
  - „Внеси готови производи (BOM)" → §5.4
  - „Креирај увозна декларација (IM)" → §5.2
  - „Прими во магацин" → §5.3
  - „Распредели подизведувач" → §5.6
  - „Издади материјал" → §5.7
  - „Креирај извозна декларација (EX)" → §5.10
  - „Razdolzuvanje" → §5.11
  - „Аудит / историја"
  - „💡 AI препораки" → §7.4
- **Timeline** (vertical, left): chronological events од domain event log filter-иран по `ClientOrderId`.
- **Progress widgets**:
  - „Произведено: 234/500 парчиња (47%)"
  - „Гаранција: задолжено €12,400 / €25,000 (50%)"
  - „Рок: 32 дена до RequestedShipDate"
- **Linked entities tabs**: Declarations, Production Orders, Shipments, Materials in stock.

**Business rules.**
- Не може да се избрише ClientOrder ако има linked CustomsDeclaration или ProductionOrder (само soft-delete cascade за Cancelled).
- Status transitions се computed, не user-editable (освен Cancel).
- ClientOrder не може да биде „Closed" додека постојат отворени Inventory rows со `LonProcessState ∈ {Imported, InProduction}` за неговите материјали.

**Edge cases.**
- Клиент менува нарачка → нов ClientOrder со „supersedes" линк назад? Или edit на постојниот? **Одлука: edit на постојниот ако е во Draft/Active без shipments; иначе нов поврзан со `SupersededByClientOrderId`.**
- Клиент откажува → user marks Cancelled; ако има задолжена гаранција, треба `WasteDeclaration` или `ReturnDeclaration` за да се ослободи.

**AI helper hooks.**
- На креат: provide RAG-based estimate „слични нарачки во минатото беа произведени за просечно 18 дена; вашиот RequestedShipDate е реален".
- На detail page: detect blocked transitions („нема FG со BOM yet — кликни 'Внеси готови производи'").

---

### §5.2 — Увоз на материјали (CustomsDeclaration IM)

**Намера.** Регистрирaj увозна царинска декларација која ги внесува материјалите потребни за ClientOrder. Тоа задолжува гаранцијата.

**Учесници.** Customs Officer (главен), Warehouse Operator (го очекува физичкиот прием), Finance Clerk (го следи задолжувањето).

**ELON-was.** Manual: frmAzurZak → cmdNovaFakturaU5 → frmNovaFakturaU5 (header) → frmFakturiU5 (lines). Bulk: cmdPrevzemanje → frmTransfer<Uvoznik> → frmNovTransferFakturaU5 (678-line VBA monster).

**LON-spec.**
- `CustomsDeclaration` (DeclarationType="IM") со FK кон `ClientOrderId` и `LONAuthorizationId`.
- Auto-generated DeclarationNumber: `IM-{year}-{6digit}` од SEQUENCE.
- DeclarationLine rows: itemRef, qty, UoM, unit price, foreign currency, tariff code, country of origin, country of dispatch.
- Computed (по `cmdFormiraj` analog — Phase 17 named `CalculateDeclarationDuties`):
  - Customs base = `quantity * unit_price * fx_rate`
  - Customs duty = base * tariff_rate
  - VAT = (base + duty) * vat_rate (default 18%, override from `TariffCode.VatRate`)
  - Total duty = duty + (vat if applicable based on procedure)
- На submit → status = `Submitted`; на customs approval (Zaverka) → status = `Approved`, ZaverkaBroj/Datum saved, **`CustomsDeclarationApprovedEvent`** emitted.
- Event handler: создава `GuaranteeLedgerEntry` (Debit) со amount = SUM(line.TotalDuty).

**UI entry points.**
- ClientOrder hub action launcher → „Креирај увозна декларација".
- `/customs/import-docs` route — list view (постои).
- **Inline create dialog** во ClientOrder hub (без navigation away from hub — preserve контекст).

**Bulk import (Phase 17 alternative path):**
- Customer-specific Excel/CSV import (KW12 wizard постои — `/tools/import/kw12`).
- Map columns to declaration lines; preview; commit creates `CustomsDeclaration` + lines + `ImportSession` audit.
- Customer formats: TEKSPORT, DREKKV, GENTHERM, JONSON, others — supported via `ImportMappingProfile`.

**Business rules.**
- Не може да се „Approve" декларација ако `Sum(line.TotalDuty)` го пробива `LONAuthorization.GuaranteeAmount - currentBalance`. Освен ако корисникот има override permission и потврди со reason.
- Skart (бракан примен материјал) — separate entity, NE се одзема од duty pre-receipt; одзема se po receipt со `Skart.SkartKol > 0`.
- Average rate (`ProsecnaSTDaNe`) — per-declaration toggle; supported via `AverageRateOverride` (P15.17, веќе постои).

**Edge cases.**
- Catalog mismatch (incoming line има непознат `ArtKatBr`): UI prompts „Региструрај нов артикал" (modal со prefilled fields од import row); или „Mapирај на постоен" (search-and-select).
- Tariff lookup miss: prompts user to override rate manually со reason; logged во audit.
- Duty calc mismatch вс. customs response: variance flag; user must reconcile before approval.

**UX detail: sticky values + bulk currency change.**
Линискиот entry користи sticky-values pattern (BLUEPRINT §7.3.1): Currency / UoM / CountryOfOrigin / TariffCode се auto-prefill-уваат од последниот внесен ред. Toolbar на line-table има „🔄 Смени валута на цел документ" — bulk update со confirmation („Recalculate-ира Vrednost на сите редови според FX rate. Продолжи?") и AuditLogEntry запис.

**AI helper hooks.**
- На IM creation: suggest „слични претходни декларации од овој клиент имаа просечен duty ratio X%; вашиот е Y% — provери ако се очекувано".
- На new material catalog entry: pre-fill specifications од similar existing items.

---

### §5.3 — Прием во магацин (Receipt)

**Намера.** Физички материјал пристигна. Магационер потврдува количини и распределува на локации.

**Учесници.** Warehouse Operator (главен), Quality Controller (ако треба QC hold).

**ELON-was.** Не постоеше „receipt" entity. Inventory rows се создаваа имплицитно при `cmdPresmetaj` на FakturiU5. Магационерот немаше своје UI flow.

**LON-spec.**
- `Receipt` entity (`ReceiptType=Import|Domestic|Return|Production`) со FK `CustomsDeclarationId` (nullable за domestic).
- `ReceiptLine` rows: declarationLineRef, qty actually received, locationId, batchNumber, MRN, qualityStatus (initial OK или Hold).
- Auto-generated ReceiptNumber: `RC-{year}-{6digit}`.
- Submit:
  - Decrement остатокот на declaration line (за expected vs received reconcile).
  - Insert `InventoryBalance` row(s) — per (item, location, batch, MRN) — со `LonProcessState=Imported`.
  - Emit `ReceiptCommittedEvent`.
- Variance handling: ако received != declared, prompt за Skart (defective) или variance reason (loose packaging, etc.).

**UI entry points.**
- ClientOrder hub action launcher → „Прими во магацин".
- `/warehouse/receipts` — list view (постои како Inventory.tsx; Phase 16.B1 ja мигрираме на react-query).
- `/warehouse/incoming` — view of declarations awaiting receipt (existing).
- **Barcode scan flow** (mobile, post-v1): scan MRN → auto-locate declaration → quick-receive UI.

**Business rules.**
- Не може да се прими без линкувана approved CustomsDeclaration (за non-domestic types).
- Quality status default = OK; user може explicitly да го стави Hold/Quarantine/Rejected.
- Skart: portion declared as defective → не влегува во InventoryBalance, instead `Skart` entity row + duty pro-rata to guarantee credit (return-of-duty).

**Edge cases.**
- Partial receipt (50 of 100 expected): остава declaration line „partially received"; remainder може да дојде во следен Receipt.
- Excess receipt (110 of 100): block by default, prompts „Зголеми декларација прво" или „Прифати со variance reason".

**AI helper hooks.**
- На variance ≥ 5%: „слични receipts од овој снабдувач имаа просек 2% variance; 5% е unusual — провери packaging count".
- Suggest location based on item history (most-used past location for this item).

---

### §5.4 — Готови производи + нормативи (BOM, ProductionOrder)

**Намера.** Дефинирај што ќе се произведе од примените материјали и со кој норматив.

**Учесници.** Production Planner (главен), Manager (одобрува за големи).

**ELON-was.** frmGotoviProizvodi + frmNormativi (+ NormativiVelicini, +NormativTemplO/S template auto-apply). Сложен sub-flow.

**LON-spec.**
- `ClientOrderFinishedGood` rows на ClientOrder (што се произведува, колку).
- `BOM` + `BOMLine` дефинираат норматив (per material per FG).
- `BOMLineWasteOverrides` (P15.6, постои) — 4 waste slots + Zaguba per BOMLine.
- `ProductionOrder` се создава од ClientOrderFinishedGood (1-to-1 ако нема variants; 1-to-N со size variants — `ProductionOrderMaterialSize` за NormativiVelicini).
- Template auto-apply (NormativTemplO/S analog): кога нов BOM се создава за Item што има постоен BOM, „Apply template" suggestion со last-used BOM за тој item; user confirms.

**UI entry points.**
- ClientOrder hub action launcher → „Внеси готови производи".
- `/master-data/boms` — separate maintenance UI (постои).
- **Inline editor** во ClientOrder hub: add FG row → autocomplete from Items → pick/create BOM → preview normatives → confirm.

**Business rules.**
- Mora to be either а BOM linked OR a `NoBom=true` flag (rare; for trivial assembly).
- Inflate-for-waste correction (legacy DREKKV/TEKSPORT): customer-template imports apply `KolMat * 100/(100-ArtOtpadProc)` when `Tenant.InflateForWasteEnabled=true`. **Legacy data check (2026-05-12 PREP):** only 4 articles out of 8,960 (0.04%) in TEKSPORT ELON have non-zero `ArtOtpadProc`, max 2%. Treat as legacy feature flag, **default OFF in v1**; TEKSPORT migration sets `true` to preserve audit-trail compatibility. Other migrated tenants opt-in explicitly.
- Lock после first ProductionReceipt — само admin со reason може да менуа BOM.

**Edge cases.**
- Size variants (S/M/L/XL): NormativiVelicini values weighted-average back to parent BOM line (legacy logic).
- Material substitution (during production): create new BOM version (versioning post-v1; v1 = soft-replace со audit log).

**AI helper hooks.**
- Suggest BOM template: „слични FG-јa користеле BOM X (3 нарачки) или BOM Y (5 нарачки) — кој применуваш?"
- Spot suspicious norm: „Normativ за матерјал М е 0.5 m²/парче; типично е 0.3 — провери".

---

### §5.5 — Диспозиција за шпедитер/царина

**Намера.** Подготвувај и испрати export декларација до шпедитер или директно до царина (file-based).

**Учесници.** Customs Officer (главен).

**ELON-was.** Excel/CSV/MDB фајлови во `C:\PREV\<4chars>\<UvoznikFull>\<DDMMYYYY>\` со ftp.exe + bat script.

**LON-spec.**
- `/customs/declarations/{id}/export` endpoint (постои за IM declarations).
- За v1: download buttons на CustomsDeclaration detail page:
  - „Excel (Spediter format A)" — column mapping per Speditor profile
  - „CSV (Spediter format B)"
  - „PEE020 XML" (manual upload to customs portal — see §5.10)
  - „Legacy MDB" (optional, post-v1)
- Speditor profiles: `SpeditorExportProfile` entity (Phase 19; за v1 hardcoded дефаули за најкористен спедитер).

**UI entry points.**
- CustomsDeclaration detail page → „Export / диспозиција" action group.
- ClientOrder hub → „Експортирај за шпедитер" (with declaration selection).

**Business rules.**
- Не може да се експортира unapproved declaration (статус мора да биде `Approved`).
- Export action logged во audit (file generated, downloaded by user X at time T).

**AI helper hooks.**
- Suggest format based on customer's historical preference („за DREKKV обично се прави CSV-B").

---

### §5.6 — Распределба на работа (Podelba) кон подизведувачи

**Намера.** Производителот ги распоредува материјалите од магацин кон конкретен подизведувач, кој физички шие.

**Учесници.** Production Planner (главен), Warehouse Operator (го извршува физичко поместување).

**ELON-was.** frmPodeliBaranjaBrz — multi-line inserts на LagerMaterijali со Proces=1 за producer.

**LON-spec.**
- `PodelbaCommand` (постои како MediatR command) — multi-row distribution.
- Inputs: ClientOrderId, sourceLocationId, targetProducerId, items[] (item + qty + targetLocationId).
- Effect:
  - InventoryBalance source row → decrement.
  - InventoryBalance target row → increment, `AssignedProducerId=targetProducerId`, `LonProcessState=Imported` (still imported until issued to production).
  - `InventoryMovement` row тип `Distribution`.
  - Optional: Generate `Propratnica` PDF (delivery note) for physical handover.

**UI entry points.**
- ClientOrder hub action launcher → „Распредели подизведувач".
- `/warehouse/podelba` (постои).
- Bulk action од Inventory list: select rows → „Подели кон…" → producer picker.

**Business rules.**
- Не смее да се подели повеќе од available qty на source location.
- ProducerPartner мора `PartnerType=Producer` со „active" status.
- Cancel within window (24h) — restore source, undo InventoryMovement; after window, requires reverse Podelba.

**Edge cases.**
- Producer одбива material (defect found post-handover): нов entity `ProducerReturnNote` (post-v1 nice-to-have; v1 = manual InventoryMovement reverse со reason).
- Material loss at producer (theft/damage): producer must declare → triggers Skart-like entry → guarantee adjustment.

**AI helper hooks.**
- Suggest producer based on:
  - Available capacity (from ProductionOrder load aggregation)
  - Historical performance (on-time delivery rate from completed orders)
  - Geographic proximity (if Partner.Address geocoded)
- Format: „за оваа нарачка препорачано: Producer X (3 minutes from warehouse, 95% on-time, 12 capacity hours free this week)".

---

### §5.7 — Издавање материјал од магацин (MaterialIssue)

**Намера.** Материјал формално преминува од „на залиха при производителот" во „во производство за конкретен ProductionOrder". Тоа е moment-от кога material започнува да се consume-ира против BOM.

**Учесници.** Warehouse Operator (физички pick на материјал), Production Planner (одобрува за пуштање), Production Operator at producer (физички приема + започнува работа).

**ELON-was.** During Podelba: `LagerMaterijali` Proces=1 → Proces=7 (exit-to-producer). Formal document: **Izdatnica** (`frmIzdatnici` → `Izdatnici` table, 1,119 rows). `Ispratnica` (`frmIspratnici` → `Ispratnici` table, 776 rows) е **destruction certificate** (Proces=9 waste), не material issue — legacy research notes го имаа помешано. Match-rate: `LagerMaterijali.DokRBr` за Proces=7 vs `Izdatnici.RBr` = **99%**; vs `Ispratnici.RBr` = 12% (RBr coincidence only).

**LON-spec.**

`MaterialIssue` entity (постои):
- TenantId, ProductionOrderId (FK), IssueDate, IssuedBy (Guid User), IssueNumber (SEQUENCE `MI-{year}-{seq:D6}`).
- Status: Draft | Released | Reversed.
- Per-line via `ProductionOrderMaterial.IssuedQuantity` increment (lines не се сепарат entity; live на PO).
- Optional: `ReceivedAt` + `ReceivedBy` (производителот потврдува прием — v1 nice-to-have, v1.1 mandatory).

Effect on commit:
- InventoryBalance at producer location: decrement (per material per location).
- ProductionOrderMaterial.IssuedQuantity += issued qty.
- InventoryMovement row тип `Issue` (with FK MaterialIssueId).
- `LonProcessState`: Imported → InProduction (на новата InventoryBalance „in production at producer X" bucket).
- Emit `MaterialIssueCommittedEvent`.

Reversal flow (v1 — needed for mistakes within 24h window):
- `MaterialIssueReversedEvent`. Sets MaterialIssue.Status = Reversed. Inverse InventoryMovement.
- After 24h: requires Administrator approval со reason (audit log).

**UI entry points.**

*v1:*
- ProductionOrder detail page → „Издади материјал" button — full editor: pick lines from BOM-required, override qty per line, default location = producer's primary.
- ClientOrder hub action launcher → „Издади материјал" (PO selection step first).
- `/production/orders` list → row action „Quick-issue all" — bulk-issue all BOM-required materials at planned qty.

*v1.1+:*
- Mobile flow (Flutter): warehouse operator scans MRN барcode → auto-locate PO → quick-issue UI.
- Producer-side dashboard (Phase 18): subcontractor sees pending issues, „acknowledge receipt" button.

**Business rules.**
- Issue qty ≤ Available qty at producer location (computed from InventoryBalance).
- ProductionOrder must be in status `Released` (not Draft).
- Per-size variants: issue per `ProductionOrderMaterialSize` row separately.
- Bulk-issue uses BOMLine.Normativ × PO.OrderQuantity as default qty.
- Issue beyond BOM (manual additional material): allowed but requires reason; logged as variance.

**Contextual actions on ProductionOrder detail (after issue):**
- „Поништи издавање" (within 24h, anyone) / Administrator only (after).
- „Прикажи историја на издавања" — list of all MaterialIssues for this PO.
- „Прикажи преостанато за издавање" — for each BOMLine: required - issued = remaining.
- „Издади дополнително" — beyond BOM, with variance reason.

**Edge cases.**
- **Material shortage:** prompts „Има 80 на лагер, треба 100. Дозволи partial issue?" — yes creates partial issue + remainder logged as `MaterialShortageEvent` (visible on hub + ProductionAtRisk page).
- **Over-issue beyond BOM:** prompts variance reason; allowed но flagged. Audit log запис.
- **Material moved away (Podelba reversed) post-issue:** blocks reversal; would create orphan in-production stock.
- **Wrong PO chosen at warehouse:** within 24h, „Преместете во друго PO" → reverse + re-issue.

**AI helper hooks.**
- Spot issue patterns: „за PO X, материјал М е под-issue-ован 3 пати — гледа дека има шорт на лагер".
- Smart default location: based on past Receipts of this Item.

---

### §5.8 — Производство и следливост

**Намера.** Производство трае од moment of „first MaterialIssue" до „final ProductionReceipt". LON следи **трите рамки на следливост**: order-level (количини), operation-level (време + оператор + машина), material-level (што влегло, што излегло, што е отпад).

**Учесници.** Production Operator (записи на произведено + времиња), Production Planner (мониторира + одобрува), Quality Controller (rejects), Production Manager (alerts + reassignment), Maintenance Tech (machine downtime).

**ELON-was.**
- LagerGotoviProizvodi Proces=6 inserts на ProductionReceipt.
- Operacijata-by-operacija tracking преку Routings + OperationLog (limited).
- Producer-side capture мнoгу minimal — повеќето е back-entered од планер.

**LON-spec — three tiers (different v1 vs post-v1 statuses):**

---

#### §5.8.1 — Tier 1: Order-level production receipts ✅ v1

`ProductionReceipt` entity (постои):
- TenantId, ProductionOrderId, ReceiptNumber (SEQUENCE), ReceiptDate, ReceivedBy.
- Per-line: FG item, qty produced, qty scrapped (within BOM tolerance), batch number, location.
- Status: Draft | Committed | Reversed.

Effect on commit:
- ProductionOrder.ProducedQuantity += received qty; ScrapQuantity += scrapped qty.
- InventoryBalance „finished goods at producer location": increment.
- Materials consumed (pro-rata BOM per line): decrement „in production" bucket per material.
- Otpad slots (4 + Zaguba): increment per BOM-defined % unless override (see §5.8.4).
- Emit `ProductionReceiptCommittedEvent`.
- ProductionOrder auto-status: Released → InProduction (on first receipt) → Completed (when ProducedQuantity ≥ OrderQuantity).

Business rules:
- ReceiptLine qty + scrap ≤ Issued material qty proportionally (per BOMLine.Normativ — pro-rata check).
- Per-size receipts when PO has size variants: separate ProductionReceiptLine per size.
- Backdated receipts (Production Operator forgot to enter): allowed within 7 days; older requires Administrator override.

UI entry points:
- ProductionOrder detail → „Запиши производство" button.
- ClientOrder hub → „Запиши производство" (PO selection).
- `/production/today`, `/production/wip`, `/production/completed` — overview lists with quick-record actions.

---

#### §5.8.2 — Tier 2: Operation-level time tracking ⚠ v1 minimal, v1.1+ full

`OperationTimeLog` entity (v1 minimum stub; v1.1 full):
- TenantId, ProductionOrderId, RoutingOperationId, EmployeeId (operator), MachineId (optional), ShiftId.
- StartedAt, FinishedAt (nullable while active), Status (InProgress | Completed | Paused | Aborted).
- QuantityProduced (per session), QuantityScrap (per session).
- SetupTimeMinutes (separate from production time).
- DowntimeMinutes (machine downtime within this session — linked to DowntimeEvent).

*v1 scope:*
- Entity + basic CRUD endpoints + manual entry UI на ProductionOrder detail (по-планер entry).
- Aggregation за ETA forecast (AI helper hook).

*v1.1+ scope:*
- Real-time „start work" / „pause" / „finish" buttons за оператори (mobile or kiosk).
- Auto-link на Machine via barcode/QR scan.
- Auto-attribute downtime overlaps.
- OperatorPerformance reports (output/hour, scrap rate per operator).

Business rules:
- StartedAt < FinishedAt (sanity).
- Overlapping sessions for same operator blocked (operator can only be on one PO at a time).
- ScrapQuantity per session must reconcile to ProductionReceipt aggregate when PO closes.

UI entry points (v1):
- ProductionOrder detail → „Време + оператор" tab → manual time log entry.
- `/production/minutes-variance` (постои) — actual vs planned per RoutingOperation.

UI entry points (v1.1+):
- Operator mobile app: shift dashboard → tap PO → start; pause; finish.
- WorkCenter kiosk display: live status of all active PO sessions.

---

#### §5.8.3 — Tier 3: Machine + downtime tracking ⚠ v1 minimal, v1.1+ full

`MachineStateEvent` entity (постои):
- MachineId, EventType (Running | Idle | Down | Setup | Maintenance), StartedAt, EndedAt, Reason (FK to CodeListItem).

`DowntimeEvent` entity (постои):
- MachineId, StartedAt, EndedAt, ReasonCode (Mechanical | Material | Operator | Other | Planned).
- ResolutionNotes, ResolvedBy.
- ImpactedProductionOrders (M:N — derived from OperationTimeLog overlap).

`MaintenanceSchedule` + `MaintenanceWorkOrder` (постои) — preventive maintenance plans.

*v1 scope:*
- Manual entry на DowntimeEvent (Maintenance Tech записи when machine goes down).
- Display на ProductionOrder detail: „machine downtime overlapping this PO".
- `/machines/status`, `/machines/downtime`, `/machines/oee` (постоen, нo data come от manual entries).

*v1.1+ scope:*
- IoT integration (kанди MQTT broker) — auto-capture state changes.
- Predictive maintenance via RAG suggestions.
- Real-time OEE dashboards per WorkCenter.

Business rules:
- DowntimeEvent overlap detection: alert when 2+ active.
- Maintenance scheduled in future blocks PO planning (cannot assign PO to machine during scheduled downtime).

UI entry points:
- /machines/* routes (постоен).
- ProductionOrder detail → „Машинско време" tab.
- Quick-action „Пријави downtime" на машина detail page.

---

#### §5.8.4 — Otpad (manufacturing waste) tracking ✅ v1, real-time entry

Otpad е централно за inward processing — секој gram материјал треба да биде declared како consumed, exported, returned, или waste. Корисникот е во право да го истакне ова — go третираме first-class.

**Two flow paths:**

*Path A — BOM-implied (auto-computed):*
- BOMLineWasteOverrides defines per-line % за 4 slots: Otpad/Otpad1/Otpad2/Zaguba.
- На ProductionReceipt commit: pro-rata decrement of consumed material into 4 slot accounts.
- Computed automatically; not user-entered.

*Path B — Manually-recorded scrap event (real-time):*
- `ScrapEvent` entity (Phase 17):
  - TenantId, ProductionOrderId, ItemId (waste material), Quantity, UoM.
  - SlotIndex (0..3 for Otpad/Otpad1/Otpad2/Zaguba).
  - Reason (FK CodeListItem), Notes, ReportedBy, ReportedAt, BatchSource (which material batch).
  - ApprovedBy + ApprovedAt (optional).
- Triggers: operator notices abnormal scrap during production; QC rejects partial batch; quality test fails.
- Effect: InventoryBalance „otpad bucket" up; source material InventoryBalance down (separate from BOM-implied amount).

**Otpad reconciliation report** (`/production/otpad-reconciliation`):
- Per ClientOrder/ProductionOrder: BOM-implied vs actual scrap recorded.
- Variance flag if actual > BOM-implied + tolerance.
- Drill-down to ScrapEvent list with reasons.

**Norm repartition post-execution** (frmAzurNormativOtpad analog):
- Production Planner или QC може to move qty from main material consumption to otpad slot post-receipt (after-the-fact correction).
- New entity `NormativOtpadAdjustment`:
  - ProductionReceiptId (FK), BOMLineId, QtyMovedToSlot, SlotIndex, Reason, ApprovedBy.
- Recomputes ProductionReceipt totals + emits `NormativAdjustedEvent` (which guarantee handler re-evaluates).

UI entry points:
- ProductionOrder detail → „Запиши отпад" button (ScrapEvent creator).
- ProductionOrder detail → „Корекција на норма" button (NormativOtpadAdjustment).
- `/production/otpad-reconciliation` — overview.
- AI helper highlights mismatches automatically (3rd core recommendation actually applies here too).

*v1 scope:* Path A (BOM-implied) + manual ScrapEvent entry + reconciliation view.
*v1.1+ scope:* photographic evidence per ScrapEvent; weight-station integration; bulk scrap event capture.

---

#### §5.8.5 — Rework flow ✅ v1

Rework = production batch that came out defective but is recoverable (rejected at QC, sent back to fix).

`ReworkOrder` entity (v1 — currently `pages/Production/Rework.tsx` stub exists; entity to be created Phase 17):
- TenantId, OriginalProductionOrderId, OriginalProductionReceiptId, Quantity, Reason, ReworkedBy, Status (Open | InProgress | ReceivedBack | Abandoned).
- Linked to: producer (same as original), location.

Effect:
- Finished goods → back to „in production" (LonProcessState flip).
- Materials consumed during rework: ScrapEvent OR additional MaterialIssue (depending on what's needed).
- After rework receipt: original ProductionReceipt has annotation „X qty reworked"; quality status flips back to OK.

UI entry points:
- ProductionReceipt detail → „Започни рework" button (when status=Rejected by QC).
- ClientOrder hub → „Преглед на rework налози".
- `/production/rework` (постои — to be wired Phase 17).

Business rules:
- Rework quantity ≤ Original receipt qty.
- ReworkOrder can be abandoned → original qty becomes ScrapEvent (waste).
- Rework consumes extra time (logged in OperationTimeLog) и материјал (issued anew).

---

#### §5.8.6 — Producer-side UI (Subcontractor) ⚠ Phase 18

When subcontractor logs in:
- Dashboard: their assigned active POs (status, due date, % complete).
- Per-PO view: required materials (issued vs remaining), required FGs (qty + sizes), routing operations.
- Action: „Запиши производство" (creates ProductionReceipt; same flow as planner-side but scoped).
- Action: „Запиши време" (OperationTimeLog).
- Action: „Запиши отпад" (ScrapEvent).
- Cannot see: other producers, customs, finance, master data.

UI implemented Phase 18 (BLUEPRINT §4.3, AGENT-PROMPTS §F3).

---

#### §5.8.7 — ProductionOrder detail page: contextual action launcher

Per BLUEPRINT §7.2 (contextual actions everywhere), ProductionOrder detail има sticky right panel со relevant акции based on current PO state:

| Action | Visible when | Phase |
|---|---|---|
| Issue materials | status ∈ {Released, InProduction} AND materials pending | v1 |
| Quick-issue all | same as above | v1 |
| Record production receipt | status ∈ {Released, InProduction} | v1 |
| Log operation time | always | v1 |
| Record scrap (ScrapEvent) | always | v1 |
| Normative adjustment | after first receipt | v1 |
| Start rework | when PR has rejected lines | v1 |
| Reassign producer | status = Released and no issued materials | v1 |
| Change normative (BOM edit) | status = Released and no issued materials; Administrator override needed if any issued | v1 |
| View material trace | always | v1 |
| View time log | always | v1 |
| View downtime overlap | always | v1 |
| View QC results | after receipts | v1 |
| Close PO | status = Completed (all FGs produced) | v1 |
| Cancel PO | status = Released and no issued materials | v1 |
| Print work order | status = Released | v1 |
| Mobile QR for operator | always (Flutter app) | v1.1 |
| AI helper | always | v1 (3 core recs apply) |

---

#### §5.8.8 — WorkCenter + Machine + Routing master data ✅ v1 (existing, polish in Phase 17)

`WorkCenter` entity (постои):
- TenantId, Code, Name (mk/en), Description.
- Location (FK Location — physical area within a Warehouse).
- HoursPerDay (planning capacity baseline; e.g. 16h for two-shift floor).
- WorkingDaysPerWeek.
- Status: Active | Inactive.
- Children: Machines belonging to this WorkCenter.

`Machine` entity (постои):
- TenantId, Code, AssetNumber, Name, Type (Cutting | Sewing | Pressing | Embroidery | Packaging | Other).
- WorkCenterId (FK), Status (Operational | Down | Maintenance | Retired).
- AcquiredOn, ServiceLifeYears.
- CapacityUnitsPerHour (nominal — used for planning baselines).
- CurrentOperatorId (computed — latest active OperatorMachineAssignment).
- Children: MachineStateEvent history, DowntimeEvent, MaintenanceWorkOrder, OperationTimeLog sessions.

`Routing` + `RoutingOperation` (постои):
- Routing: TenantId, Code, Name, FinishedItemId (FG that uses this routing).
- RoutingOperation: RoutingId, Sequence, Code, Name, RequiredMachineType (or specific MachineId), StandardMinutesPerUnit, SetupMinutes.
- Multiple Routings can exist per Item (variant versions); ProductionOrder picks one.

UI entry points (master data CRUD):
- `/master-data/work-centers` — list + detail с inline edit.
- `/master-data/machines` — list + detail.
- `/master-data/routings` (постои) — list + per-operation editor.

Contextual actions on Machine detail (v1):
- View current operator | View today's PO sessions | View downtime history | Schedule maintenance | Mark as down (Maintenance Tech action).

---

#### §5.8.9 — Production planning + capacity scheduling ⚠ v1 simple, v1.1+ full

*v1 — simple capacity check:*
- When Production Planner creates ProductionOrder (from ClientOrder hub, §5.4), system computes required machine-hours:
  - For each RoutingOperation × OrderQuantity → required minutes per machine type.
- System checks `WorkCenter.HoursPerDay × WorkingDaysPerWeek × Machine count of required type` versus existing PO load in same period.
- Returns capacity utilization % preview before confirm: „за периодот 12–18 јуни: cutting capacity 95% утилизирана, sewing 78%".
- If overload (>100%), prompts user: continue (manager override) or reduce qty / extend date.

*v1.1+ — full scheduling engine:*
- `ProductionSchedule` entity: per ProductionOrder, scheduled start/end per RoutingOperation, assigned machine.
- Drag-and-drop Gantt UI (`/production/schedule`).
- Auto-suggest optimal scheduling (minimize setup time, respect machine capabilities, balance load).
- Reschedule on disruption (DowntimeEvent, urgent PO).
- Constraint solver (OR-Tools or custom): minimize lateness, respect resource constraints.

UI entry points:
- v1: ClientOrder hub action „Создај налог за производство" → inline capacity preview widget.
- v1.1+: `/production/schedule` Gantt view, `/production/load` capacity dashboard.

Business rules:
- Capacity check is advisory in v1 (no hard block); only warning. v1.1+ may enforce.
- Scheduled PO blocks the same machine for that time window (others see conflict).

Contextual actions:
- v1: View capacity load for date range | Suggest start date for PO.
- v1.1+: Re-schedule PO | Lock schedule | Optimize day | Print shop schedule.

---

#### §5.8.10 — Routing operations execution + queues ✅ v1

When Production Operator works on a PO, the work is broken into RoutingOperations (cutting → sewing → finishing → packaging). Each has a queue.

`OperationQueue` (computed view, не entity):
- For each WorkCenter / RoutingOperation type: ordered list of POs that need this operation (status InProduction, not yet executed this op).
- Pre-existing pages: `pages/Production/CuttingQueue.tsx`, `OperationQueue.tsx` (parameterized by operationType).

When operator starts a session:
- Creates OperationTimeLog (§5.8.2) with StartedAt, OperatorId, MachineId.
- On finish: enters Qty + Scrap; closes the log; updates PO progress per-operation.

`ProductionOrderOperation` entity (постои):
- POid, RoutingOperationId, Sequence, Status (NotStarted | InProgress | Completed), ActualMinutes, PlannedMinutes.
- ActualScrapQty, ActualGoodQty.

UI entry points:
- `/production/cutting-queue`, `/production/sewing-queue` etc. — queues per op type.
- ProductionOrder detail → „Routing operations" tab — per-op status table.
- v1.1+ mobile: operator scans PO barcode → starts session.

Business rules:
- Operations executed in sequence (sequence number ascending) unless explicitly parallel-flagged on Routing.
- Cannot start operation 2 before operation 1 is completed (or marked „skipped" with reason).

---

#### §5.8.11 — Real-time shop floor view + bottleneck analysis ⚠ v1 minimal, v1.1+ live

*v1 minimal:*
- `/production/today` — list of all in-progress POs with assigned operator + machine + start time + progress.
- `/machines/status` — list of all machines with current state (Running on PO X | Idle | Down).
- `/production/wip` — work-in-progress overview, refresh-on-action.
- Bottleneck analysis: `/machines/bottleneck` — list of machines with utilization >90% over last 7 days.

*v1.1+ — live shop floor:*
- WebSocket-pushed updates when MachineStateEvent / OperationTimeLog changes.
- Big-board kiosk display (large monitor in production floor): live status board, current production rate vs plan.
- Color-coded alerts (machine down >30 min → red; setup time >planned → yellow).

UI entry points:
- v1: `/production/today`, `/machines/status`, `/machines/bottleneck`.
- v1.1+: `/floor-board` kiosk page; mobile alert push notifications для managers.

---

#### §5.8.12 — Efficiency reporting (OEE, setup time, performance) ⚠ v1 manual, v1.1+ auto

**OEE** = **Availability** × **Performance** × **Quality**

- Availability = (PlannedTime − DowntimeMinutes) / PlannedTime
- Performance = (ActualOutput × StandardMinutesPerUnit) / RunTime
- Quality = GoodOutput / TotalOutput

`MachineEfficiencyDaily` (computed daily view / materialized):
- Date, MachineId, OEE, Availability, Performance, Quality, MinutesPlanned, MinutesActual, GoodUnits, ScrapUnits.
- Source: aggregation од OperationTimeLog + DowntimeEvent + ProductionReceipt.

UI entry points:
- `/machines/oee` (постои) — per-machine OEE chart over time, drill-down to component metrics.
- `/machines/setup-time` (постои) — setup time variance vs planned.
- `/management/capacity` — overall floor utilization.

*v1 scope:*
- Daily-aggregated views (batch-computed by LON.Worker at 02:00 nightly).
- Per-machine, per-WorkCenter, per-shift breakdowns.
- Export to Excel.

*v1.1+:*
- Real-time OEE (rolling 8h window).
- Per-operator efficiency comparison.
- Trend prediction (RAG-assisted).

Business rules:
- StandardMinutesPerUnit must be set on RoutingOperation for Performance to compute.
- Setup time tracked separately from production time (operators flag „setup" mode при clock-in).

---

**Cross-cutting business rules за §5.8:**

- ProductionOrder status state machine: Draft → Released → InProduction → Completed → Closed; Cancelled is terminal alternative.
- Material consumption proof — мора биде conservation: Sum(Issued) = Sum(Consumed_in_Receipts) + Sum(ScrapEvent) + Sum(Remaining_InProduction). Mismatch = blocking issue.
- Time conservation — OperationTimeLog sessions should not overlap for same operator/machine; downtime overlapping production session reduces effective production time.

**AI helper hooks за §5.8 (extends 3 core recommendations):**
- ETA forecast: „при тековен темпо (10 парчиња/ден), PO X ке заврши на 12 јуни; RequestedShipDate е 10 јуни → разгледај reassign на втор подизведувач".
- Anomalous waste: „просечниот waste за FG-7 е 3.1%; ова PR пријавува 6.5% — провери последно одржување на машината (last DowntimeEvent: 3 days ago, mechanical)".
- Material shortage prediction: „материјал М е consume-ан 80% бо 50% time → ke имаш недостаток за PO Y (запoчнва за 5 дена)".
- Operator performance: „просечен output на оператор X на машина Z е 8 парчиња/час; денес 5 → провери дали нешто блокира" (post-v1).

---

### §5.9 — QC + пакување (расширено)

**Намера.** Готов производ доaгa од подизведувач, се проверува за квалитет, се transferира во главен магацин, се пакува за извоз.

**Учесници.** Quality Controller (главен), Warehouse Operator (повторен прием во главен магацин), Packaging Operator, Production Planner (rework decisions).

**ELON-was.** Минимално — `frmGotoviProizvodiPak` (packaging). QC немаше formal entity; quality issues се ракуваа ad-hoc преку comments на `LagerMaterijali` rows и `frmReklamacii` (not in local DB slice). Не преку Ispratnici — Ispratnici е destruction certificate (Proces=9 waste), не QC sign-off.

**LON-spec.**

#### §5.9.1 — FinishedGoodReceipt (HQ-bound transfer)

Когa подизведувачот заврши batch, физички ja носи во главен магацин на производителот.

`FinishedGoodReceipt` (Phase 17 entity — variant of Receipt со ReceiptType=Production):
- TenantId, ProductionReceiptId (source FK), Number (SEQUENCE), Date, ReceivedBy.
- Source location: producer's stock.
- Target location: главен магацин на производителот (HQ).
- Lines: FG item, qty, batch, initial quality status (Quarantine if QC required, else OK).

Effect:
- InventoryBalance „at HQ finished": increment.
- InventoryBalance „at producer finished": decrement.
- Emit `FinishedGoodReceivedAtHQEvent`.
- Auto-trigger: QualityInspection record if Item.RequiresQC = true.

#### §5.9.2 — QualityInspection (richer than v1-minimal)

`QualityInspection` entity (Phase 17):
- TenantId, FinishedGoodReceiptId (FK), Inspector (User), InspectedAt.
- Status: Pending | Passed | Failed | PartialPass.
- DefectsFound (M:N to `DefectType` lookup) + DefectsCount per type.
- DefectsPct = DefectsCount / QtyInspected.
- Notes, PhotosAttached (v1.1+; v1 = single attachment URL field).
- Resolution: ApproveAll | RejectAll | PartialAccept (specify QtyAccepted) | SendBackToRework.

`DefectType` lookup (master data):
- Code, Name (mk + en), Severity (Minor | Major | Critical), TypicalCause.

Effect:
- Pass → InventoryBalance „at HQ FG, QualityStatus=OK".
- Fail → InventoryBalance „at HQ FG, QualityStatus=Rejected" + creates ReworkOrder OR WasteDeclaration option.
- PartialPass → split inventory: passed qty becomes OK, rejected qty becomes Rejected.

UI entry points:
- `/finished/qc-hold` (постои; FG variant) — list of pending inspections.
- FinishedGoodReceipt detail → „Изврши QC".
- Inspection form: defect picker (multi-select), qty per defect, photo upload, resolution action.

Contextual actions на FinishedGoodReceipt detail:
- Изврши QC | Пакувај | Изпрати назад на rework | Прогласи отпад (waste declaration).

#### §5.9.3 — Packaging (PackList)

*v1 — simple:*
- Packaging metadata directly on `ShipmentLine` fields (BoxCount, BoxesPerPallet, TotalGrossWeight, TotalNetWeight). No separate PackList entity.

*v1.1+ — rich:*
- `PackList` entity: lines mapping FG items to package boxes (PackList → PackBox → PackBoxLine). Supports mixed-package boxes, labels, carrier-specific format.
- Print pack-list labels.
- Verify pack-list at loading (barcode scan).

UI entry points (v1):
- `/finished/awaiting-pack` (постои; list of FGs at HQ awaiting packaging).
- Bulk-action „Пакувај" — opens dialog asking for box count + weight; creates ShipmentLine record stub.

UI entry points (v1.1+):
- Per-FG „Пакувај во кутии" detailed editor.
- Mobile scanner workflow.

#### §5.9.4 — Defect tracking + analytics (v1.1+)

Post-v1 analytics:
- DefectType frequency per Producer (which producer makes which defects most).
- DefectType frequency per Item (which products have most defects).
- Trend over time per ClientOrder, per quarter.
- Used in producer ranking + AI helper recommendations.

**Business rules.**
- FG не може да оди на shipment до status QualityStatus = OK.
- QualityInspection.Status = Failed → must choose: rework, waste, or partial accept.
- Rework receipt counts as new production cycle (separate ReworkOrder + new QualityInspection).
- Partial accept: rejected qty cannot just be „forgotten" — must be one of: rework / waste / return to customer (very rare).

**Contextual actions на QualityInspection detail:**
- View defect photos | Approve | Reject (with rework or waste choice) | Add note | Re-inspect.

**AI helper hooks.**
- Highlight critical orders: „рокот на ClientOrder X е за 3 дена и сè уште 50 парчиња на QC hold".
- Defect pattern alert (post-v1): „Producer Бета пријавува 8% defect rate на FG-7 last month, нормално 3% — провери".
- Rework decision recommendation: based on DefectType + historical rework success rate, suggest „rework" vs „waste".

---

### §5.10 — Извоз (CustomsDeclaration EX) + раздолжување

**Намера.** Готови производи се извезуваат како дел od inward processing → гаранцијата се раздолжува пропорционално на consumed материјали.

**Учесници.** Customs Officer (главен), Warehouse Operator (готови за товар), Speditor (post-v1, ако се logged).

**ELON-was.** Customs EX submission се правеше преку **PEE060 XML manual upload** на customs portal + commercial export invoice во `tblIzvozniFakturi` (3.2k headers, 57.9k lines — out-of-v1 per §9.1 D4). Inward-processing **exit-to-producer** flow (Proces=7 → Izdatnica) е **различен** flow (види §5.6 Podelba и §5.7 MaterialIssue) — не дел od EX customs. EX customs во ELON немаше еден „submit" клик; correlation между PEE XML + commercial invoice + ClientOrder се правеше manually.

**LON-spec.**
- `Shipment` entity (постои) со FK `ClientOrderId`, ShipmentType=Export, CustomsDeclarationId (EX type).
- `ShipmentLine`: FG item, qty, target country, value.
- `CustomsDeclaration` (DeclarationType=EX) co линкови до lines, computed exit duties.
- Effect on submit + approval (Zaverka):
  - InventoryBalance at HQ FG: decrement (qty shipped).
  - InventoryBalance materials consumed (pro-rata): LonProcessState transitions to Exported.
  - Emit `ShipmentCommittedEvent` + `CustomsDeclarationApprovedEvent`.
- Event handler creates `GuaranteeLedgerEntry` (Credit) со amount = `sum(consumed_material_duty * exported_ratio)`.
- Razdolzuvanje flag: `Shipment.RazdolzenaDaNe = true` once user confirms via Razdolzuvanje action (§5.11).

**UI entry points.**
- ClientOrder hub action launcher → „Креирај извозна декларација".
- `/customs/export-docs` (постои).
- `/finished/awaiting-pack` → bulk-select FGs → „Креирај извоз".

**Business rules.**
- Shipment must be linked to an approved EX CustomsDeclaration.
- IzpratnicaNumber auto-generated SEQUENCE (`IS-{year}-{6digit}`).
- Cannot ship more than available FG inventory at HQ location.
- PEE060 XML за раздолжување (manual download for v1) — endpoint постои.

**Edge cases.**
- Partial shipment: ClientOrderFinishedGood може да оди во повеќе shipments; tracking via `ShipmentLine.RemainingQty` analogous to declarations.
- Return shipment (customer rejects post-export): new ReturnDeclaration (variant of CustomsDeclaration) — reverses parts of guarantee credit.

**AI helper hooks.**
- Pre-flight check: „пред клик 'Поднеси извоз': гаранциски credit ќе биде €Y; постоечки balance €X → нов balance €Z. Сообразено со declared material consumption".

---

### §5.11 — Извештаи + Razdolzuvanje finale

**Намера.** Финален извештај за затварање на ClientOrder (или на ниво на LONAuthorization за периодот): задолжено, раздолжено, остаток, отпад, гаранциски статус.

**Учесници.** Manager (главен консумент), Finance Clerk, Customs Officer.

**ELON-was.** rptRazdolzuvanje, frmRazdolzuvanjeZak, PEE060 XML.

**LON-spec.**
- `RazdolzuvanjeReport` (computed; not entity) — generated on demand per ClientOrder or per LONAuthorization period.
- Aggregates:
  - SUM(IM duty) — what was charged
  - SUM(EX duty pro-rata + Waste + Return + FinalImport) — what was released
  - Variance flag if not balanced within tolerance (€0.50 default)
- Output formats: HTML view + PDF + PEE060 XML.
- Snapshot saved: `GuaranteeBalanceSnapshot` (post-Razdolzuvanje).

**UI entry points.**
- ClientOrder hub action launcher → „Razdolzuvanje" (single-order view).
- `/finance/guarantees/{authId}/razdolzuvanje` (multi-order roll-up за authorization period).
- Dashboard quick card „Open guarantees" → click thru.

**Business rules.**
- Razdolzuvanje цело не значи bookkeeping (тоа е надворешно за LON); LON само го документира.
- Per-customs-line reconciliation: each declaration line should have `RazdolzenaDaNe=true` set by user after report acceptance.
- Snapshot frozen (cannot edit historical data after taking).

**Edge cases.**
- Outstanding waste/return: ако не сите материјали се „consumed" (sum of Proces 7/8/9 ≠ sum of Proces 1), report flags it.
- Partial razdolzuvanje (some lines done, others awaiting customs): allowed; tracking by line.

**AI helper hooks.**
- Pre-Razdolzuvanje sanity: „претходна проверка: 12 IM lines имаат duty €X total, но само 10 имаат matching EX consumption — 2 missing. Ова е normalno?"

---

### §5.12 — Човечки ресурси (HR domain)

**Намера.** Управувa со луѓе кои работат во производство — нивни ангажмани, смени, присуство, отсуства, прековременa работа, обуки, performance — за да можат правилно да се распоредуваат, да се аутhentify-ат, и нивните часови да се извезат за плата.

**Учесници.** HR Manager (главен), Manager (одобрува overtime + absences), Production Planner (assignment), Production Operator (clock-in/out), Finance Clerk (payroll export).

**ELON-was.** Слабо покриено — основна evidencija на Firmi (производители) + Korisnici (logins). Нема formal HR domain.

#### §5.12.1 — Employees master data ✅ v1

`Employee` entity (постои):
- TenantId, EmployeeNumber (SEQUENCE `EMP-{seq:D4}`), FirstName, LastName, DateOfBirth, NationalId, Address, Phone, Email.
- HiredOn, TerminatedOn (nullable), Status (Active | OnLeave | Terminated).
- DepartmentId (FK Department — Cutting | Sewing | QC | Warehouse | Administration | etc.).
- PositionId (FK Position — Operator | Foreman | Supervisor | Mechanic | Inspector | etc.).
- DefaultShiftId (FK Shift).
- HourlyRate (decimal, currency), HourlyRateCurrency.
- UserId (FK — nullable; if set, this Employee can log in to LON).
- Soft-delete + audit.

`Department` lookup (master data — code list или separate entity).
`Position` lookup.

UI entry points:
- `/admin/employees` (постои — EmployeeManagement.tsx) — list + form.
- Employee detail: contact info, current shift, attendance summary, certifications.

Contextual actions on Employee detail:
- View attendance | View absences | View overtime requests | View certifications | View performance reviews | Assign to machine | Create user login | Terminate.

#### §5.12.2 — Shifts ✅ v1

`Shift` entity (постои):
- TenantId, Code (M | A | N — morning/afternoon/night), Name, StartTime, EndTime, BreakMinutes.
- WorkingDays (bitmask Mon-Sun).
- IsActive.

`ShiftAssignment` entity (Phase 17 if not present):
- EmployeeId, ShiftId, EffectiveFrom, EffectiveTo (nullable), Notes.
- Multiple assignments over time = employee's shift history.

UI entry points:
- `/admin/shifts` (постои — ShiftManagement.tsx) — list + form.
- Employee detail → „Распоред на смени" tab.

Business rules:
- One active ShiftAssignment per Employee per date.
- ShiftSwap (post-v1): formal swap request flow between employees.

#### §5.12.3 — Attendance ✅ v1 manual, v1.1+ auto

`AttendanceRecord` entity (постои):
- TenantId, EmployeeId, ClockInAt (datetime), ClockOutAt (nullable), Source (Manual | Kiosk | Mobile | Card), Notes.
- Computed: HoursWorked = ClockOutAt - ClockInAt - Shift.BreakMinutes.

*v1:*
- Manual entry by foreman/HR Manager at end of day via `/hr/attendance-today`.
- Kiosk-style UI for self clock-in (post-v1 polished, but minimal version OK in v1).
- Bulk entry: paste from existing Excel rosters.

*v1.1+:*
- RFID card readers at gate.
- Mobile clock-in with location validation.
- Auto-pair with OperationTimeLog (operator clocks in → their first session inherits clock-in time).

UI entry points:
- `/hr/attendance-today` (постои; partial) — quick clock for present employees.
- `/hr/attendance/history` — date-range view.
- Employee detail → „Часови" tab.

Business rules:
- One active (no ClockOut) record per Employee allowed (cannot clock-in if already in).
- Overlapping records blocked.
- Late corrections: HR Manager can edit prior days with audit log entry.

Contextual actions:
- Clock-in | Clock-out | Edit (with reason) | Bulk-import from CSV | Export to Payroll (§5.14).

#### §5.12.4 — Absences ✅ v1

`Absence` entity (постои):
- TenantId, EmployeeId, AbsenceType (Vacation | Sick | Unpaid | Public | Compensation | Other).
- StartDate, EndDate, Days (computed), Reason.
- Status: Pending | Approved | Rejected.
- ApprovedBy, ApprovedAt.
- AttachmentUrl (sick note, etc.).

UI entry points:
- `/hr/absences` (постои) — list + form.
- Employee detail → „Отсуства" tab.

Workflow:
- Employee (or HR on behalf) creates Absence with Pending status.
- Manager reviews + approves/rejects with comment.
- On approve: blocks employee from any new ShiftAssignment in that window; auto-creates corresponding AttendanceRecord entries marking absent.

Contextual actions on Absence detail:
- Approve | Reject | Edit | Cancel | Attach document.

#### §5.12.5 — Overtime ⚠ v1 = computed view, v1.1+ = formal workflow

*v1 — computed-only:*
- `/hr/overtime` (постои) — client-side rollup од AttendanceRecord: hours/day > `StandardHoursPerDay` (default 8) = overtime.
- Aggregated per Employee, per month.
- Без entity, без approval workflow.
- Подобар во payroll export — оvertime hours се додаваат в PayrollLine.OvertimeHours automatic.

*v1.1+ (Phase 25) — formal request workflow:*
- `OvertimeRequest` entity: EmployeeId, RequestedDate, Hours, Reason, Status (Pending | Approved | Rejected), ApprovedBy/At.
- Pre-approval ритуал (Employee submits → Manager approves) пред actual hours да се logged во AttendanceRecord.
- Maximum overtime per Employee per week configurable (legal limit per local labor law).

UI entry points:
- v1: `/hr/overtime` read-only view + payroll integration.
- v1.1+: same path с request form + approval queue за Manager.

Business rules (v1):
- StandardHoursPerDay configurable per Tenant (default 8).
- Overtime appears in PayrollLine.OvertimeHours computed.

#### §5.12.6 — Operator–Machine assignment ✅ v1

`OperatorMachineAssignment` entity (постои):
- EmployeeId, MachineId, EffectiveFrom, EffectiveTo (nullable), AssignedBy, IsPrimary.
- Multiple operators can be assigned to same machine (across shifts).
- Multiple machines per operator (cross-trained).

UI entry points:
- `/hr/assignment` (постои; OperatorAssignment.tsx) — matrix view.
- Machine detail → „Оператори" tab.
- Employee detail → „Машини" tab.

Used by:
- OperationTimeLog default machine pick (operator's primary).
- Capacity planning (operator availability multiplied by their assigned machines).
- Performance reporting (per-operator-per-machine output).

Business rules:
- Must have valid certification (§5.12.7) for machine's required certifications.
- IsPrimary: one machine flagged primary per operator (UI default).

#### §5.12.7 — Certifications ✅ v1 (Phase 16.C2)

`EmployeeCertification` (постои од Phase 16.C2):
- EmployeeId, CertificationName, IssuedDate, ExpiryDate, IssuingAuthority, CertificateNumber, Notes.
- Status: Active | Expired | Revoked.

UI entry points:
- `/hr/training` (постои) — list + form.
- Employee detail → „Сертификати" tab.
- Expiry traffic-light: green (>60 days), yellow (30–60 days), red (<30 days or expired).

Business rules:
- Machine has `RequiredCertifications[]` (post-v1 enrichment) — assigning operator without active cert prompts override.
- Expiry alerts visible on Manager dashboard.

#### §5.12.8 — Performance evaluation ⚠ v1 minimal, v1.1+ rich

*v1 minimal:*
- `/hr/performance` (постои) — view aggregated metrics per Employee:
  - Total hours last 30 days.
  - Output (sum of ProductionReceiptLine where this operator logged time).
  - Scrap % (sum of ScrapEvent where they were active session) — needs §5.8.2 v1 stub.
  - Attendance rate (% on-time vs late).
- Read-only.

*v1.1+ — rich:*
- `EmployeePerformanceReview` entity: PeriodStart/End, Rating (1–5), Strengths, Improvements, Goals, ReviewedBy.
- Workflow: Manager creates review → Employee acknowledges → archived.
- Trending: rating over time, drift alerts.

#### §5.12.9 — Payroll aggregation export ✅ v1 (Phase 16.C3.b)

`PayrollPeriod` + `PayrollLine` (постои од Phase 16.C3.b):
- Aggregates AttendanceRecord + Absence + OvertimeRequest per Employee per PayrollPeriod.
- Exports CSV/XML за надворешен payroll систем — **LON does NOT calculate salary**, само часовите.

UI entry points:
- `/finance/payroll` (постои).
- Finance hub → „Payroll" tile.

Workflow:
- Finance Clerk creates PayrollPeriod (monthly default).
- System auto-populates PayrollLine за секој Employee со RegularHours + OvertimeHours + AbsenceHours.
- Manual adjustments возможни со reason + audit.
- Export action: download CSV/XML; period status → Exported.

#### §5.12.10 — HR dashboard

`/hr/dashboard` (нов, Phase 17 polish or post-v1):
- Today's attendance: % present, late, absent.
- Active absences this week.
- Pending overtime approvals.
- Expiring certifications next 30 days.
- Open performance reviews due.

---

### §5.13 — Менаџмент репортинг (Management & Reporting)

**Намера.** Менаџер (главен газда) има единствено место (Dashboard) каде гледа КПИ на бизнисот — нарачки, производство, гаранции, маржа, on-time, capacity, alerts — со drill-down во кој било метрик.

**Учесници.** Manager (главен консумент), Production Planner (capacity + bottleneck), Finance Clerk (margin + AP), QC (defect trends), Customs Officer (guarantee aging).

**ELON-was.** Многу извештаи (200+ rptXxxx) но без интерактивен dashboard. Operator мораше да отвори формa за секoj.

#### §5.13.1 — Executive Dashboard ✅ v1

`/management/dashboard` (постои — Dashboard.tsx; polish in Phase 17):

Layout: cards by domain, drill-down on click.

**Cards (v1):**

1. **Active ClientOrders** — count + breakdown by status (Draft / Active / Producing / Shipped / Closed).
2. **On-time delivery KPI** — % of ClientOrders shipped before RequestedShipDate, rolling 90 days.
3. **Guarantee utilization** — traffic-light per LONAuthorization (red >90%, yellow 80-90%, green <80%); total exposure.
4. **Production capacity** — current WIP vs available capacity (utilization %); bottleneck WorkCenter highlighted.
5. **Margin** — gross margin per period (rolling 30/90 days); top 5 most-profitable customers.
6. **Inventory aging** — value of stock at producers >30/60/90 days (warning of slow-moving).
7. **Open alerts** — count by severity (Critical/High/Medium); link to alert list.
8. **Quality** — first-pass yield % (rolling 30 days); defect rate trend mini-chart.

Each card: click → relevant drill-down route.

#### §5.13.2 — Drill-down pages ✅ v1

Existing pages (mostly built, need consistency polish):

- `/management/on-time` — per-customer on-time delivery breakdown.
- `/management/by-customer` — revenue, orders, margin per customer.
- `/management/capacity` — capacity utilization by WorkCenter, period.
- `/management/margin` (alias of `/finance/margin`).
- `/management/alerts` — configurable alert list (see §5.13.4).
- `/management/risks` — risk register (Phase 16.C1).
- `/management/escalations` — escalation register (Phase 16.C1).
- `/management/trends` — multi-metric trend explorer.
- `/management/client-scorecard` — per-customer scorecard (delivery, quality, payment timing).
- `/management/monthly-pack` — monthly summary export (PDF + Excel).

#### §5.13.3 — Reports library ✅ v1 + ongoing

`/reports/` namespace contains operational reports (different from KPI dashboards):

- WMS dashboard, inventory by location/MRN/batch, blocked inventory, cycle count accuracy, warehouse utilization, movement reports.
- Razdolzuvanje per ClientOrder, per LONAuthorization (BLUEPRINT §5.11).
- Mozni minusi (potential shortages).

Each report: filterable, exportable (PDF + Excel + CSV).

Contextual actions on report:
- Filter | Export PDF | Export Excel | Schedule recurring email | Save filter preset.

#### §5.13.4 — Alerts system ✅ v1

`AlertRule` entity (Phase 17):
- TenantId, Code, Name, Severity, IsActive.
- TriggerCondition: SQL-like expression OR predefined enum (e.g. GuaranteeUtilizationAbove80, ClientOrderDueSoon, MachineDownOverHour, CertificationExpiringIn30Days).
- Threshold values.
- DeliveryChannels: Dashboard | Email | Push (post-v1).
- Recipients: Role-based (е.g. all Managers) or specific Users.

`AlertEvent` entity:
- AlertRuleId, OccurredAt, EntityType, EntityId, Severity, Title, Body, AcknowledgedBy, AcknowledgedAt, ResolvedAt.

Background worker (LON.Worker) evaluates rules every 5 min and writes AlertEvent rows.

UI entry points:
- `/admin/alert-rules` — define rules (Administrator only).
- `/management/alerts` — view + acknowledge events.
- Dashboard card surfaces unacknowledged criticals.

Pre-defined v1 alert rules:
- GuaranteeUtilization > 90%
- ClientOrder due in <7 days with <50% produced
- Machine down >2 hours
- Certification expiring in <30 days
- Receipt variance >5% (single event)
- Subcontractor late on PO milestone

#### §5.13.5 — AI assistant integration ✅ v1

AI helper floating button (§7.4) на dashboard prepares „daily briefing": natural-language summary of today's most-important events.

Example output:
> „Денес: 3 нарачки треба да се испратат до петок. Гаранцијата на одобрение #G-119 е на 87% — близу прагот. Machine M-12 имала 4h downtime вчера; PO-2026-042 е во ризик. Препорака: разговор со подизведувач Бета за приоритизација."

#### §5.13.6 — Export + scheduling ⚠ v1 manual, v1.1+ auto

*v1:* download buttons на секoj report (PDF / Excel / CSV).
*v1.1+:* Schedule recurring email of report („Monthly summary 1st of month to gazda@teksport.mk").

---

### §5.14 — Финансиски операции (Finance domain)

**Намера.** Финансиските aspekti на бизнисот — клиентски контракти, фактурирање за услуги на производство, цена на чинење, добавувачки фактури, маржа, готовински тек, payroll агрегација.

**Учесници.** Finance Clerk (главен), Manager (одобрува, прегледа margin), Customs Officer (FX rate за declarations).

**ELON-was.** Минимално покриено — фактурирање со надворешен систем; ELON немаше формално финансиско модулирање.

**Ограничување (важно).** LON **не е книговодствен систем**. LON ги пресметува часовите, маржата, цена на чинење — и испорачува CSV/XML на надворешен payroll/accounting систем. Без double-entry bookkeeping. Без journal entries. Без tax filings.

#### §5.14.1 — Client contracts + rate cards ✅ v1 (Phase 15 work)

`ClientContract` (entity exists):
- TenantId, Number, PartnerId (customer), ValidFrom, ValidTo, PaymentTermsDays, Currency, Notes.
- Status: Active | Expired | Cancelled.

`RateCardEntry` (entity exists):
- ContractId, RateType (PerPiece | PerMinute | PerOperation | Fixed), ItemId (nullable), OperationCode (nullable), RatePerUnit, Currency, ValidFrom, ValidTo.

Workflow:
- Customer signs production-services contract specifying prices per finished item or per operation.
- Active rate card consulted when ClientOrder created (auto-fill ClientOrderFinishedGood.UnitPriceForeign).
- Per-contract margin computation via cost accounting (§5.14.4).

UI entry points:
- `/finance/contracts` (постои — ClientContracts.tsx).
- Per-contract detail: rate card editor; linked ClientOrders count.
- Customer Partner detail → „Активен контракт" tab.

Contextual actions:
- Create contract | Update | Cancel | View rate history | Clone contract for renewal | View linked orders.

#### §5.14.2 — Sales invoicing ✅ v1

`Invoice` entity (postoi):
- TenantId, Number (SEQUENCE), ClientOrderId (FK), PartnerId (customer), IssueDate, DueDate.
- Status: Draft | Issued | Paid | Overdue | Cancelled.
- Currency, Subtotal, TaxAmount, TotalAmount.

`InvoiceLine`:
- InvoiceId, Description, ItemId (nullable), Quantity, UnitPrice, LineTotal.
- Linked to ProductionReceiptLine OR ShipmentLine (за audit).

Workflow:
- After Shipment (or per agreed cadence), Finance Clerk generates Invoice from ClientOrder.
- „Generate from PO" action — auto-populates lines based on confirmed FG receipts × rate card.
- Status transitions: Draft → Issued (PDF generated + sent) → Paid (manual mark or on payment received) → Overdue (auto, after DueDate).

UI entry points:
- `/finance/invoicing` (постои).
- ClientOrder hub → „Фактура" tab.

Contextual actions on Invoice detail:
- Issue (lock) | Send via email | Mark paid | Cancel | Re-issue (rare; copy) | Download PDF.

#### §5.14.3 — Supplier invoices (AP) ✅ v1 (Phase 16.C3.c)

`SupplierInvoice` (постои од Phase 16.C3.c):
- TenantId, Number (external; supplier's number), SupplierPartnerId, InvoiceDate, DueDate, Amount, Currency, Status (Open | Paid | Overdue | Cancelled), PaidDate.

Purpose: track payables to suppliers (utility, services, raw material outside LON, packaging, transport).

NOT linked to material receipts — those are governed by CustomsDeclaration. SupplierInvoice е стандардни AP records.

UI entry points:
- `/finance/ap` (постои).
- Supplier Partner detail → „Фактури" tab.

Contextual actions:
- Mark paid | Edit | Cancel | View AP aging report.

#### §5.14.4 — Cost accounting ✅ v1 (Phase 16.C3.a)

`CostRate` (постои од Phase 16.C3.a):
- TenantId, Scope (Machine | Operator | Shift | Operation), ScopeId (nullable Guid), CostPerHour, CostPerUnit, Currency, ValidFrom, ValidTo, Notes.

Used for computing cost-of-production per ClientOrder:
- Hours from OperationTimeLog × CostPerHour for involved (machine, operator, shift).
- Plus material cost from FakturaU5/CustomsDeclarationLine.UnitCost × consumed qty.
- Plus overhead allocation (post-v1 sophistication).

Margin per ClientOrder = Revenue (from Invoice) − Cost of production.

UI entry points:
- `/finance/cost-accounting` (постои).
- ClientOrder hub → „Маржа" widget.

#### §5.14.5 — Margin reporting ✅ v1

`/finance/margin` route — view margin breakdown:
- Per ClientOrder, per Customer, per FG item, per Period.
- Computed = Revenue − COGS (cost of goods sold, from §5.14.4).
- Filterable + exportable.

Cards: Gross Margin %, Net Margin (post-v1, after overhead), Top profitable customers.

#### §5.14.6 — Cash flow ⚠ v1 simple, v1.1+ predictive

*v1:*
- `/finance/cash-flow` (постои) — list of expected inflows (Invoice DueDate × Status=Issued) vs outflows (SupplierInvoice DueDate × Status=Open).
- Net cash forecast for next 30/60/90 days.

*v1.1+:*
- Probability-weighted forecast (e.g. customers paying late = adjust).
- What-if simulation („ако ja задоцниме Invoice X со 7 дена, како се менува cash?").

#### §5.14.7 — P&L preview ⚠ v1 simple

`/finance/pnl` (постои — PnLPreview.tsx):
- Revenue (sum of Invoices Issued in period).
- Direct costs (material + labor from cost accounting).
- Gross profit + margin %.
- Operating expenses (read-only — sum of SupplierInvoice по категорија).
- Operating profit.

Note: ова не е официјален P&L (тоа го прави сметководствениот софтвер); LON го дава како **preview** за management decisions.

#### §5.14.8 — FX rate maintenance ✅ v1

`FxRate` entity (Phase 17, ако не постои):
- TenantId, FromCurrency, ToCurrency, Rate, EffectiveDate, Source (Manual | Central Bank API).
- Used by: CustomsDeclaration line valuations, Invoice currency conversion, margin reports.

UI entry points:
- `/finance/fx-rates` (нов).
- Auto-import од national central bank API daily (v1.1+).
- Manual entry в v1 (Finance Clerk одговорен).

#### §5.14.9 — Payroll export (alias за §5.12.9)

PayrollPeriod (§5.12.9) се прикажува и под Finance navigation бидејќи го exporta Finance Clerk.

#### §5.14.10 — Currency considerations + audit

- Сите финансиски amounts чуваат explicit Currency.
- FX conversion at point-in-time (using rate effective on transaction date).
- AuditLogEntry recordira FX rate used for each conversion (for traceability).
- Margin reports: aggregate в Tenant.PrimaryCurrency (e.g. EUR for Teksport); per-row currency preserved.

#### §5.14.11 — Finance dashboard

`/finance/reports` hub (постои):
- Cards linking to invoicing, contracts, guarantees, margin, P&L, cash flow, cost accounting, AP, payroll.

**AI helper hooks (cross-cutting за §5.14):**
- Margin anomaly: „маржата на ClientOrder X е 4% наспроти просечни 18% за овој клиент — провери rate card или unexpected costs".
- Cash flow alert: „во следните 30 дена очекувани inflows €X, outflows €Y → нет −€Z. Препорака: интервенирај со 2 unpaid Invoices > 60 days late".
- Payment behavior: „клиент Z плаќа просечно 12 дена касно — следната Invoice издаде со shorter terms".

---

## §6 — Cross-cutting concerns

### §6.1 — Guarantee lifecycle

Целосен flow илустриран:

```
ClientOrder created
   └→ no guarantee impact (it's intent, not movement)
IM Declaration created (Draft/Submitted) → no impact
IM Declaration Approved (Zaverka)
   └→ event ⇒ GuaranteeLedgerEntry (Debit, amount=duty)
   └→ GuaranteeAccount.CurrentBalance += duty
   └→ Check: balance > authorization.GuaranteeAmount? Alert manager.
Material distributed (Podelba)
   └→ no guarantee impact (still on-hand)
Material issued to production
   └→ no guarantee impact (still legally on bond)
Production receipt
   └→ no guarantee impact (FG made, but not exported)
EX Declaration Approved (Zaverka)
   └→ event ⇒ Calculate consumed materials per FG shipped
   └→ GuaranteeLedgerEntry (Credit, amount=consumed_duty_pro_rata)
   └→ GuaranteeAccount.CurrentBalance -= credit
Waste/Return Declaration Approved
   └→ same pattern: credit pro-rata
Razdolzuvanje finale
   └→ Snapshot saved; per-line flag updated
```

**Invariant:** `LONAuthorization.GuaranteeAmount >= GuaranteeAccount.CurrentBalance >= 0` (modulo override permission).

### §6.2 — Inventory state machine (Proces)

Reused from `LonProcessState` enum. Transitions are domain events emitted by handlers (not direct SQL UPDATE). Full state graph in §3.4.

### §6.3 — Skart vs Otpad — clarification (legacy gotcha)

- **Skart** = defective incoming material on receive. Entity: `Skart`. Removed from inventory before production. Guarantee credit triggered separately (return-of-duty).
- **Otpad** = manufacturing by-product. Tracked in `BOMLineWasteOverrides` (4 slots: KolOtpad, KolOtpad1, KolOtpad2, KolZaguba). On production receipt, decremented from material balance pro-rata.

UI never mixes the two; separate entry points (`/warehouse/skart` for Skart; otpad is computed automatic from BOM during production).

### §6.4 — Average rate override (ProsecnaST)

`AverageRateOverride` (P15.17, exists). Per-declaration toggle: vbypass per-tariff lookups, use single fixed rate, VAT=0. Tracked in `CustomsDeclaration.UseAverageRate + AverageRate` fields.

### §6.5 — Audit trail policy

**Mandatory audit (created/updated/deleted with field-level changes):**
- All `IAuditable` entities (§3.7)
- All `GuaranteeLedgerEntry` writes (every single one; never bulk-edit without per-row audit)
- Status transitions of `ClientOrder`, `CustomsDeclaration`, `ProductionOrder`, `Shipment`

**Audit log retention:** 7 years (regulatory norm for customs).

**UI:** `/admin/audit-log` page with filters (entity type, entity ID, actor, date range, field name). Per-entity „Audit history" tab on detail pages.

**Implementation:** EF Core `SaveChangesInterceptor` captures pre/post values for `IAuditable` entities; writes `AuditLogEntry` rows. Done in `LON.Infrastructure/Persistence/Interceptors/AuditInterceptor.cs` (Phase 17 task).

### §6.6 — Numbering & concurrency

**SQL SEQUENCE objects** (atomic; multi-user safe):

```sql
CREATE SEQUENCE seq_ClientOrder_<tenantId> START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_CustomsDeclarationIM_<tenantId> START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_CustomsDeclarationEX_<tenantId> START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE seq_Receipt_<tenantId> ...
CREATE SEQUENCE seq_MaterialIssue_<tenantId> ...
CREATE SEQUENCE seq_Shipment_<tenantId> ...
CREATE SEQUENCE seq_ProductionOrder_<tenantId> ...
CREATE SEQUENCE seq_GuaranteeLedger_<tenantId> ...
```

**Format helpers** in `LON.Domain.Common.NumberFormatter`:

```csharp
ClientOrder:        $"CO-{year}-{seq:D6}"     // CO-2026-000042
ImportDecl:         $"IM-{year}-{seq:D6}"
ExportDecl:         $"EX-{year}-{seq:D6}"
Receipt:            $"RC-{year}-{seq:D6}"
MaterialIssue:      $"MI-{year}-{seq:D6}"
Shipment / Izpratnica: $"IS-{year}-{seq:D6}"
ProductionOrder:    $"PO-{year}-{seq:D6}"
```

Per-tenant prefix optional (`{tenant.Prefix}-CO-{year}-{seq}` ако корисникот сака multi-tenant identification во документи).

Number generation е во handler (од `NEXT VALUE FOR seq_...`) — никаков `MAX+1`.

### §6.7 — Soft-delete policy

Сите entities listed во §3.7 со `ISoftDeletable`. Global query filter `WHERE !IsDeleted` (EF Core). 

UI:
- Default views hide deleted.
- Admin: `/admin/recycle-bin` page lists soft-deleted entities per type with restore button.
- Retention: 90 days, then hard-delete via scheduled job (`LON.Worker`).

Cascade:
- Soft-delete ClientOrder → cascade soft-delete linked CustomsDeclarations, ProductionOrders, Shipments (with audit).
- Soft-delete Partner → block if referenced (cannot delete a customer with active orders).

### §6.8 — Multi-language (i18n)

v1: **MK + EN** only. SQ + SR post-v1.

- All user-facing strings via `t('key.path')`.
- Locales: `frontend/web/src/i18n/locales/{en,mk}.json`.
- Server messages: also i18n via `Accept-Language` header → `LON.API/Resources/{en,mk}.resx`.
- Date/number formatting: `Intl.DateTimeFormat` / `Intl.NumberFormat` with locale.
- Customs documents (PEE XML, Razdolzuvanje PDF) — fixed Macedonian (regulatory).

### §6.9 — Tenant isolation (RLS)

**SQL Server Row-Level Security (RLS)** policy enforces tenant scope at DB engine level:

```sql
CREATE FUNCTION dbo.fn_TenantPredicate(@TenantId UNIQUEIDENTIFIER)
RETURNS TABLE WITH SCHEMABINDING
AS RETURN SELECT 1 AS result
WHERE @TenantId = CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER)
   OR SESSION_CONTEXT(N'IsSystemAdmin') = 1;

CREATE SECURITY POLICY TenantIsolationPolicy
ADD FILTER PREDICATE dbo.fn_TenantPredicate(TenantId) ON dbo.ClientOrders,
ADD BLOCK PREDICATE dbo.fn_TenantPredicate(TenantId) ON dbo.ClientOrders AFTER INSERT
-- ... for every ITenantScoped table
WITH (STATE = ON);
```

ASP.NET Core middleware (Phase 17) reads JWT `tenant_id` claim, sets `SESSION_CONTEXT('TenantId', ...)` per request. EF Core query filter remains as defense-in-depth.

Tampered queries (e.g. `dbContext.ClientOrders.IgnoreQueryFilters()`) still blocked by DB engine. Penetration test in Phase 21.

### §6.10 — Backup & disaster recovery (v1 minimum)

- **Daily logical backup**: `BACKUP DATABASE Teksport TO DISK` automated via cron + scp to off-VPS storage.
- **Retention:** 30 days rolling.
- **Tested restore:** monthly drill — `RESTORE` to staging container, run reconciliation queries; SESSION_LOG entry confirming.
- **PITR:** Not v1 (SQL Server Express limitations). Post-v1 upgrade to Standard для full PITR.
- **VPS snapshot:** weekly Contabo snapshot (manual UI for v1; automate post-v1).

### §6.11 — Knowledge Base (RAG) operations

- Auto-index on document upload (regulations, customs procedures, internal SOPs).
- Manual chunking via `IDocumentChunkingService` (existing).
- Vector store: SQL Server table with vector column (existing).
- Embeddings: OpenAI ada-002 (existing).
- Used by AI helper (§7.4) and by `/knowledge-base` search/chat UI.

---

## §7 — UI principles

### §7.1 — Hub-and-spoke

**Принцип:** корисникот стартува секоja бизнис operacija од **една главна точка** на тој бизнис object. Не „odi на различни секции на сидебар и зашиј сите".

**Implementacija:**
- `/orders/:id` (ClientOrder hub) е централно место.
- Sidebar постои но *deemphasized*: главно навигација „по тип на објект" — листа кратенки, не главна работа површина.
- Од hub: action launcher (sticky right panel) со ВСИ relevant акции за тој ClientOrder во неговиот текoвен state.
- Враќаењето кон hub-от е always-available (breadcrumb + hard navigation key).

### §7.2 — Contextual actions everywhere

Секоја detail page (CustomsDeclaration, ProductionOrder, Shipment, etc.) има своj action panel со relevant operations.

Пример: на CustomsDeclaration IM detail:
- „Approve (Zaverka)" — ако status=Submitted
- „Receive into warehouse" — ако status=Approved и не сите lines се primljeni
- „Export to Spediter file" — ако status=Approved
- „View parent ClientOrder" — back-link
- „Audit history"

Никогаш „Search for the related ClientOrder, open its hub, then come back"; if you're on a declaration, you can do what you'd want to do with it.

### §7.3 — Smart prefill & suggestions

Legacy ELON's biggest productivity win was template auto-apply (NormativTemplO/S). LON extends this:

- New BOM for Item X → suggest most-used BOM for Item X across past ClientOrders.
- New CustomsDeclaration line → suggest TariffCode + Country based on item history.
- New ProductionOrder → suggest Routing based on Item's past production records.
- Material allocation: suggest producer based on historical performance + capacity.

Implementация: `LON.Application.Suggestions/SuggestionsService.cs` (Phase 17). Returns ranked suggestions; UI shows top 3 with confidence.

#### §7.3.1 — „Sticky values" pattern (in-document memory)

Legacy ELON имаше module-global variables (`sValuta`, `sLastEdMer`...) што го памтеа последниот внесен value и го авто-prefill-ираа во следен ред. Тоа е голема UX победа за repetitive data entry (10 lines на иста декларација често имаат иста валута, UoM, country of origin).

LON го имплементира како **document-scoped React state**, не globally:

```typescript
// pattern: per-document sticky fields
const stickyDefaults = useStickyDefaults<DeclarationLine>('declarationLines', {
  Currency: partner?.primaryCurrency ?? 'EUR',
  UoM: undefined,
  CountryOfOrigin: undefined,
  TariffCode: undefined,
});
// When user creates a new line, prefill from sticky.
// When user edits a line's Currency, update sticky for next line.
```

Sticky fields per entity:
- **CustomsDeclarationLine**: Currency, UoM, CountryOfOrigin, TariffCode.
- **BOMLine**: Material UoM, Waste %.
- **ReceiptLine**: Location, QualityStatus.
- **ProductionOrderMaterial**: Source location.

**Bulk override toolbar** на секoja line-form: „🔄 Смени [field] на сите редови" → confirm dialog → update + audit log. За currency конкретно: dialog warns „Промена на валута ке ja recalculate-ира Vrednost според FX rate".

UI placement: sticky pattern автоматска (нема user toggle); bulk action explicit (button во table toolbar).

**Reality-check (2026-05-12 PREP):** TEKSPORT ELON snapshot има 43,223 EUR lines од 43,224 (99.998%). Currency bulk-change е degenerate use case за TEKSPORT — sticky-default-от за currency прави единствено „pre-fill EUR every time". Primary value на pattern-от е за **UoM / CountryOfOrigin / TariffCode** (значителна variance). Currency останува покриен од истата инфраструктура „безмала бесплатно", но не е showcase. Pattern не се rescope-ира; expectation за demos и user-training се прави.

### §7.4 — AI assistant (RAG свртен кон корисник)

**Where it lives:** floating button (bottom-right) on every page. Click → side panel.

**Two modes:**

1. **Контекстни препораки** (passive): panel auto-loads recommendations based on current page state.
   - On ClientOrder hub: „You have 245m² received but not distributed; FG-7 needs 200m² — consider Podelba to Producer Бета."
   - On Receipt creation: „Similar receipts from this supplier had 2% variance; you're at 5% — check packaging."
   - On Razdolzuvanje pre-flight: „2 IM lines have no matching EX consumption — normal?"

2. **Free-form Q&A** (active): chat box. User types „кој BOM беше последен пат користен за FG-7?" → LLM queries RAG over: knowledge base + recent ClientOrders/BOMs/etc.

**v1 scope (3 core recommendations):**
- ClientOrder hub: detect blocked next step.
- Receipt: flag variance.
- Razdolzuvanje: pre-flight reconciliation check.

**Out-of-v1:**
- Anomaly detection across many entities.
- Predictive ETA modeling.
- Auto-classification of incoming Excel imports.

**Implementation:**
- `LON.Application.Ai.AiAssistantService.cs` — accepts context (entity type, entity ID, current state).
- Calls OpenAI chat completion с function calling (RAG retrieval as one tool).
- Returns recommendations со confidence + structured action links (deep-link back to UI route).
- Server-side caching (per-entity-state) — recompute on event.

### §7.5 — Forms

- Required: `react-hook-form` for any form with 3+ fields.
- Validation: Zod schemas (или yup) shared between client + server (post-v1; v1 = duplicate validation).
- Pattern: `FormDialog` component (post-v1 polish: full-page form on mobile).
- File uploads: `<FileUpload>` component (Phase 17 — handles Excel/PDF/image).

### §7.6 — Tables

- Required: `components/common/DataTable.tsx` (hardened in Phase 16.B2).
- No handcrafted `<table>` markup for new pages.
- Server-side sort/paginate for >100 row tables.
- Export buttons: CSV (built-in), Excel (uses SheetJS), PDF (jsPDF for simple; server-render for complex).

### §7.7 — Mobile-friendly (responsive)

v1 = responsive web (Bootstrap-y breakpoints) for desktop + tablet. Магационер скенира со tablet за барcode (browser-based). Mobile-only flows (phone-sized) = post-v1 via Flutter.

---

## §8 — Tech architecture details

### §8.1 — Multi-tenant data model (with RLS)

Все ITenantScoped entities имаат `TenantId UNIQUEIDENTIFIER NOT NULL`. RLS predicate (§6.9) applied at table level. Per-request middleware sets `SESSION_CONTEXT('TenantId', ...)` from JWT.

Tenant provisioning:
- New tenant → run `Migrate.cs` for new schema + seed defaults (roles, code lists, SEQUENCEs).
- Tenant offboarding (post-v1): archive + soft-delete + retention purge.

### §8.2 — Auth + RBAC

- JWT (HS256) → claims: `sub`, `tenant_id`, `role[]`, `external_partner_id?`, `permissions[]`.
- Refresh token rotation (existing).
- Permissions are role-derived но cacheable per-token (compiled).
- `[HasPermission("EditGuaranteeAmount")]` attribute on controllers (Phase 17).

### §8.3 — API design

- REST, resource-oriented (`/api/ClientOrders`, `/api/CustomsDeclarations`, etc.).
- OpenAPI spec generated, `scripts/gen-api-types.sh` produces TS types.
- Pagination: `?page=&pageSize=` (offset) + `?cursor=` (cursor-based for big tables).
- Sort/filter: `?sort=field,-desc&filter[status]=Active`.
- Versioning: header-based (`X-API-Version: 1`); breaking changes bump version (post-v1 concern).

### §8.4 — Frontend stack

- React 18 + TypeScript 4.9 (upgrade to TS 5 in Phase 17 if compatible).
- MUI v5 + theme (Phase 16.B3).
- @tanstack/react-query (Phase 16.B1).
- react-hook-form + Zod.
- react-router 6.
- i18next (existing).
- recharts (existing; for dashboards).
- Single source of API client: `frontend/web/src/api/*` (auto-generated from OpenAPI in Phase 18+).

### §8.5 — Testing strategy

**Pyramid:**

- **Unit (many, fast)**: `frontend/web/src/**/*.test.tsx` (Jest + React Testing Library). Server side: `LON.Application/**/*.Tests.cs` (xUnit, in-memory).
- **Integration (some)**: `tests/LON.IntegrationTests/` — xUnit + LonApiFactory + Testcontainers SQL Server. Tests handlers + endpoints with real DB.
- **E2E (few)**: `tests/LON.E2ETests/` — Playwright Test. Covers user flows end-to-end (login → click through hub → produce result).

**E2E test scope for v1:**

1. **Auth & RBAC** — login as each role, assert visible nav + permissions.
2. **Closed loop flow** (the v1 acceptance criterion):
   - Create ClientOrder
   - Create IM declaration + lines
   - Approve declaration (Zaverka)
   - Verify guarantee debit
   - Receive into warehouse
   - Create BOM + ProductionOrder
   - Podelba to producer
   - Material issue
   - Production receipt
   - QC pass
   - EX declaration + shipment
   - Approve EX (Zaverka)
   - Verify guarantee credit
   - Razdolzuvanje view shows balance reconciled
3. **Tenant isolation** — login as TenantA, verify TenantB data invisible (via direct API + UI).
4. **Audit trail** — make change, verify audit log captures it.
5. **AI helper** — verify 3 core recommendations render correctly on respective pages.

Playwright config in `playwright.config.ts` at repo root. CI: nightly + on PR.

---

## §9 — Data import / export

### §9.1 — ELON migration runbook

**Goal:** transfer Teksport's full production ELON DB to LON Teksport DB without data loss, within one business day downtime.

**Phases:**

1. **Z2779 happy-path import** (Phase 17.PRE.7). Single canonical Zaklucok (1 IM → 13 import lines → 5-line BOM → 1 Izdatnica → fully razdolzeno) used as fixture to validate the full mapping end-to-end **before scaling**. Z2802 is the multi-producer stress test; Z2780 the daily smoke.
2. **Dry-run** (Phase 21.1). Run `LON.Migration` against staging copy of ELON DB for **all 269 non-staging Zaklucoci**. Document every error, every missing FK, every unmapped record. Tolerate 0 errors before progressing.
3. **Reconciliation queries** (Phase 21.1, six required):
   - **R1** Record counts per Proces in ELON `LagerMaterijali` vs. `InventoryBalance` + `InventoryMovement` after migration. Tolerance 0.01%.
   - **R2** `SUM(GarancijaIznos)` per `Odobrenija` vs. `SUM(GuaranteeAccount.CurrentBalance)` per `LONAuthorization`. Tolerance exact (currency-aware).
   - **R3** `SUM(Vrednost)`, `SUM(Davacki)`, `SUM(Carina)` per declaration (10 random spot-checks).
   - **R4** Count `Zaklucoci` (non-staging `<> '00000'`) vs. `ClientOrders`.
   - **R5** Count `Normativi` vs. `BOMLines`.
   - **R6** **`NaimU5` aggregate check**: re-aggregate `FakturiU5` lines grouped by `(TarBr, EdMer, ZemjaPoteklo, OdobrenieRBr)` and assert SUM matches legacy `NaimU5` rows. (LON has no `NaimU5` table — computed view at query time.)
4. **Iteration**: Fix every gap in `LON.Migration`. Re-run. Until all six reconciliation queries pass.
5. **Cutover plan** (Phase 21.2):
   - T-7 days: final dry-run reconciliation; approve plan.
   - T-1 day: freeze ELON inserts (read-only).
   - T-0 morning: full export ELON → import LON. 4-8h estimated.
   - T-0 afternoon: spot-check 20 random ClientOrders end-to-end; user acceptance.
   - T-0 evening: go-live LON; ELON archived (read-only access maintained for 1 year).

**Critical mappings (corrected 2026-05-12 after Phase 17.PREP recon):**

| ELON | LON | Notes |
|---|---|---|
| `Odobrenija` | `LONAuthorization` | |
| `Zaklucoci` (non-staging `ZaklucokBroj <> '00000'`) | `ClientOrder` (`OrderNumber = CO-<year>-<6-digit-seq>`) | Synthesize ClientOrderNumber via SQL SEQUENCE; preserve legacy `ZaklucokBroj` in `ClientOrder.CustomerOrderReference` for traceability. |
| `Zaklucoci` (staging `00000`) | **— SKIP —** | Local DB has 0 staging rows; prod may have leftover staging; staging is by definition pre-finalized work-in-progress. |
| `FakturiU5Z` + `FakturiU5` | `CustomsDeclaration` + `CustomsDeclarationLine` | |
| `NaimU5` | **— NOT a table; computed view** | Reconciliation query R6 must re-aggregate `FakturiU5` lines and SUM-match legacy. |
| `LagerMaterijali` | `InventoryBalance` + `InventoryMovement` (per row → 1 InventoryMovement; balances recomputed from MovementType sequences) | Proces-aware mapping required (see DocumentSource resolver below). |
| `Garancija` (scalar on `Odobrenija`) | `LONAuthorization.GuaranteeAmount` + `GuaranteeAccount` (computed `CurrentBalance` via ledger) | |
| Razdolzuvanje (implicit per declaration line) | `GuaranteeLedgerEntry` rows (Credit per shipment-approval / waste / return) | |
| `GotoviProizvodi` | `Item` (type=Finished) + `ClientOrderFinishedGood` | |
| `Normativi` | `BOM` + `BOMLine` | |
| `NormativiVelicini` | `ProductionOrderMaterialSize` | Empty in local DB (0 rows); keep entity for prod migration. |
| ~~`GotoviProizvodi.Proizvoditeli` (comma-text)~~ → `LagerMaterijali.Proizvoditel` (numeric ID) | `Partner` with type=Producer | **Correction**: `GotoviProizvodi.Proizvoditeli` is NULL on every Z2779/Z2802/Z2780 candidate. True producer attribution is per-movement on `LagerMaterijali.Proizvoditel` (small int) and on `Ispratnici.Proizvoditel`/`Izdatnici.Proizvoditel`. Migration must build `Partner` (type=Producer) catalogue from the **union of distinct movement-row `Proizvoditel` values**, not from `GotoviProizvodi`. |
| `Ispratnici` (only when sourced from `Proces=9` rows) | `WasteDeclaration` + `WasteDeclarationLine` (destruction certificate) | **Correction**: research labeled `Proces=7` as "EX shipment via Ispratnica" — wrong. Real `Ispratnici` count = 776, but match-rate vs `Proces=7 LagerMaterijali.DokRBr` is only 12% (RBr coincidence); match-rate vs `Proces=9` is 100%. `Ispratnici` is **destruction documentation**, not export shipment. |
| `Izdatnici` (sourced from `Proces=7` rows + return-receipts from `Proces=8`) | `Shipment` (Type=ToProducer) + `ShipmentLine` | **Correction**: `Proces=7` is "exit to producer via Izdatnica" — match-rate 99%. `Izdatnici` count = 1,119. The Shipment entity covers both ToProducer and ToCustomer; type discriminator chosen by `Proizvoditel != NULL`. |
| `FakturiU5Skart` | `Skart` | Table absent in local TEKSPORT slice; verify presence in prod ELON. |
| `KnigaNai` | `TariffCode` + `TariffCodeRate` (year-rows, not year-columns) | **Table absent in local TEKSPORT slice.** Distinct `TarBr` values in `FakturiU5` = 147 — minimum codelist subset. Full KnigaNai (~9k rows) must be exported from Teksport prod for Phase 21. |
| `Aneksi` (year columns) | `TariffCodeRate` (one row per (code, year)) | **Table absent in local TEKSPORT slice** — needs prod export. |
| `Preferencijal` | `CodeListItem` (PreferentialOrigin) | **Table absent in local TEKSPORT slice** — needs prod export. |
| `tblArtikli` | `Item` (type derived from `ArtKatTip` flag) | Total 11,114 (Materials 8,960 / Finished 2,154); 80% archived — migration carries archived as `IsActive=false`. |
| `tblArtikli.ArtOtpadProc` (inflate-for-waste) | `Item.WasteSlots` / tenant-policy feature flag | **Correction**: only 4 articles out of 8,960 have non-zero `ArtOtpadProc` (max 2%). Inflate-for-waste кеп як feature flag (`Tenant.InflateForWasteEnabled`), default **OFF** for new tenants. Migration sets `true` on TEKSPORT to preserve legacy behavior; other migrated tenants default false. |
| `tblFirmi` | `Partner` | **Table absent in local TEKSPORT slice** — needs prod export. |
| `tblKorisnik<TenantName>` (per-tenant) | `Employee` + `User` | **Table absent in local TEKSPORT slice** + **D6=prod-export approved 2026-05-12**: Phase 21 cutover plan must include `tblKorisnikTEKSPORT` export from prod ELON. `FakturiU5Z.User` + `LagerMaterijali.User` (small-int FKs 0–8) resolved via that export. Phase 17.PRE.7 Z2779 happy-path uses placeholder `migrated-elon-bulk` user for created-by attribution (real users joined in Phase 21). |
| `tblIzvozniFakturi` + `Stavki` (3,239 headers + 57,857 lines) | `CommercialInvoice` + `CommercialInvoiceLine` (NEW v1 entity, BLUEPRINT §3.2.1) | **D4=new entity approved 2026-05-12.** Distinct from sales `Invoice` (§5.14.2 = Teksport invoicing customer for processing). `CommercialInvoice` е царински документ на consignor/consignee level со declared trade value of FG at border. Built in Phase 17 §E8.5. Finance integration (margin reconciliation) deferred to Phase 27. |
| `Propratnici` + `PropratniciStavki` (1,658 headers + 295,918 lines) | `DeliveryNote` + `DeliveryNoteLine` (NEW v1 entity, BLUEPRINT §3.8) | **D5=new entity approved 2026-05-12.** Polymorphic (`DocumentType`): ProducerDispatch (paired w/ MaterialIssue) / ProducerReturn (paired w/ Shipment) / CustomerShipment (paired w/ Shipment Export). Auto-generated on related-doc commit. Built in Phase 17 §E6.5. |

**DocumentSource resolver (new — keys on `Proces`):**

| Proces | Movement type | DokRBr → resolves to | Match rate in local ELON |
|---|---|---|---|
| 1 | Receipt (stock-on-hand) | `null` (no exit doc) | 294,288 rows; no DokRBr expected |
| 6 | In-house adjustment (rare) | `null` | 192 rows; treat as adjustment |
| **7** | Exit to producer | `Izdatnici.RBr` | **99%** (294,332 / 298,056) — primary path |
| 8 | Return from producer | `Izdatnici.RBr` (return voucher) | Partial (265 / 2,071); orphans → quarantine |
| **9** | Waste destruction | `Ispratnici.RBr` | **100%** (166,038 / 166,038) |

Migration code must implement a `ResolveExitDocument(LagerMaterijaliRow row)` switch on `Proces`, not a single Ispratnici lookup as the prep notes implied.

**Single-tenant local ELON DB caveat:** Local ELON is a TEKSPORT-only slice (31 tables, vs. the full ~501-table production ELON). `LagerMaterijali.Uvoznik` is NULL across all 760,645 rows — tenant discriminator was extracted to the database name itself ("ELON" = TEKSPORT-only). Multi-tenant production ELON uses `Uvoznik` as the discriminator. Migration code must check both.

**Currencies in local snapshot:** EUR 43,223 lines + NULL 1 — effectively single-currency. Multi-currency support is required (per BLUEPRINT §5.2) but reconciliation for TEKSPORT specifically can assume EUR. Other migrated tenants may have MKD/USD/RSD lines.

**Out of scope of migration:** historical reports (one-shot regenerate post-migration); employee attendance history (start fresh).

**Out-of-BLUEPRINT entities flagged for decision (Phase 17.PRE.3):** `tblIzvozniFakturi`, `Propratnici`, `PropratniciStavki`. See `docs/migration/MAPPING.md` (PRE.4) for the authoritative tracking.

### §9.2 — KW12 wizard (existing, polish in Phase 17)

Excel/CSV import for customer files (TEKSPORT KW12 format, etc.). Maps source columns → CustomsDeclarationLine fields. Mapping profiles persistent (`ImportMappingProfile` entity). Dry-run preview before commit.

### §9.3 — Speditor export

Per-Speditor profile (Phase 19) defines column mapping + format. v1 hardcoded one profile (the most-used). Output: download CSV / Excel / XML file.

### §9.4 — PEE XML to customs (manual download for v1)

Endpoints `/api/Customs/declarations/{id}/pee/{envelope}` already exist (P15.12+). User clicks „Download PEE060" → file saved to disk → user manually uploads to government customs portal.

Auto-submit (SOAP/REST integration with customs system) — post-v1.

### §9.5 — Future imports / exports (out of v1)

- ECD auto-pull (current `frmTransferECD` analog) — post-v1.
- E-invoicing — post-v1.
- API for external speditori (programmatic dispatch queue access) — post-v1.

---

## §10 — Roadmap to v1

**Six phases.** Each phase has explicit Done definition. Phase 16 is in progress (cleanup foundation). Phases 17–21 build the closed loop. Phase 22 is hardening to launch.

### Phase 16 — Cleanup + UI foundation *(in progress)*

> Detailed prompts: `AGENT-PROMPTS.md` §A–§D.

- 16.A Cleanup: dead routes, lying navGroups, audit MasterData dupes
- 16.B UI foundations: react-query, DataTable hardening, PageShell + MUI theme
- 16.C localStorage→backend: RiskRegisterItem, EmployeeCertification, CostRate/PayrollPeriod/SupplierInvoice
- 16.D Test gap fill: WMSController tests, RBAC matrix, MasterData CRUD

**Done definition:** `tsc` 0 errors, ESLint 0 warnings, no localStorage business data, all 174 routes covered by tests, VPS smoke for changed pages.

### Phase 17 — ClientOrder hub + flow wiring + AI helper minimum

> Promтови: AGENT-PROMPTS.md §E1–§E9 (added in this turn).

- 17.1 New `ClientOrder` entity + migrations + handlers + endpoints (BLUEPRINT §3.1)
- 17.2 ClientOrder hub page `/orders` + detail `/orders/:id` (BLUEPRINT §5.1, §7.1)
- 17.3 Wire IM declaration creation from hub (BLUEPRINT §5.2)
- 17.4 Wire Receipt from hub (BLUEPRINT §5.3)
- 17.5 Wire BOM + ProductionOrder from hub (BLUEPRINT §5.4)
- 17.6 Wire Podelba from hub (BLUEPRINT §5.6)
- 17.7 Wire MaterialIssue + ProductionReceipt from hub (BLUEPRINT §5.7, §5.8)
- 17.8 Wire EX declaration + Shipment from hub (BLUEPRINT §5.10)
- 17.9 Wire Razdolzuvanje view (BLUEPRINT §5.11)
- 17.10 AI helper service + 3 core recommendations (BLUEPRINT §7.4)
- 17.11 Domain events + handlers (guarantee ledger entries on declaration approval, status transitions) (BLUEPRINT §3.6, §6.1)
- 17.12 SQL SEQUENCE objects + NumberFormatter (BLUEPRINT §6.6)
- 17.13 Audit interceptor + AuditLogEntry writes (BLUEPRINT §6.5)
- 17.14 Soft-delete global filter + recycle bin UI (BLUEPRINT §6.7)

**Done definition:** одна реална ClientOrder може да помине целиот flow (од §5.1 до §5.11) end-to-end локално, на VPS. E2E Playwright test (single happy path) passes.

### Phase 18 — Subcontractor login + role

- 18.1 Subcontractor role seeded
- 18.2 JWT claims include `external_partner_id`
- 18.3 Server-side filter: `WHERE producer.Id = claims.external_partner_id` for all queries returning data to subcontractor
- 18.4 Subcontractor view: minimal dashboard (their POs, their materials, their pending issues)
- 18.5 RLS policy extended (если потребно)
- 18.6 Integration tests for subcontractor role isolation

**Done definition:** subcontractor login на VPS, гледа само своите налози; не може да види туѓи.

### Phase 19 — Speditor role + export polish

- 19.1 Speditor role + JWT claim
- 19.2 SpeditorExportProfile entity + UI
- 19.3 Speditor view: their assigned shipments (post-shipment, document download)
- 19.4 Email notification (post-shipment) — automatic email to speditor (optional v1)

**Done definition:** spend ETA на shipment се испрака на speditor email; speditor може да дојде на login и да download-ира документи.

### Phase 20 — RLS + data-level isolation + tenant security audit

- 20.1 SQL Server RLS policy applied to all ITenantScoped tables
- 20.2 Middleware: SESSION_CONTEXT setup per request
- 20.3 Penetration test: tampered JWT → confirm DB engine blocks
- 20.4 SecurityAudit document + remediation
- 20.5 Backup automation + first restore drill

**Done definition:** documented pen test passes; daily backup runs unattended; restore drill report submitted.

### Phase 21 — ELON migration polish + production hardening + launch prep

- 21.1 ELON migration dry-run loop (until reconciliation 100%)
- 21.2 USER_MANUAL.md updated to реflect ClientOrder hub flow
- 21.3 Onboarding video / walkthrough (optional but recommended)
- 21.4 Cutover plan dry-run on staging
- 21.5 Go-live checklist (final verification gates)
- 21.6 Phase 22+ post-v1 backlog formalized

**Done definition:** Cutover runbook approved; cutover dry-run on staging succeeds; user signs off.

### v1 = end of Phase 21.

---

## §11 — Open questions / out-of-v1 scope

### Resolved (decisions captured in BLUEPRINT)

- ✅ v1 = minimum closed loop. (§1.3)
- ✅ AI helper в v1, со 3 core recommendations. (§7.4)
- ✅ Tenant isolation = RLS. (§6.9)
- ✅ Tenant = производител. (§1.1, §4)
- ✅ Subcontractor + Speditor = users post-Phase 18/19.
- ✅ PEE XML = manual download за v1; auto-submit post-v1.
- ✅ Languages в v1: MK + EN; SQ + SR post-v1.
- ✅ **Q11.1.** ClientOrder edit cascade: **edit-in-place** додека status ∈ {Draft, Active} без linked Shipments. Откако shipments се поднесени → ClientOrder влегува во „soft-locked" mode (само Cancel + Notes; structural edits бараат explicit Administrator override со reason). Cascade на linked entities: декларациите остануваат непроменети; ако ClientOrder се откаже → linked declarations остануваат во нивниот тековен status (не auto-cancel) — корисникот мора одделно да ги cancel-ира или да ги релинкува на нов ClientOrder.
- ✅ **Q11.2.** Guarantee ceiling override: **само `Administrator` role**. Manager не може да override; може само да побара од Administrator. Override action логиран во AuditLogEntry со mandatory `Reason` field (free-text + дропдаун со предефинирани причини: „одлука на менаџмент", „привремена ситуација", „исправка на грешка").
- ✅ **Q11.3.** Currency: **per-line**, default од `Partner.PrimaryCurrency` (или EUR ако partner не е поставен). UX олеснувања (BLUEPRINT §7.3.1 за detail):
  - Кога корисникот внесува нов ред мануелно, валутата се **auto-prefill-ува од последниот внесен ред** во истиот документ (mirroring legacy ELON `sValuta` pattern).
  - Toolbar action „🔄 Смени валута на цел документ" — bulk-update со confirmation и audit log запис.
  - Сличен auto-prefill pattern важи и за UoM, CountryOfOrigin, TariffCode по item history (BLUEPRINT §7.3).

### Out of v1 (confirmed deferral, spec exists in BLUEPRINT, code may exist, не активирано)

**Production tracking (§5.8 expansion — v1 minimal stubs, v1.1+ full):**
- Real-time operator UI for OperationTimeLog (start/pause/finish buttons). v1 = manual planner entry only.
- IoT integration за MachineStateEvent auto-capture. v1 = manual entry only.
- Predictive maintenance suggestions via RAG. v1 = no.
- OperatorPerformance reports + ranking. v1 = no.
- Real-time OEE dashboards per WorkCenter. v1 = no.
- Photographic evidence per ScrapEvent. v1 = no (text only).
- Weight-station integration for waste. v1 = no.
- Bulk scrap event capture (CSV import). v1 = single-event only.

**Production planning + scheduling (§5.8.9):**
- Full scheduling engine (Gantt UI, OR-Tools solver, drag-and-drop). v1 = simple capacity check advisory only.
- Constraint-based reschedule on disruption. v1 = manual.
- Big-board kiosk display (§5.8.11). v1 = `/production/today` web view only.

**HR (§5.12 expansion):**
- RFID/card readers for attendance. v1 = manual + simple kiosk.
- Mobile clock-in with geolocation. v1 = no.
- ShiftSwap formal request workflow. v1 = manual edit by HR Manager.
- EmployeePerformanceReview entity + workflow. v1 = read-only aggregated metrics.
- Configurable certification → machine binding (block assignment without cert). v1 = warning only.
- Salary calculation (LON never does this; out-of-LON forever).

**Management Reporting (§5.13 expansion):**
- Recurring email scheduling for reports. v1 = manual export.
- Configurable AlertRule UI beyond predefined set. v1 = predefined list only, no SQL editor.
- Push notifications for alerts. v1 = email + dashboard only.
- AI daily briefing (§5.13.5) extended with predictive insights. v1 = current-state summary only.

**Finance (§5.14 expansion):**
- Auto FX rate import from national bank API. v1 = manual.
- What-if cash flow simulation. v1 = static forecast only.
- Cost allocation including overhead. v1 = direct costs only.
- AP aging full report with reminder workflow. v1 = simple list view.
- Integration with external accounting system (CSV/XML export beyond Payroll). v1 = manual export of specific reports.

**QC + Packaging (§5.9 expansion):**
- PackList entity (multi-box, mixed-content, label-printing). v1 = simple metadata fields on ShipmentLine.
- Mobile scanner workflow for packaging verification. v1 = no.
- Defect analytics (per-producer, per-item, trend). v1 = single inspection record only.
- Photo upload for defects. v1 = URL field placeholder.

**Mobile (Flutter):**
- Warehouse operator app (barcode scan receive/transfer/skart).
- Production operator app (start/pause/finish operation, scrap entry).
- QC inspector app (defect capture + photo).
- Producer-side mobile (subcontractor).

**Customs / integration:**
- Auto-submit PEE XML to government customs system.
- Full ECD auto-pull integration.
- E-invoicing.

**Multi-tenant / scale:**
- Multi-tenant deployment beyond Teksport (capability exists; second tenant onboarded post-v1).
- BOM versioning (currently soft-replace со audit log).

**AI helper extensions:**
- Anomaly detection across many entities at once.
- Predictive ETA modeling.
- Auto-classification of incoming Excel imports (smart mapping suggestions exist; full automation post-v1).
- Operator performance anomaly alerts.
- Defect pattern alerts.

### Confirmed never (out of LON forever)

- Financial bookkeeping (LON exports to external system).
- HR full lifecycle.
- E-commerce / client portal.
- CMMS (maintenance management beyond „mark machine down").

---

## §12 — Document maintenance

- BLUEPRINT changes must be made via explicit session with user sign-off.
- Per-phase progress: status update in `PLAN.md`, not BLUEPRINT.
- New decisions during a phase: log in SESSION_LOG; promote to BLUEPRINT in next review cycle (typically end-of-phase).
- BLUEPRINT version note: increment on substantial change; minor edits (typo, clarification) noted in commit message.

---

*End of BLUEPRINT.md v1 (2026-05-11)*
