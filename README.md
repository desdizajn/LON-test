# LON Production + WMS + Customs & Trade Compliance System

🏭 **Enterprise system** за интегрирано управување со **производство**, **складирање**, **царински постапки** и **гаранции**.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver)
![React](https://img.shields.io/badge/React-18.2-61DAFB?logo=react&logoColor=black)
![Flutter](https://img.shields.io/badge/Flutter-3.0+-02569B?logo=flutter)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)

---

## ✨ Features

### 🏪 Master Data Management
- Items (Raw, Semi-Finished, Finished Goods, Packaging)
- Batch tracking & MRN linkage
- HS Codes & Country of origin
- Warehouses & Locations (bin level)
- Partners (Suppliers, Customers, Carriers, Banks)
- Work Centers & Machines

### 📦 WMS (Warehouse Management System)
- **Inbound:** Receipts with quality status
- **Inventory:** Real-time balance tracking per batch + MRN + location
- **Internal:** Transfers, Replenishment, Cycle counts
- **Outbound:** Picking waves, Packing, Shipments
- **Rule:** No negative stock, No movement without location

### 🏭 LON / Production (MES-lite)
- Production orders with lifecycle (Draft → Released → In Progress → Completed → Closed)
- BOM (Bill of Materials) - Versioned
- Routing (Operations with standard time)
- Material reservation & picking
- Issue to work order (**mandatory batch + MRN**)
- FG receipt with automatic batch generation
- Scrap reporting
- **Full traceability** from raw material to finished goods

### 🚢 Customs & Trade Compliance
- **Configurable procedures:**
  - Local Purchase
  - Temporary Import
  - **Inward Processing** (облагородување)
  - Final Clearance
  - Export
- Customs declarations with MRN tracking
- MRN registry with usage tracking
- Due date monitoring
- Document management

### 💰 Guarantee Management
- **Ledger-based** guarantee accounts (НЕ balance поле!)
- Debit on import (Inward Processing → 50% duty)
- Credit on export (Раздолжување)
- Real-time exposure tracking
- Expiring guarantees alerts
- Multi-currency support

### 🔍 Traceability Graph
- **Forward tracing:** Raw batch + MRN → WO → FG batch → Export MRN
- **Backward tracing:** Export MRN → FG batch → Materials
- Batch genealogy with parent batches & MRNs
- TraceLink navigation
- Query: "За овој извоз, кои увози се користени?"

### 📊 BI & Analytics
- Real-time dashboard
- Production KPIs (WIP, Yield, Scrap, Productivity)
- WMS KPIs (Blocked inventory, Cycle count accuracy)
- Customs summary (Pending declarations, Active MRNs, Expiring procedures)
- Guarantee exposure
- Inventory by location
- MRN usage analysis

### 🔄 Event-Driven Architecture
- Outbox pattern for reliable event processing
- Background worker (10-second polling)
- Event handlers for analytics updates
- Audit trail

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────┐
│  React Web (Dashboard, Analytics, Reports)   │
│  Flutter Mobile (Scan-first, Offline-first)  │
└────────────────┬─────────────────────────────┘
                 │ HTTP/REST
                 ▼
┌──────────────────────────────────────────────┐
│         API Layer (Controllers)              │
│  WMS, Production, Customs, Guarantees, etc.  │
└────────────────┬─────────────────────────────┘
                 │
      ┌──────────┴──────────┐
      │                     │
      ▼                     ▼
┌─────────────┐      ┌─────────────┐
│ Application │      │   Worker    │
│  (CQRS)     │      │ (Outbox)    │
└──────┬──────┘      └──────┬──────┘
       │                    │
       └────────┬───────────┘
                ▼
┌──────────────────────────────────────────────┐
│         Domain Layer (Entities)              │
└────────────────┬─────────────────────────────┘
                 ▼
┌──────────────────────────────────────────────┐
│   Infrastructure (EF Core, SQL Server)       │
└──────────────────────────────────────────────┘
```

**Pattern:** Clean Architecture + CQRS + Event Sourcing (Guarantee Ledger) + Outbox Pattern

---

## 🚀 Quick Start

### With Docker Compose (Recommended)

```bash
# 1. Clone repository
git clone <repo-url>
cd LON-test

# 2. Start all services
docker-compose up --build

# 3. Access
# - Web UI: http://localhost:3000
# - API: http://localhost:5000
# - Swagger: http://localhost:5000/swagger
```

**Default credentials:**
- Username: `admin@lon.local`
- Password: `Admin@123`

### Local Development

#### Prerequisites
- .NET 8 SDK
- SQL Server 2022
- Node.js 18+
- Flutter SDK (for mobile)

#### Backend

```bash
# 1. Start SQL Server (Docker)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# 2. Apply migrations
cd src/LON.Infrastructure
dotnet ef database update --startup-project ../LON.API/LON.API.csproj

# 3. Run API
cd ../LON.API
dotnet run

# 4. Run Worker
cd ../LON.Worker
dotnet run
```

#### Frontend

```bash
# Web (React)
cd frontend/web
npm install
npm start

# Mobile (Flutter)
cd frontend/mobile
flutter pub get
flutter run
```

---

## 📚 Documentation

Comprehensive documentation is available in the [`docs/`](docs/) folder:

- [**README.md**](docs/README.md) - System overview, features, deployment
- [**ARCHITECTURE.md**](docs/ARCHITECTURE.md) - Clean Architecture layers, patterns, dependencies
- [**ERD.md**](docs/ERD.md) - Complete entity relationship diagram
- [**PRODUCTION_FLOW.md**](docs/PRODUCTION_FLOW.md) - Production process from receipt to shipment
- [**CUSTOMS_FLOW.md**](docs/CUSTOMS_FLOW.md) - Customs procedures & guarantee management
- [**API.md**](docs/API.md) - Complete API endpoints reference
- [**DEPLOYMENT.md**](docs/DEPLOYMENT.md) - Deployment guide (Docker, Azure, K8s)

---

## 🎯 Key Business Rules

1. **No Issue without Batch** - Batch number is mandatory when issuing material
2. **No FG Receipt without TraceLinks** - Finished goods must have lineage to raw materials
3. **MRN Tracking** - For Inward Processing, MRN tracking is mandatory
4. **Guarantee Management** - Debit on import, Credit on export
5. **Quality Status** - Only OK items can be issued for production
6. **No Negative Stock** - Inventory balance cannot be negative
7. **Ledger-Based Guarantees** - Balance is calculated, not stored
8. **Batch Genealogy** - Every FG batch knows its parent batches and MRNs

---

## 🗄️ Database Schema

**Core tables (40+):**
- Master Data: Items, UoM, Warehouses, Locations, Partners, Employees, WorkCenters, Machines
- WMS: Receipts, InventoryBalances, InventoryMovements, Transfers, CycleCounts, PickTasks, Shipments
- Production: BOMs, Routings, ProductionOrders, MaterialIssues, ProductionReceipts
- Customs: CustomsProcedures, CustomsDeclarations, MRNRegistry
- Guarantees: GuaranteeAccounts, GuaranteeLedgerEntries, DutyCalculations
- Traceability: TraceLinks, BatchGenealogy
- Events: OutboxMessages

**Key Constraints:**
- `InventoryBalance`: UNIQUE(ItemId, LocationId, BatchNumber, MRN)
- `MRNRegistry`: UNIQUE(MRN)
- Soft delete on all entities
- Audit fields on all entities

---

## 🔐 Security

- JWT Bearer authentication
- Role-based authorization
- HTTPS in production
- Environment variables for sensitive data
- Connection string encryption
- SQL injection protection (EF Core parameterized queries)

---

## 📈 Performance

- EF Core with query filters
- Indexed columns for fast lookups
- Pagination on all lists
- Background worker for heavy operations
- Async/await throughout
- No N+1 query problems

---

## 🧪 Testing

```bash
# Unit tests
dotnet test

# Build verification
dotnet build

# Check for errors
dotnet build --no-incremental
```

---

## 🛠️ Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Backend | .NET / ASP.NET Core | 8.0 |
| Database | SQL Server | 2022 |
| ORM | Entity Framework Core | 8.0 |
| Authentication | JWT Bearer | - |
| CQRS | MediatR | 12.2 |
| Frontend | React + TypeScript | 18.2 |
| Mobile | Flutter | 3.0+ |
| State Management | Provider (Flutter) | - |
| API Client | Axios | - |
| Charts | Chart.js | - |
| Container | Docker & Docker Compose | - |
| Web Server | Nginx | - |

---

## 📦 Project Structure

```
LON-test/
├── src/
│   ├── LON.Domain/          # Entities, Value Objects, Enums, Domain Events
│   ├── LON.Application/     # Commands, Queries, DTOs, Interfaces
│   ├── LON.Infrastructure/  # DbContext, Configurations, Migrations, Seed
│   ├── LON.API/             # Controllers, Middleware, Program.cs
│   └── LON.Worker/          # Background Service (Outbox Processor)
├── frontend/
│   ├── web/                 # React + TypeScript
│   └── mobile/              # Flutter
├── docs/                    # Documentation
├── docker-compose.yml       # Multi-container orchestration
├── .gitignore
└── README.md
```

---

## 🚢 Deployment

### Docker Compose

```bash
docker-compose up --build
```

Services:
- `sqlserver` - SQL Server 2022 (port 1433)
- `api` - .NET API (port 5000)
- `worker` - Background worker
- `frontend` - React app (port 3000)

### Azure

See [DEPLOYMENT.md](docs/DEPLOYMENT.md) for Azure App Service, Static Web Apps, and Kubernetes deployment instructions.

---

## 🤝 Contributing

This is a complete enterprise system. Contributions are welcome!

Areas for enhancement:
- [ ] Real-time notifications (SignalR)
- [ ] Advanced BI with Power BI integration
- [ ] Machine Learning predictions
- [ ] Blockchain for traceability
- [ ] Advanced scheduling & optimization
- [ ] Multi-tenant support
- [ ] Comprehensive test coverage

---

## 📝 License

Proprietary - For internal use

---

## 🎓 Learning Resources

- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html)

---

## 📞 Support

For questions or issues, please create an issue in the repository.

---

**Built with ❤️ using .NET 8, React, Flutter, and SQL Server**

**Version:** 1.0.0  
**Status:** ✅ Production Ready  
**Last Updated:** December 2025
