LON Production + WMS + Customs & Trade Compliance + Guarantee Management + BI Analytics

Ова е самостојна enterprise апликација, индустриски независна, со можност за интеграција со ERP, царина, шпедитери и BI алатки.

🎯 Цел: систем што реално може да се користи во фирма со:
увоз на суровини (различни постапки),
производство по налог (LON),
следење batch + MRN,
управување со царински гаранции,
извоз / раздолжување,
детална аналитика и трасирање.
Нема ограничување во обем. Направи сè што е потребно.

1️⃣ ТЕХНОЛОГИЈА (НЕ МЕНУВАЈ)
Backend: .NET 8 – ASP.NET Core Web API
DB: SQL Server
ORM: EF Core (Code First + Migrations)
Auth: JWT + Roles
Worker: BackgroundService (event processing, recalculations)
Frontend Web: React + TypeScript
Mobile: Flutter (offline-first)
Messaging: Outbox pattern (без екстерен broker во v1)
BI: SQL views + aggregation tables + API
Docker: docker-compose
Repo: Clean Architecture

2️⃣ ОСНОВЕН КОНЦЕПТ (ОБАВЕЗЕН)
Системот е:
Event-driven
Ledger-based (за гаранции)
Batch + MRN centric
Procedure-driven (царински правила)
❗ НИШТО не смее да биде “скриено поле” – сè мора да биде објект, настан или запис.

3️⃣ CORE DOMAIN MODULES (FULL IMPLEMENTATION)
🟦 A) MASTER DATA
Items (Raw / Semi / FG / Packaging)
UoM + conversion
Warehouses → Locations (bin level)
Partners (Supplier, Customer, Carrier)
Employees
WorkCenters, Machines

👉 Items мора да поддржуваат:
Batch tracking
MRN linkage
HS/Tariff code
Country of origin

🟦 B) WMS – FULL LOGIC
Inbound
Receipt
Quality status (OK / Blocked / Quarantine)
Putaway rules (by item, procedure, location type)
Inventory
InventoryBalance = Item + Batch + MRN + Location
No negative stock
No movement without location
Internal
Transfers
Replenishment
Cycle counts (planned + ad-hoc)
Outbound
Picking waves
Pick tasks
Packing
Shipment

🟦 C) LON / PRODUCTION (MES-lite)
Production Order lifecycle
Draft
Released
In Progress
Completed
Closed / Cancelled
BOM & Routing
Versioned BOM
Operations with standard time
Work centers
Material Flow
Reservation
Pick for production
Issue to WO (batch mandatory)
Scrap reporting
FG receipt → new batch

❗ FG batch мора да има lineage до суровини

🟦 D) CUSTOMS & TRADE COMPLIANCE
Customs Procedures (CONFIGURABLE)
Local purchase
Temporary import
Inward processing (облагородување)
Final clearance
Секој тип дефинира:
Дали се задолжува гаранција
Кои документи се задолжителни
Дозволени движења
Рокови
Правила за раздолжување
Documents
Customs Declarations
MRN Registry
Commercial Invoice
Packing List
Transport docs (CMR/BL/AWB)

🟦 E) GUARANTEE MANAGEMENT (CRITICAL)
Guarantee Accounts
По фирма / банка
Валутни
Лимит
Ledger (НЕ balance поле!)
Debit (задолжување)
Credit (раздолжување)
Link to:
Receipt
Customs declaration
Export
Production output
Duty Calculation Engine
HS code
Customs value
Duty %
VAT
Other charges
Snapshot per transaction
❗ Мора да можеш да кажеш:
„Колку гаранција е активна, зошто и од кој MRN“

🟦 F) TRACEABILITY GRAPH (OBLIGATORY)

Системот мора да овозможи:
Raw batch + MRN → WO → FG batch → Export MRN
Reverse tracing за царина и инспекција
Имплементирај:
TraceLinks table
Graph traversal logic

4️⃣ EVENT & OUTBOX SYSTEM

Секоја акција генерира event:
InventoryMoved
MaterialIssued
FGReceived
GuaranteeDebited
GuaranteeCredited
CustomsCleared
Worker:
process events
update analytics
validate consistency

5️⃣ BI & ANALYTICS (REAL, NOT FAKE)
Operational KPIs
WIP
Shortages
Open guarantees
Expiring procedures
Blocked batches
Productivity
Per employee
Per work center
Per machine
Per operation
Financial
Cost per WO
Yield vs scrap
Guarantee exposure

Имплементирај:
Aggregation tables
SQL views
API endpoints

6️⃣ FRONTEND (WORKING, NOT DEMO)
Web
Dashboards
Lists + details
Status colors
Drill-down (WO → batches → MRN → guarantee)
Mobile (Flutter)
Scan-first UI
Offline queue
Sync conflicts handling
Pick / Issue / Putaway / FG receipt

7️⃣ DATABASE

Normalized tables
Proper FK
Indexes
Constraints
Seed data

8️⃣ DOCUMENTATION

Во /docs:
Architecture
ERD
Flowcharts (production, customs, guarantee)
Sequence diagrams
README (step by step run)

9️⃣ DELIVERY RULES (STRICT)

Проектот мора да се билдa и стартува
Docker compose мора да работи
Нема TODO во core logic
Нема празни методи

🔚 OUTPUT FORMAT

Еден единствен документ
За секој фајл:

# path/to/file
```code
...

- Никакви zip
- Никакви линкови
- Никакви објаснувања надвор од кодот и README

---

## 🧠 МЕНТАЛЕН МОДЕЛ
Размислувај како:
- цариник
- производствен менаџер
- финансиски директор
- warehouse supervisor
- auditor

Ако нешто не е логично во реална фирма → редизајнирај го.