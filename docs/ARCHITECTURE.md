# Architecture Overview

## Clean Architecture Layers

### 1. Domain Layer (LON.Domain)
**Responsibility:** Core business logic and entities

- **Entities:** Business objects (Item, ProductionOrder, CustomsDeclaration, etc.)
- **Value Objects:** Immutable objects без identity
- **Domain Events:** Events кои се случуваат во доменот
- **Enums:** Типови и статуси
- **Interfaces:** Contracts за репозиторија (minimal)

**Dependency:** ZERO - не зависи од ништо друго

### 2. Application Layer (LON.Application)
**Responsibility:** Application business logic, orchestration

- **Commands:** Write operations (CreateProductionOrder, DebitGuarantee)
- **Queries:** Read operations (GetInventory, GetDashboard)
- **DTOs:** Data Transfer Objects
- **Validators:** FluentValidation rules
- **Interfaces:** IApplicationDbContext

**Dependencies:** LON.Domain, MediatR, FluentValidation

### 3. Infrastructure Layer (LON.Infrastructure)
**Responsibility:** External concerns (DB, Files, APIs)

- **Persistence:** DbContext, Configurations, Migrations
- **Repositories:** Concrete implementations (ако се користат)
- **External Services:** API clients, File storage

**Dependencies:** LON.Application, LON.Domain, EF Core

### 4. API Layer (LON.API)
**Responsibility:** HTTP endpoints, presentation

- **Controllers:** REST API endpoints
- **Middleware:** Error handling, logging
- **Filters:** Authorization, validation
- **Program.cs:** DI configuration

**Dependencies:** LON.Infrastructure, LON.Application

### 5. Worker Layer (LON.Worker)
**Responsibility:** Background processing

- **Background Services:** Outbox processor, scheduled tasks
- **Event Handlers:** Process domain events

**Dependencies:** LON.Infrastructure, LON.Application

## Dependency Flow

```
┌─────────────┐
│   API/Web   │
└──────┬──────┘
       │
       ↓
┌─────────────────┐
│  Infrastructure │
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│   Application   │
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│     Domain      │  ← Core, no dependencies
└─────────────────┘
```

## Key Patterns

### 1. CQRS (Command Query Responsibility Segregation)
- Commands за write operations
- Queries за read operations
- MediatR за dispatching

### 2. Repository Pattern (Minimal)
- DbContext директно во Application handlers
- Можност за репозиторија ако е потребно

### 3. Outbox Pattern
- Domain events се зачувуваат како OutboxMessages
- Worker ги обработува асинхроно
- Transactional consistency

### 4. Event-Driven Architecture
- Domain events за loosely coupled logic
- Background processing за тешки операции

### 5. Ledger Pattern (за Guarantees)
- НЕ balance поле - само entries
- Debit/Credit записи
- Calculated balance

## Data Flow Examples

### Example 1: Create Production Order

```
User → API Controller
       ↓
    MediatR.Send(CreateProductionOrderCommand)
       ↓
    CommandHandler (Application)
       ↓
    Create ProductionOrder entity (Domain)
       ↓
    DbContext.SaveChanges (Infrastructure)
       ↓
    Domain Event → OutboxMessage
       ↓
    Worker процесира event
```

### Example 2: Query Dashboard

```
User → API Controller
       ↓
    MediatR.Send(GetDashboardQuery)
       ↓
    QueryHandler (Application)
       ↓
    DbContext query (Infrastructure)
       ↓
    Return DTO → Controller → User
```

### Example 3: Material Issue with Traceability

```
Mobile App → API Controller
       ↓
    IssueMaterialCommand
       ↓
    Create MaterialIssue entity
    Create TraceLink (Material → WO)
    Update InventoryBalance
    Create InventoryMovement
       ↓
    Emit MaterialIssuedEvent
       ↓
    Worker процесира:
      - Update analytics
      - Check WO material requirements
      - Notify if shortage
```

## Scalability Considerations

### Horizontal Scaling
- API instances (stateless)
- Worker instances (distributed lock за Outbox)
- Frontend static files (CDN)

### Vertical Scaling
- Database (SQL Server scaling options)
- In-memory caching (Redis - future)

### Performance Optimizations
- EF Core query optimization
- Indexed columns
- Pagination
- Async/await throughout
- Background processing за тешки операции

## Security Architecture

### Authentication
- JWT Bearer tokens
- Token expiry: 60 minutes
- Refresh tokens (future enhancement)

### Authorization
- Role-based (extensible)
- Controller-level `[Authorize]`
- Custom policies (future)

### Data Protection
- Connection strings во environment variables
- Secrets management (Azure Key Vault - future)
- HTTPS во production

## Monitoring & Observability

### Logging
- Built-in .NET logging
- Structured logging (Serilog - future enhancement)
- Log levels: Debug, Info, Warning, Error

### Metrics (Future)
- Application Insights
- Performance counters
- Business metrics

### Tracing (Future)
- Distributed tracing
- Correlation IDs
