# LON — Roadmap за преостанатите фази (P7 → P13)

> **Single source of truth за следливост.** Секој ред има стабилен ID (`P7.1`,
> `P11.3` итн.) што се користи во commit messages, WORK_PLAN status updates и
> SESSION_LOG записи. `CLAUDE.md` § 7 налага cross-reference.
>
> **Статус легенда (повторена од WORK_PLAN):** `[ ]` не започнат ·
> `[/]` во тек · `[x]` готов + верификуван · `[!]` блокиран · `[~]` скипнат.
>
> **Ефорт:**
> - **S** ≤ 0.5 ден — пат-low: еден query + еден page, reuse на постоечки backend.
> - **M** 1–2 дена — нова мала ентитет + CRUD + UI.
> - **L** 3–5 дена — повеќе ентитети, workflow, events, голем UI.
> - **XL** 1–2 недели — целосен нов subsystem.
>
> **Приоритет:**
> - **P0** hot-path за TEKSPORT дневни операции (блокира експертска валидација).
> - **P1** висок ROI; природно follow-up на активно shipped функционалност.
> - **P2** nice-to-have; reporting/visibility; не го блокира critical flow.
> - **P3** long-tail — management KPI-и без јасен data source или rarely used.
>
> **DoD за секоја ставка:** (a) backend green, (b) frontend green, (c) VPS
> deployed, (d) SESSION_LOG запис со доказ, (e) WORK_PLAN status обновен, (f) за
> промена на backend contract — integration test пиратски-catch regression.

---

## 🎯 Executive snapshot (2026-04-20)

**Shipped до овој commit:**
- Phases 0, 1, 3, 5, 6-Priority-A, 6-Priority-B (partial).
- Phase 2 (целиот TEKSPORT IM flow + P2.1–P2.7 compliance gaps + P2.6a/b/c + export/return/waste).
- Phase 4 (P4.1 Zaverka, P4.2 PEE060, P4.3 MozniMinusi, P4.4 TrafficLight, P4.6 waste slots, P4.7 TariffCodeRate).
- Phase 2.5 (P2.5.1–P2.5.6 ready; P2.5.7 partial со CSV helper).
- Phase 5 (P5.1 generic importer, P5.2.1/2/5/6/7/8/3/4, P5.3.1/2/3/4/5).
- Phase 6.37 customs IA conversion (5 placeholder страници → real views).

**Преостанува: ~65 placeholder страници распоредени во 7 групи.** Сите се
специфицирани со `workPlanRef` во `navGroups.ts` + `plannedBehavior` string. Овој
документ ги групира во 7 фази (P7–P13) со линеарен execution order.

---

## Легенда за табелата
- **ID** — стабилна референца во commits/logs.
- **Path** — frontend route (авторитативен во App.tsx + navGroups).
- **Backend** — што треба: `reuse` (постоечки endpoint), `aggregate` (нов query,
  нема нов entity), `new-entity` (нова ентитет + миграција), `workflow` (нова
  ентитет + state machine + events).
- **Eff** — S / M / L / XL.
- **Pri** — P0 / P1 / P2 / P3.
- **Deps** — други ID-и што мора да бидат shipped прво.

---

## Phase 7 — Quick wins (view-only over existing data)

**Цел:** 1 session (~1 ден). Избришете ~9 placeholder-и со чиста aggregation врз
постоечки endpoints. Без нови ентитети, без нови миграции.

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P7.1** | `/warehouse/incoming` | aggregate: Receipts WHERE status=Draft/Planned | S | P1 | — | **✅ 2026-04-20** — MRN registry WHERE UsedQuantity=0 (pragmatic ASN proxy). `IncomingShipments.tsx`. |
| **P7.2** | `/warehouse/qc-hold` | reuse: BlockedInventory report + `PUT /wms/inventory/{id}/quality-status` | S | P1 | — | **✅ 2026-04-20** — `QcHold.tsx` со blocked/quarantine toggle + inline release. |
| **P7.3** | `/warehouse/variance` | reuse: CycleCount + CycleCountLine постојат | M | P2 | — | **✅ 2026-04-20** — `VarianceReport.tsx` со shortage/surplus tabs + CSV. |
| **P7.4** | `/warehouse/ready-to-ship` | aggregate: Shipments WHERE Status=Packed | S | P1 | — | **✅ 2026-04-20** — `ShipmentsByStatus.tsx` filterStatus=4. |
| **P7.5** | `/warehouse/stock-by-customer` | reuse: InventoryByMRN + partner join | S | P2 | — | **✅ 2026-04-20** — `StockByCustomer.tsx` со collapsible groups + CSV. |
| **P7.6** | `/finished/shipped` | aggregate: Shipments WHERE Status=Shipped | S | P1 | — | **✅ 2026-04-20** — `ShipmentsByStatus.tsx` filterStatus=5 (reused component). |
| **P7.7** | `/finished/traceability` | reuse: `/traceability` + reverse walk | S | P2 | — | **✅ 2026-04-20** — redirect на /customs/traceability. |
| **P7.8** | `/finished/history-by-customer` | aggregate: Shipments GROUPBY CustomerId, Month | S | P2 | P7.6 | **✅ 2026-04-20** — `ShipmentsHistoryByCustomer.tsx` customer × month matrix. |
| **P7.9** | `/customs/search`, `/warehouse/search`, `/production/search` | aggregate: text-based scoped search | S | P1 | — | **✅ 2026-04-20** — `ScopedSearch.tsx` reusable со 3 scope variants. |

**Phase 7 DoD:** ✅ 2026-04-20 — 9 `PlaceholderPage` блокови избришани. 7 pages со CSV export, 1 redirect, 1 scoped-search component (3 routes).

---

## Phase 8 — Production operations visibility

**Цел:** TEKSPORT primary flow — од backlog до completed — видлив во апликацијата.
2–3 sessions. Најмногу aggregations; минорни schema additions.

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P8.1** | `/production/today` | aggregate: PO WHERE PlannedStartDate ≤ today ≤ PlannedEndDate | S | P0 | — | **✅ 2026-04-20** — `ProductionToday.tsx`. Client-side filter on `GET /Production/orders`; progress bar + colour-coded by pct; CSV export. |
| **P8.2** | `/production/wip` | aggregate: PO(Status=InProgress) + InventoryBalance(LonProcessState=InProduction) | S | P0 | — | **✅ 2026-04-20** — `ProductionWip.tsx`. Two-section page: POs InProgress + WIP inventory rows (LonProcessState=6). Independent CSV per section. |
| **P8.3** | `/production/completed` | aggregate: PO(Status=Completed) | S | P1 | — | **✅ 2026-04-20** — `ProductionCompleted.tsx`. Period selector (7/30/90/365) on ActualEndDate (falls back to PlannedEndDate); totals row (ordered/produced/scrap). |
| **P8.4** | `/production/at-risk` | aggregate + heuristic: остаток × минути/парче vs преостанато време | M | P1 | — | **✅ 2026-04-20** — `ProductionAtRisk.tsx`. Schedule-vs-progress heuristic (`scheduleUsed% − progress%`): red ≥ 25% + ≤ 7d to end, amber ≥ 10%. Operations-based refinement deferred to P8.9. |
| **P8.5** | `/production/shortage` | aggregate: ProductionOrderMaterial WHERE RequiredQuantity > available inventory | M | P0 | — | **✅ 2026-04-20** — new backend `GET /Production/shortage` + MediatR `GetProductionShortageQuery`. Sums (Required − Issued) across active POs per material; subtracts OK/Imported inventory; surfaces deficit rows with expandable affected-POs detail. `ProductionShortage.tsx`. |
| **P8.6** | `/production/cutting-queue` | aggregate: ProductionOrderOperation WHERE OperationType='Cutting' AND Status≠Completed | M | P1 | — | Бара `ProductionOrderOperation.Status` enum + `OperationType` tag (може да се изведе од име). |
| **P8.7** | `/production/sewing-queue` | исто како P8.6 со OperationType='Sewing' | M | P1 | P8.6 | Shared component. |
| **P8.8** | `/production/rework` | reuse: Waste declarations + InventoryMovement(Type=Adjustment) | S | P2 | — | Поврзано со P4.6 waste slots. |
| **P8.9** | `/production/minutes-variance` | new-entity: `OperationTimeLog(OperationId, StartedAt, StoppedAt, Pieces)` | L | P2 | P8.6 | Bара piece-level time tracking. Отвори за UI дискусија со експертот. |

**Phase 8 DoD:** корисник гледа денешен план, WIP, completed, at-risk и shortage без
да влезе во master data. Доказ: TEKSPORT експерт може да препознае оперативна
состојба само од /production/* screens. **Sprint 2 closed 2026-04-20 (P8.1–P8.5).**
P8.6–P8.9 стануваат long-tail (види Sprint 8+); P8.6/P8.7 бараат ProductionOrderOperation
status + OperationType tagging, P8.9 бара новиот `OperationTimeLog` entity.

---

## Phase 9 — Finished Goods domain

**Цел:** нов под-домен за packing + shipment staging. 3–4 sessions.

**Нови ентитети (MVP):**
- `PackingStation` (subtype на `Location` со `Type=Packing`, веќе поддржано).
- `PackingTask` (ProductionOrderId, StationId, AssignedToEmployeeId, Status: Pending/InProgress/Completed/Rejected, StartedAt, CompletedAt, Notes).
- `ReturnRequest` (CustomerId, OriginalShipmentId, Reason, Status, Items[]).
- `PackListTemplate` (CustomerId, Layout JSON, PaperFormat).

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P9.1** | `/finished/awaiting-pack` | aggregate: PO(Status=Completed) WHERE NOT EXISTS ShipmentLine | M | P1 | — | Без нов entity — чиста query. |
| **P9.2** | `/finished/packing` | new-entity: PackingTask + CreatePackingTaskCommand + UpdateStatusCommand | L | P2 | P9.1 | Со assign to station/operator. |
| **P9.3** | `/finished/ready-to-ship` | aggregate: Shipments WHERE Status=Packed | S | P1 | — | Shares data со /warehouse/ready-to-ship (P7.4). |
| **P9.4** | `/finished/shipped` | see P7.6 | — | — | P7.6 | Dup entry; link to P7.6. |
| **P9.5** | `/finished/pack-lists` | new-entity: PackListTemplate + PDF generation | L | P2 | P9.1 | PDF render преку QuestPDF или iText. |
| **P9.6** | `/finished/packaging-stock` | reuse: Items WHERE Type=PackagingMaterial + InventoryBalance | M | P1 | — | Bара `ItemType.PackagingMaterial` нова enum vрednost + миграција. |
| **P9.7** | `/finished/returns` | new-entity: ReturnRequest + CreateReturnRequestCommand + customs hook | L | P2 | P2.6b | Linkage до `CreateReturnDeclarationCommand` за customs impact. |

**Phase 9 DoD:** од Completed PO до Shipment без leaving апликацијата. Експертски
preview pack list за една shipment.

---

## Phase 10 — HR operations

**Цел:** attendance + piece-rate visibility. 2–3 sessions. `Employee` + `Shift`
постојат; нови се AttendanceRecord / Absence / Overtime / OperatorAssignment /
Training.

**Нови ентитети:**
- `AttendanceRecord` (EmployeeId, Date, ClockIn, ClockOut, Hours, Status).
- `Absence` (EmployeeId, From, To, Type: Sick/Vacation/Personal/Other, Approved).
- `OvertimeRecord` (EmployeeId, Date, HoursOvertime, ApprovedBy).
- `OperatorMachineAssignment` (EmployeeId, MachineId, ValidFrom, ValidTo).
- `TrainingRecord` (EmployeeId, TrainingType, CompletedAt, Certification, ExpiresAt).

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P10.1** | `/hr/attendance-today` | new-entity: AttendanceRecord + clock-in/out endpoints | M | P0 | — | Clock-in/out UI + late/early badges. |
| **P10.2** | `/hr/absences` | new-entity: Absence + approval workflow | M | P1 | — | Approve/reject + filter by type. |
| **P10.3** | `/hr/overtime` | new-entity: OvertimeRecord + aggregate vs shift | M | P2 | P10.1 | Weekly overtime summary. |
| **P10.4** | `/hr/performance` | aggregate: OperationTimeLog GROUPBY Operator | L | P2 | P8.9 | Pieces per shift + variance vs standard. |
| **P10.5** | `/hr/assignment` | new-entity: OperatorMachineAssignment | M | P1 | — | Shift board view. |
| **P10.6** | `/hr/training` | new-entity: TrainingRecord | M | P3 | — | Expiry tracking. |
| **P10.7** | `/hr/payroll-export` | aggregate: Attendance + Overtime + Absence + piece-rate | M | P2 | P10.1, P10.3, P12.3 | CSV/Excel export за payroll provider. |

**Phase 10 DoD:** shift lead клика "clock in" за сите, види живи hours + late;
payroll export файл добар за upload во надворешна платформа.

---

## Phase 11 — Machine operations

**Цел:** OEE + downtime tracking + maintenance plan. 2–3 sessions.

**Нови ентитети:**
- `MachineStateEvent` (MachineId, State: Running/Idle/Down/SetUp/Maintenance, Since).
- `DowntimeEvent` (MachineId, Start, End, Reason, RootCauseCategory, CostImpact).
- `MaintenanceSchedule` (MachineId, Interval, LastDone, NextDue, TaskDescription).
- `MaintenanceWorkOrder` (MachineId, ScheduledDate, CompletedAt, TechnicianId, Notes).
- `SetupEvent` (MachineId, FromItemId, ToItemId, SetupMinutes).

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P11.1** | `/machines/status` | new-entity: MachineStateEvent + current-state query | M | P1 | — | **✅ 2026-04-20** — `MachineStateEvent` entity + `POST /Machines/{id}/state-events` + `GET /Machines/current-states` resolver. `MachineStatus.tsx` со pill-based states + inline change modal. |
| **P11.2** | `/machines/downtime` | new-entity: DowntimeEvent | M | P1 | — | **✅ 2026-04-20** — `DowntimeEvent` entity + POST/close endpoints + `GET /Machines/downtime/pareto` category rollup. `MachineDowntime.tsx` со inline log + Pareto bars + open-events highlight. |
| **P11.3** | `/machines/oee` | aggregate: MachineStateEvent + DowntimeEvent + production pieces | L | P2 | P11.1, P11.2, P8.9 | Availability × Performance × Quality формула. |
| **P11.4** | `/machines/maintenance-plan` | new-entity: MaintenanceSchedule | M | P1 | — | **✅ 2026-04-20** — `MaintenanceSchedule` entity + POST/PUT/GET. NextDue auto-computed од LastDone + IntervalDays, advanced at work-order completion. `MaintenancePlan.tsx` со risk-coloured days-until-due. |
| **P11.5** | `/machines/maintenance-history` | new-entity: MaintenanceWorkOrder | S | P2 | P11.4 | **✅ 2026-04-20** — `MaintenanceWorkOrder` entity + POST/Complete/GET. Complete action rolls Schedule.LastDone + NextDue forward. `MaintenanceHistory.tsx` со inline create + complete action + filter by machine/open. |
| **P11.6** | `/machines/capacity` | aggregate: WorkCenter × RoutingOperation.StandardTime × shift hours | S | P2 | — | Проста roll-up. |
| **P11.7** | `/machines/setup-time` | new-entity: SetupEvent | M | P3 | — | Matrix view (From×To). |
| **P11.8** | `/machines/bottleneck` | aggregate: Throughput vs capacity per work center | L | P3 | P11.1, P11.3, P11.6 | Weeks of analysis work. |

**Phase 11 DoD:** мантенанс менаџер гледа next-due maintenance; shift supervisor
знае колку била downtime денес по машина. **Sprint 3 closed 2026-04-20
(P11.1/11.2/11.4/11.5).** P11.3 OEE + P11.6–P11.8 long-tail — зависат од ops time
log (P8.9) или се bottleneck аналитика за подоцна.

---

## Phase 12 — Finance

**Цел:** invoicing + rate cards + margin. XL phase; ~6–8 sessions. Recommended
spilt across several weeks.

**Нови ентитети:**
- `ClientContract` (PartnerId, ValidFrom, ValidTo, RateCardJson, PaymentTermsDays).
- `RateCardEntry` (ContractId, OperationType/ItemId, RatePerPiece/RatePerMinute, Currency).
- `Invoice` (Number, IssueDate, DueDate, PartnerId, CurrencyCode, TotalAmount, Status).
- `InvoiceLine` (InvoiceId, Description, Qty, UnitPrice, Total, RelatedPOId, RelatedShipmentId).
- `VendorInvoice` (similar to Invoice but AP side).
- `CostRate` (WorkCenterId/MachineId, EffectiveFrom, CostPerMinute).

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P12.1** | `/finance/guarantees` | done (redirect to /guarantees) | — | — | — | Shipped. |
| **P12.2** | `/finance/invoicing` | new-entity: Invoice + InvoiceLine + GenerateFromPoCommand | L | P0 | P12.3 | Первичен драйвер за fiскал кореспонденција. |
| **P12.3** | `/finance/contracts` | new-entity: ClientContract + RateCardEntry | L | P0 | — | Unlock-ува margin + payroll piece rate. |
| **P12.4** | `/finance/cost-accounting` | new-entity: CostRate + aggregate per job | L | P1 | P12.3, P11.6 | Cost/минута × машинско време. |
| **P12.5** | `/finance/margin` | aggregate: Invoice total − CostRate rollup | M | P1 | P12.2, P12.4 | Per PO или per shipment. |
| **P12.6** | `/finance/ap` | new-entity: VendorInvoice | L | P2 | — | Vendor invoice tracking + payment schedule. |
| **P12.7** | `/finance/payroll` | aggregate: HR + RateCardEntry | M | P2 | P10.7, P12.3 | Веќе mostly covered од P10.7. |
| **P12.8** | `/finance/pnl` | aggregate: Invoice − VendorInvoice − CostRate | M | P2 | P12.2, P12.4, P12.6 | Period selector. |
| **P12.9** | `/finance/cash-flow` | new-entity: CashFlowForecast logic | L | P3 | P12.2, P12.6 | Based on DueDate + рокови. |
| **P12.10** | `/finance/reports` | reuse: existing reports + catalogue | S | P3 | — | Index page. |

**Phase 12 DoD:** invoice се испраќа директно од апликација за еден реален
TEKSPORT job; margin показател таков што експертот го потврдува vs нивни сметки.

---

## Phase 13 — Management KPIs

**Цел:** role-specific rollup views. Мostly aggregations врз earlier фази.
Тргнуваме кога дата-изворите се свежи.

| ID | Path | Backend | Eff | Pri | Deps | Note |
|---|---|---|---|---|---|---|
| **P13.1** | `/management/on-time` | aggregate: Shipment.ShipmentDate vs ClientContract.PromiseDate | M | P0 | P12.3 | Топ метрика за менаџмент. |
| **P13.2** | `/management/capacity` | aggregate: Machine.capacity − booked | M | P2 | P11 | Capacity rollup. |
| **P13.3** | `/management/by-customer` | aggregate: Production + shipment by customer | S | P1 | P7.8 | Shares logic со P7.8. |
| **P13.4** | `/management/margin` | aggregate: margin by customer | M | P2 | P12.5 | — |
| **P13.5** | `/management/alerts` | aggregate: multi-source (MRN expiring, low stock, at-risk PO) | M | P0 | P7–P8 | Единствен alert feed — high value. |
| **P13.6** | `/management/risks` | new-entity: RiskRegisterItem | M | P2 | — | Manual register. |
| **P13.7** | `/management/trends` | aggregate: 3M/6M/12M rollups | M | P3 | P13.* | Time-series chart. |
| **P13.8** | `/management/escalations` | new-entity: EscalationCase + workflow | M | P3 | — | Trigger + route + SLA. |
| **P13.9** | `/management/client-scorecard` | aggregate: per-customer KPI summary | M | P2 | P13.1, P13.3, P13.4 | One page per client. |
| **P13.10** | `/management/monthly-pack` | aggregate + PDF: monthly review | M | P3 | P13.* | PDF export за board. |

**Phase 13 DoD:** CEO/COO отворa `/management/alerts` прво нешто наутро и добива
ажурирана листа од сите домени без да клика повеќе.

---

## Препорачан редослед за извршување

> Принцип: domain-first bundles, но со ранги kwarter wins на почеток да се
> избришат најболните empty страници. Секој bundle е 1–3 sessions.

1. **Sprint 1 (1 session) — Phase 7 complete.** 9 S-sized quick wins.
2. **Sprint 2 (2 sessions) — Phase 8.1–8.5.** Production visibility core (today,
   WIP, completed, at-risk, shortage). Високо-П0, TEKSPORT daily ops.
3. **Sprint 3 (1 session) — Phase 11.1–11.2 + 11.4–11.5.** Machine basics
   (manual state + downtime + maintenance schedule + history). Без OEE yet.
4. **Sprint 4 (1 session) — Phase 10.1–10.2 + 10.5.** HR basics (attendance +
   absences + assignment). Без payroll yet.
5. **Sprint 5 (2 sessions) — Phase 9.1 + 9.3 + 9.6.** FG simple queries (awaiting
   pack, ready-to-ship, packaging stock). P9.2/9.5/9.7 deferred.
6. **Sprint 6 (2 sessions) — Phase 12.3 contracts + P12.2 invoicing MVP.** Largest
   ROI од финансии.
7. **Sprint 7 (1 session) — Phase 13.1 + 13.3 + 13.5.** Management alerts + on-time
   + by-customer. Податоците веќе постојат од претходните sprint-и.
8. **Sprint 8+ (long tail) — Phase 8.6–8.9, 9.2/9.5/9.7, 10.3–10.7, 11.3+11.7/8,
   12.4–12.10, 13.2/4/6–10.** Preioritize по ad-hoc barania од експертот.

**Ретро after sprint 7:** пред long tail, прави demo со TEKSPORT експертот. Сите
hot-path screens се треба да се поклапуваат со неговите дневни операции. Ако
некој view е undefined/surprise, преработи пред да додаваш low-priority
дashboards.

---

## Cross-cutting remaining

Не во navGroups но треба да се трага:

- **P2.5.4** retrofit — останати страници (Production, Admin, Reports) на
  `formatQuantity`/`formatDate`. Opportunistic.
- **P2.5.7 extension** — PDF render (invoices + pack lists во Phase 9/12).
- **P6.12** — uniform `{ isSuccess, data, errorMessage, errorCode, errors }`
  response envelope низ сите controllers. Тест refactor блокира.
- **P6.36 continuing** — per-line duty breakdown panel + waste-slot preview во
  WasteDeclarationModal + advisory панел од `/validate` rule engine.
- **P6.37.13** — user-driven per-role visual smoke (чека корисник-интеракција).
- **P6.37.15** — `design:accessibility-review` аудит (deferred).
- **P7** Flutter mobile app — целосно посебен track.

---

## Update правила за овој документ

1. **Секогаш** кога се shipping-ира ставка, WORK_PLAN.md + овој файл го добиваат
   status update (check-box) во истиот commit.
2. Ако се појави нова ставка што не е во table-ите погоре — додади ред под
   природната фаза со следниот broj (`P8.10`, `P12.11`, ...), не
   преименувај постоечки ID-и.
3. Ако ставка помине на deferred / cancelled — маркирај `[~]` и додади
   **Причина:** ред под неа.
4. Dependencies мора да останат консистентни. Ако A зависи од B и B се измени,
   провери сите A што го наведуваат.

*Последна ревизија: 2026-04-20.*
