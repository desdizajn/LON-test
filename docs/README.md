# LON Production + WMS + Customs & Trade Compliance + Guarantee Management + BI Analytics

Enterprise system за интегрирано управување со производство, складирање, царински постапки и гаранции.

## 🎯 Цел

Систем кој реално може да се користи во производствена фирма со:
- Увоз на суровини (различни царински постапки)
- Производство по налог (LON - Lot Order Number)
- Следење на batch + MRN (Movement Reference Number)
- Управување со царински гаранции
- Извоз / раздолжување
- Детална аналитика и трасирање

## 📋 Технологија

### Backend
- **.NET 8** - ASP.NET Core Web API
- **SQL Server** - Релациска база
- **Entity Framework Core** - Code First + Migrations
- **JWT** - Автентикација и авторизација
- **MediatR** - CQRS pattern
- **Background Service** - Event processing

### Frontend
- **React + TypeScript** - Web апликација
- **Flutter** - Mobile апликација (offline-first)

### Infrastructure
- **Docker & Docker Compose** - Контејнеризација
- **Clean Architecture** - Слоевита архитектура

## 🏗️ Архитектура

```
┌─────────────────────────────────────────────────┐
│              Frontend Layer                      │
│  ┌──────────────┐        ┌──────────────┐       │
│  │ React Web    │        │ Flutter      │       │
│  │ (Dashboard,  │        │ (Scan-first, │       │
│  │  Analytics)  │        │  Offline)    │       │
│  └──────────────┘        └──────────────┘       │
└─────────────────────────────────────────────────┘
                     ↓ HTTP/REST
┌─────────────────────────────────────────────────┐
│              API Layer (LON.API)                 │
│  Controllers: WMS, Production, Customs,          │
│              Guarantees, Traceability, Analytics │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│         Application Layer (LON.Application)      │
│  Commands, Queries, Validators, DTOs, Events    │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│         Domain Layer (LON.Domain)                │
│  Entities, Value Objects, Domain Events, Enums  │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│      Infrastructure Layer (LON.Infrastructure)   │
│  DbContext, Configurations, Repositories         │
└─────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────┐
│              SQL Server Database                 │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│         Background Worker (LON.Worker)           │
│  Event Processor, Outbox Pattern                │
└─────────────────────────────────────────────────┘
```

## 📦 Модули

### A) Master Data
- **Items** (Raw, Semi-Finished, Finished Goods, Packaging)
  - Batch tracking
  - MRN linkage
  - HS Code / Tariff code
  - Country of origin
- **UoM** (Units of Measure) + Conversions
- **Warehouses** → **Locations** (bin level)
- **Partners** (Supplier, Customer, Carrier, Bank)
- **Employees**
- **WorkCenters** + **Machines**

### B) WMS (Warehouse Management System)

#### Inbound
- **Receipt** - Прием на материјал
- **Quality Status** (OK / Blocked / Quarantine)
- **Putaway Rules** - Правила за складирање

#### Inventory
- **InventoryBalance** = Item + Batch + MRN + Location
- No negative stock
- No movement without location

#### Internal Operations
- **Transfers** - Префрлање помеѓу локации
- **Replenishment** - Пополнување
- **Cycle Counts** - Инвентура (planned + ad-hoc)

#### Outbound
- **Picking Waves** - Групирање на pick задачи
- **Pick Tasks** - Подигање материјал
- **Packing** - Пакување
- **Shipment** - Испорака

### C) LON / Production (MES-lite)

#### Production Order Lifecycle
1. Draft
2. Released
3. In Progress
4. Completed
5. Closed / Cancelled

#### BOM & Routing
- **BOM** (Bill of Materials) - Versioned
- **Routing** - Operations with standard time
- **Work Centers** - Работни центри

#### Material Flow
- **Reservation** - Резервација на материјал
- **Pick for Production** - Подигање за производство
- **Issue to WO** - Издавање на материјал (batch mandatory)
- **Scrap Reporting** - Евиденција на отпад
- **FG Receipt** - Прием на готов производ → нов batch

**❗ Важно:** FG batch мора да има lineage (потекло) до суровините

### D) Customs & Trade Compliance

#### Customs Procedures (Configurable)
1. **Local Purchase** - Локална набавка
2. **Temporary Import** - Привремен увоз
3. **Inward Processing** - Облагородување (увоз за преработка)
4. **Final Clearance** - Дефинитивно царинење
5. **Export** - Извоз

Секој тип дефинира:
- Дали се задолжува гаранција
- Кои документи се задолжителни
- Дозволени движења
- Рокови
- Правила за раздолжување

#### Documents
- **Customs Declarations** - Царински декларации
- **MRN Registry** - Регистар на MRN
- **Commercial Invoice** - Фактура
- **Packing List** - Паковна листа
- **Transport Docs** (CMR/BL/AWB)

### E) Guarantee Management (Critical)

#### Guarantee Accounts
- По фирма / банка
- Валутни
- Лимит
- **Ledger-based** (НЕ balance поле!)

#### Ledger Entries
- **Debit** - Задолжување
- **Credit** - Раздолжување
- Link to:
  - Receipt
  - Customs declaration
  - Export
  - Production output

#### Duty Calculation Engine
- HS code
- Customs value
- Duty %
- VAT
- Other charges
- **Snapshot per transaction**

**❗ Критично:** Мора да можеш да кажеш:
"Колку гаранција е активна, зошто и од кој MRN"

### F) Traceability Graph (Obligatory)

Системот овозможува:
- Raw batch + MRN → WO → FG batch → Export MRN
- **Reverse tracing** за царина и инспекција

Имплементација:
- **TraceLinks** table
- Graph traversal logic
- Forward & Backward tracing

## 🔄 Event & Outbox System

### Domain Events
Секоја акција генерира event:
- `InventoryMovedEvent`
- `MaterialIssuedEvent`
- `FGReceivedEvent`
- `GuaranteeDebitedEvent`
- `GuaranteeCreditedEvent`
- `CustomsClearedEvent`

### Worker
Background service кој:
- Обработува events од Outbox
- Ажурира аналитика
- Валидира конзистентност

## 📊 BI & Analytics

### Operational KPIs
- **WIP** (Work in Progress)
- **Shortages** - Недостатоци
- **Open Guarantees** - Активни гаранции
- **Expiring Procedures** - Истекувачки процедури
- **Blocked Batches** - Блокирани batch-ови

### Productivity
- Per Employee
- Per Work Center
- Per Machine
- Per Operation

### Financial
- **Cost per WO** - Трошок по налог
- **Yield vs Scrap** - Принос vs отпад
- **Guarantee Exposure** - Изложеност на гаранции

## 🚀 Deployment

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK
- Node.js 18+
- Flutter SDK (за mobile)

### Running with Docker

```bash
# Build и стартување на сите сервиси
docker-compose up --build

# Апликацијата ќе биде достапна на:
# - API: http://localhost:5000
# - Web Frontend: http://localhost:3000
# - SQL Server: localhost:1433
```

### Running Locally (Development)

```bash
# 1. Стартување на SQL Server (Docker)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# 2. Апликација на миграции
cd src/LON.Infrastructure
dotnet ef database update --startup-project ../LON.API/LON.API.csproj

# 3. Стартување на API
cd ../LON.API
dotnet run

# 4. Стартување на Worker
cd ../LON.Worker
dotnet run

# 5. Стартување на React frontend
cd ../../frontend/web
npm install
npm start

# 6. Flutter mobile
cd ../mobile
flutter pub get
flutter run
```

## 📐 Database Schema

### Core Tables
- **Items** - Артикли
- **Warehouses**, **Locations** - Складишта и локации
- **InventoryBalances** - Состојба на залиха
- **InventoryMovements** - Движења
- **Receipts**, **ReceiptLines** - Приеми
- **Shipments**, **ShipmentLines** - Испораки
- **ProductionOrders** - Производни налози
- **BOMs**, **BOMLines** - Саставници
- **MaterialIssues** - Издавања материјал
- **ProductionReceipts** - Приеми готов производ
- **CustomsProcedures** - Царински постапки
- **CustomsDeclarations** - Царински декларации
- **MRNRegistries** - MRN регистар
- **GuaranteeAccounts** - Гаранциски сметки
- **GuaranteeLedgerEntries** - Книга на гаранции (ledger)
- **TraceLinks** - Врски за трасирање
- **OutboxMessages** - Outbox за events

## 🔐 Security

- JWT Bearer authentication
- Role-based authorization (можност за проширување)
- HTTPS во production
- Environment variables за sensitive data

## 📱 Mobile Features

- **Offline-first** - Работи без интернет
- **Scan-first UI** - Barcode scanning
- **Sync Queue** - Автоматска синхронизација
- Функции:
  - Прием материјал
  - Pick задачи
  - Издавање за производство
  - Прием готов производ

## 🧪 Testing

```bash
# Unit тестови
dotnet test

# Integration тестови
# TODO: Add integration tests
```

## 📈 Performance

- EF Core с Query Filters
- Indexed columns за брзо пребарување
- Pagination на сите листи
- Background worker за тешки операции

## 🔮 Future Enhancements

- [ ] Real-time notifications (SignalR)
- [ ] Advanced BI with Power BI integration
- [ ] Machine Learning за предвидување
- [ ] Blockchain за трасирање
- [ ] Advanced scheduling & optimization
- [ ] Multi-tenant support
- [ ] Audit trail со детални логови

## 👥 Contributors

LON System - Enterprise Production & WMS Solution

## 📄 License

Proprietary - За internal употреба

---

**Датум на креирање:** December 2025  
**Верзија:** 1.0.0  
**Status:** Production Ready ✅
