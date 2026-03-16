# LON - Application Blueprint

## Што е ова?

**LON** е enterprise систем за управување со **увоз за облагородување** (Inward Processing) — царинска постапка каде фирма увезува суровини без плаќање давачки, ги преработува во готови производи, и ги извезува. Системот ги покрива сите аспекти: од царинска декларација, преку складиште и производство, до раздолжување на гаранции и полна трасирање.

**Домен:** Производствена фирма во Македонија што работи со царински процедури 42 00 (одложено плаќање) и 51 00 (враќање давачки).

---

## Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND                                 │
│  ┌──────────────────────┐    ┌────────────────────────────┐     │
│  │   React + TypeScript │    │   Flutter (Mobile)          │     │
│  │   Web Dashboard      │    │   Scan-first, Offline-first │     │
│  └──────────┬───────────┘    └─────────────┬──────────────┘     │
└─────────────┼──────────────────────────────┼────────────────────┘
              │ REST API                     │
┌─────────────▼──────────────────────────────▼────────────────────┐
│                    LON.API (.NET 8)                              │
│  ASP.NET Core Web API + JWT Auth + Swagger                      │
│  15 Controllers │ MediatR (CQRS) │ Health Checks                │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│                    LON.Application                               │
│  Commands (CreateReceipt, CreateDeclaration, DebitGuarantee...) │
│  Queries (ValidateDeclaration)                                  │
│  Validation Engine (Rule-based customs validation)              │
│  Knowledge Base Services (RAG, Embeddings, Vector Store)        │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│                    LON.Domain                                    │
│  6 Domain Modules │ 40+ Entities │ Domain Events │ Enums        │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│                    LON.Infrastructure                            │
│  EF Core (Code First) │ SQL Server │ Seed Data │ Auth Service   │
│  OpenAI RAG │ Vector Store │ Background Services                │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────┐    ┌──────────────────────────────────┐
│  SQL Server 2022        │    │  LON.Worker                       │
│  LONDB                  │    │  BackgroundService                │
│                         │    │  Event Processing                 │
│                         │    │  Outbox Pattern                   │
└─────────────────────────┘    └──────────────────────────────────┘
```

---

## Домен модули (6 столба)

### A) Master Data — Основни податоци
**Цел:** Централен регистер на сите ентитети во системот.

| Ентитет | Опис | Клучни полиња |
|---------|------|---------------|
| `Item` | Артикл (суровина, полупроизвод, готов производ, амбалажа) | Code, Type, HSCode, CountryOfOrigin, IsBatchTracked, IsMRNTracked |
| `UnitOfMeasure` | Мерна единица | Code, Name, Symbol |
| `ItemUoMConversion` | Конверзија меѓу мерни единици | FromUoM → ToUoM, ConversionFactor |
| `Warehouse` | Складиште | Code, Name, Address |
| `Location` | Локација (bin level) | Aisle/Rack/Shelf/Bin, Type (Receiving/Storage/Picking/Production/Shipping/Quarantine/Blocked), MaxCapacity |
| `Partner` | Деловен партнер | Type (Supplier/Customer/Carrier/CustomsBroker/Bank), TaxNumber, Country |
| `Employee` | Вработен | EmployeeNumber, Department, Position, Shift, User link |
| `User` | Корисник (login) | Username, PasswordHash, Roles, RefreshToken |
| `Role` / `Permission` | RBAC систем | Role → Permissions (по категории: MasterData, Production, Customs, WMS...) |
| `Shift` | Работна смена | StartTime, EndTime |
| `WorkCenter` | Работен центар | StandardCostPerHour, Capacity |
| `Machine` | Машина | SerialNumber, WorkCenter link |

### B) WMS — Warehouse Management System
**Цел:** Целосно управување со складишни операции, без негативен залиха, без движење без локација.

| Ентитет | Опис | Клучно |
|---------|------|--------|
| `Receipt` + `ReceiptLine` | Прием на стока | Partner, Warehouse, BatchNumber, MRN, QualityStatus |
| `InventoryBalance` | Салдо = Item + Batch + MRN + Location | Quantity (no negative!), QualityStatus |
| `InventoryMovement` | Секое движење на залиха | Type (Receipt/Issue/Transfer/Adjustment/ProductionReceipt/ProductionIssue/Shipment/Return) |
| `Transfer` + `TransferLine` | Интерен трансфер | FromLocation → ToLocation, Batch, MRN |
| `CycleCount` + `CycleCountLine` | Циклична инвентура | SystemQuantity vs CountedQuantity, Variance |
| `PickingWave` | Бранови за комисионирање | Warehouse, PickTasks |
| `PickTask` | Задача за пикирање | Item, Location, Batch, MRN, AssignedToEmployee, Status |
| `Shipment` + `ShipmentLine` | Испорака | Customer, Carrier, TrackingNumber, Status (Draft→Shipped→Delivered) |

**Бизнис правила:**
- `InventoryBalance.SubtractQuantity()` фрла exception ако нема доволно залиха
- `InventoryBalance.AddQuantity()` не дозволува негативно додавање
- Секое движење = `InventoryMovement` запис (audit trail)

### C) Production (LON / MES-lite)
**Цел:** Производство по налог со полна трасирање од суровина до готов производ.

| Ентитет | Опис | Клучно |
|---------|------|--------|
| `BOM` + `BOMLine` | Рецептура (Bill of Materials) | Versioned, BaseQuantity, ScrapPercentage |
| `Routing` + `RoutingOperation` | Технолошки процес | Operations со WorkCenter, StandardTime, SetupTime |
| `ProductionOrder` | Производствен налог | Status lifecycle: Draft → Released → InProgress → Completed → Closed/Cancelled |
| `ProductionOrderMaterial` | Потребен материјал | RequiredQuantity, IssuedQuantity, ReservedQuantity |
| `ProductionOrderOperation` | Операција на налог | StandardTime vs ActualTime, Machine |
| `MaterialIssue` | Издавање материјал | BatchNumber + MRN (задолжително!) |
| `ProductionReceipt` | Прием на готов производ | Нов BatchNumber, QualityStatus, Location |

**Бизнис правила:**
- FG batch МОРА да има lineage до суровини (TraceLink)
- Материјал се издава со задолжителен batch и MRN
- Scrap се пријавува посебно

### D) Customs & Trade Compliance — Царина
**Цел:** Управување со царински декларации по Македонски правилник, со сите 47 полиња (box-ови).

| Ентитет | Опис | Клучно |
|---------|------|--------|
| `CustomsProcedure` | Царинска постапка (конфигурабилна) | Type, RequiresGuarantee, DueDays, RequiresMRNTracking |
| `CustomsProcedureDocument` | Потребни документи по постапка | DocumentType, IsMandatory |
| `CustomsDeclaration` | Царинска декларација (ЕДД) | Box 01-47 полиња, MRN, ProcedureCode (42 00, 51 00), Lines, Documents |
| `CustomsDeclarationLine` | Ставка во декларација | TariffCode, CountryOfOrigin, GrossWeight, NetWeight, CustomsValue, DutyRate, VATRate |
| `CustomsDocument` | Приложен документ | Type (CommercialInvoice, PackingList, CMR, BillOfLading, AWB, Certificate) |
| `MRNRegistry` | Регистар на MRN-ови | TotalQuantity, UsedQuantity, RemainingQuantity, ExpiryDate |
| `LONAuthorization` | Одобрение за увоз за облагородување | AuthorizationNumber, SystemType (ОдложеноПлаќање/ВраќањеДавачки), CompletionPeriodDays |
| `LONAuthorizationItem` | Одобрена стока | ImportItem → CompensatingProduct, YieldRate, AllowedWastePercentage |

**Царински постапки:**
| Код | Постапка | Гаранција? | MRN? |
|-----|----------|-----------|------|
| Local Purchase | Локален набавка | Не | Не |
| 42 00 | Увоз за облагородување (одложено) | Да | Да |
| 51 00 | Увоз за облагородување (враќање) | Да | Да |
| Temporary Import | Привремен увоз | Да | Да |
| Final Clearance | Дефинитивно царинење | Да | Не |
| Export | Извоз / повторен извоз | Не | Да |

**Validation Engine:**
- `DeclarationRuleEngine` — rule-based валидација
- `RequiredFieldsRule` — задолжителни полиња
- `TariffCodeFormatRule` — формат на тарифна ознака
- `TariffCodeExistsRule` — дали постои во TARIC
- `ProcedureCodeValidRule` — дали процедурниот код е валиден

### E) Guarantee Management — Гаранции
**Цел:** Ledger-based управување со царински гаранции. НИКОГАШ balance поле — секогаш пресметка од ledger!

| Ентитет | Опис | Клучно |
|---------|------|--------|
| `GuaranteeAccount` | Гарантна сметка | Bank, Currency, TotalLimit, GetCurrentBalance() од ledger |
| `GuaranteeLedgerEntry` | Ставка во ledger | Debit (задолжување) / Credit (раздолжување), MRN, CustomsDeclaration link |
| `DutyCalculation` | Пресметка на давачки | HSCode, CustomsValue, DutyRate, VATRate, TotalAmount |

**Бизнис правила:**
- `GetCurrentBalance()` = SUM(Debit) - SUM(Credit) — СЕКОГАШ од ledger
- `GetAvailableLimit()` = TotalLimit - CurrentBalance
- Секое задолжување/раздолжување е поврзано со MRN и декларација
- Мора да можеш да одговориш: "Колку гаранција е активна, зошто и од кој MRN"

### F) Traceability — Трасирање
**Цел:** Полна трасирање: Суровина (Batch + MRN) → Производство (WO) → Готов производ (Batch) → Извоз (MRN)

| Ентитет | Опис | Клучно |
|---------|------|--------|
| `TraceLink` | Врска меѓу два настана | SourceType/TargetType (Receipt, MaterialIssue, ProductionReceipt, Shipment), Batch, MRN |
| `BatchGenealogy` | Генеалогија на batch | ParentBatches (JSON), ParentMRNs (JSON), ProductionOrder link |

---

## Event System (Outbox Pattern)

Секоја акција генерира domain event кој се процесира асинхроно:

| Event | Кога? |
|-------|-------|
| `ReceiptCreatedEvent` | Прием на стока |
| `InventoryMovedEvent` | Секое движење на залиха |
| `MaterialIssuedEvent` | Издавање материјал на налог |
| `FGReceivedEvent` | Прием на готов производ |
| `GuaranteeDebitedEvent` | Задолжување на гаранција |
| `GuaranteeCreditedEvent` | Раздолжување на гаранција |
| `CustomsClearedEvent` | Царинско ослободување |
| `ProductionOrderCompletedEvent` | Завршен производствен налог |
| `ShipmentCreatedEvent` | Креирана испорака |

`LON.Worker` (BackgroundService) ги процесира event-ите, ажурира аналитика, и валидира конзистентност.

---

## Knowledge Base (RAG)

AI-powered знаење за царински регулативи:

| Компонента | Имплементација |
|-----------|----------------|
| `IRAGService` | OpenAI GPT-4o-mini за Q&A |
| `IEmbeddingService` | OpenAI text-embedding-ada-002 |
| `IVectorStoreService` | In-memory vector store |
| `IDocumentChunkingService` | Chunking на документи |

**Извори:** Закон за царинење, Правилник за пополнување на ЕДД, TARIC, Уредби, Прирачници — сите во `/kb/Raw_Files/`.

---

## API Controllers (15)

| Controller | Одговорност |
|-----------|-------------|
| `AuthController` | Login, Register, Refresh Token |
| `UsersController` | CRUD корисници |
| `RolesController` | CRUD улоги + пермисии |
| `PermissionsController` | Листа на пермисии |
| `EmployeesController` | CRUD вработени |
| `ShiftsController` | CRUD смени |
| `MasterDataController` | Items, UoMs, Warehouses, Locations, Partners, WorkCenters, Machines, BOMs, Routings |
| `WMSController` | Receipts, Transfers, PickTasks, Shipments, CycleCounts, InventoryBalance, Movements, Adjustments |
| `ProductionController` | ProductionOrders, MaterialIssues, ProductionReceipts, BOM explosion |
| `CustomsController` | Declarations, Procedures, MRN Registry, LON Authorizations, Validation |
| `GuaranteeController` | Accounts, Ledger, Debit/Credit, DutyCalculation |
| `TraceabilityController` | TraceLinks, BatchGenealogy, Graph traversal |
| `AnalyticsController` | KPIs, Dashboards, Productivity, Financial reports |
| `KnowledgeBaseController` | RAG Chat, Document search |
| `BaseController` | Base class со заеднички функционалности |

---

## Frontend (Web — React + TypeScript)

### Страници и рути:

| Модул | Рути | Опис |
|-------|------|------|
| **Dashboard** | `/dashboard` | Оперативни KPI, преглед |
| **Inventory** | `/inventory` | Салдо, движења, прием, трансфер, пикирање |
| **Production** | `/production` | Налози, издавање материјал, прием на ГП |
| **Customs** | `/customs` | Декларации (ЕДД форма) |
| **Guarantees** | `/guarantees` | Гарантни сметки, ledger |
| **Traceability** | `/traceability` | Batch → MRN → WO → FG → Export |
| **WMS** | `/wms/pick-tasks` | Pick tasks |
| **Reports** | `/reports/*` | WMS Dashboard, Inventory by Location/MRN/Batch, Blocked, Movements, Cycle Count, Utilization |
| **Advanced** | `/advanced/*` | Batch Traceability, MRN Usage, Location/Item Inquiry |
| **Admin** | `/admin/*` | Users, Employees, Shifts, Roles |
| **Master Data** | `/master-data/*` | Items, Partners, Warehouses, Locations, WorkCenters, Machines, UoMs, BOMs, Routings, Code Lists |
| **Knowledge Base** | `/knowledge-base` | AI Chat за царински прашања |

### WMS Форми:
`ReceiptForm`, `TransferForm`, `PickTaskForm`, `ShipmentForm`, `AdjustmentForm`, `CycleCountForm`, `QualityStatusChangeForm`

### Production Форми:
`ProductionOrderForm`, `MaterialIssueForm`, `ProductionReceiptForm`

### Customs Форми:
`CustomsDeclarationForm` — полна ЕДД форма со сите box-ови

---

## Frontend (Mobile — Flutter)

Offline-first мобилна апликација за складиштари:

| Screen | Опис |
|--------|------|
| `HomeScreen` | Главен мени |
| `ReceiveScreen` | Прием на стока (scan) |
| `PickScreen` | Пикирање (scan) |
| `IssueScreen` | Издавање за производство |
| `FGReceiptScreen` | Прием на готов производ |

Providers: `InventoryProvider`, `SyncProvider` (offline queue)

---

## Технолошки стек

| Слој | Технологија |
|------|------------|
| Backend | .NET 8, ASP.NET Core Web API |
| ORM | Entity Framework Core (Code First + Migrations) |
| Database | SQL Server 2022 |
| Auth | JWT + Refresh Tokens + RBAC |
| CQRS | MediatR |
| Frontend Web | React 18 + TypeScript + Material UI + react-toastify |
| Frontend Mobile | Flutter (Dart) |
| AI/RAG | OpenAI (GPT-4o-mini + ada-002 embeddings) |
| Messaging | Outbox Pattern (без екстерен broker) |
| Container | Docker + docker-compose |
| API Docs | Swagger/OpenAPI |

---

## Deployment

```
docker-compose.yml
├── sqlserver     (mcr.microsoft.com/mssql/server:2022-latest)
├── api           (LON.API на порт 5000)
├── worker        (LON.Worker - event processing)
└── frontend      (React app на nginx, порт 80)
```

Потребни env variables: `SQL_SA_PASSWORD`, `JWT_SECRET_KEY`, `OPENAI_API_KEY` (optional), `ENABLE_VECTOR_STORE`.

---

## Проект структура

```
LON-test/
├── LON.sln                           # Solution file
├── docker-compose.yml                # Deployment
├── .env                              # Environment variables
│
├── src/
│   ├── LON.Domain/                   # Entities, Enums, Events, Value Objects
│   │   ├── Entities/
│   │   │   ├── MasterData/           # Item, Warehouse, Location, Partner, Employee, User, Role...
│   │   │   ├── WMS/                  # Receipt, InventoryBalance, Movement, Transfer, PickTask, Shipment...
│   │   │   ├── Production/           # BOM, Routing, ProductionOrder, MaterialIssue, ProductionReceipt
│   │   │   ├── Customs/              # CustomsDeclaration, Procedure, MRNRegistry, LONAuthorization
│   │   │   ├── Guarantee/            # GuaranteeAccount, LedgerEntry, DutyCalculation
│   │   │   └── Traceability/         # TraceLink, BatchGenealogy
│   │   ├── Enums/                    # ItemType, QualityStatus, ProductionOrderStatus...
│   │   ├── Events/                   # Domain events (9 events)
│   │   └── Common/                   # BaseEntity, ValueObject, DomainEvent
│   │
│   ├── LON.Application/              # Commands, Queries, Validation, Services
│   │   ├── Common/                   # ICommand, IQuery, DTOs, Result
│   │   ├── Customs/                  # CreateDeclaration, ValidateDeclaration, Rule Engine
│   │   ├── Production/               # CreateProductionOrder
│   │   ├── WMS/                      # CreateReceipt
│   │   ├── Guarantee/                # Debit/Credit commands
│   │   └── KnowledgeBase/            # RAG, Embedding, Vector Store, Chunking interfaces
│   │
│   ├── LON.Infrastructure/           # EF Core, Persistence, Services
│   │   ├── Persistence/              # DbContext, Configurations (15), Migrations, Seeders
│   │   ├── Services/                 # AuthService, RAG, Embedding, VectorStore
│   │   └── Initialization/           # UserManagementSeed, VectorStoreInitializer
│   │
│   ├── LON.API/                      # Web API (15 Controllers)
│   │   ├── Controllers/              # Auth, Users, Roles, MasterData, WMS, Production, Customs...
│   │   └── Program.cs               # Startup, DI, JWT, Swagger, DB init
│   │
│   └── LON.Worker/                   # Background event processor
│       └── EventProcessorWorker.cs
│
├── frontend/
│   ├── web/                          # React + TypeScript
│   │   ├── src/
│   │   │   ├── pages/                # Dashboard, Inventory, Production, Customs, Guarantees...
│   │   │   ├── components/           # Sidebar, WMS forms, Production forms, common components
│   │   │   ├── services/             # api.ts, authService.ts, masterDataApi.ts
│   │   │   ├── types/                # TypeScript interfaces per module
│   │   │   └── store/                # Zustand (useMasterDataStore)
│   │   └── package.json
│   │
│   └── mobile/                       # Flutter
│       └── lib/                      # main.dart, screens, providers
│
├── kb/                               # Knowledge Base
│   ├── Raw_Files/                    # PDF/XLSX/DOC документи (царински регулативи)
│   ├── processed/                    # JSON (TARIC, codelists, validation rules, countries)
│   └── scripts/                      # Python scripts за обработка
│
├── docs/                             # Architecture, ERD, Flow diagrams
└── scripts/                          # Migration scripts
```

---

## Клучни бизнис текови

### 1. Увоз за облагородување (42 00)
```
LON Одобрение → Царинска декларација (IM 42 00) → MRN регистрација
    → Гаранција задолжена → Прием во складиште (Receipt + Batch + MRN)
    → Издавање на производство (MaterialIssue) → Производствен налог
    → Прием на готов производ (FG Receipt + нов Batch)
    → Извоз (EX декларација + Shipment) → Гаранција раздолжена
```

### 2. WMS тек
```
Receipt → QualityCheck → Putaway (Location assignment)
    → Storage → Pick Task → Packing → Shipment → Delivery
    ↕ Transfers, Adjustments, Cycle Counts
```

### 3. Production тек
```
BOM + Routing → Production Order (Draft → Released → InProgress)
    → Material Issue (Batch + MRN задолжителни)
    → Operations (WorkCenter + Machine + Time tracking)
    → FG Receipt (нов Batch, lineage до суровини)
    → Completed → Closed
```

### 4. Guarantee тек
```
GuaranteeAccount (Bank, Limit)
    → Debit (при увоз: CustomsDeclaration + MRN)
    → Credit (при извоз/раздолжување)
    Balance = СЕКОГАШ од Ledger entries
```

---

## Број на ентитети по модул

| Модул | Ентитети | Клучни |
|-------|----------|--------|
| Master Data | 14 | Item, Warehouse, Location, Partner, Employee, User, Role, Permission, Shift, WorkCenter, Machine, UoM, ItemUoMConversion, CodeListItem |
| WMS | 12 | Receipt, ReceiptLine, InventoryBalance, InventoryMovement, Transfer, TransferLine, CycleCount, CycleCountLine, PickingWave, PickTask, Shipment, ShipmentLine |
| Production | 8 | BOM, BOMLine, Routing, RoutingOperation, ProductionOrder, ProductionOrderMaterial, ProductionOrderOperation, MaterialIssue, ProductionReceipt |
| Customs | 8 | CustomsProcedure, CustomsProcedureDocument, CustomsDeclaration, CustomsDeclarationLine, CustomsDocument, MRNRegistry, LONAuthorization, LONAuthorizationItem |
| Guarantee | 3 | GuaranteeAccount, GuaranteeLedgerEntry, DutyCalculation |
| Traceability | 2 | TraceLink, BatchGenealogy |
| Knowledge Base | 2 | KnowledgeDocument, KnowledgeDocumentChunk |
| **Вкупно** | **~49** | |

---

*Blueprint генериран на 2026-03-16*
