import { NavGroup } from './types';

/**
 * **Single source of truth for sidebar IA (P6.37).**
 *
 * Groups are populated incrementally by P6.37.5–P6.37.12:
 *  - P6.37.5 Warehouse (pilot)
 *  - P6.37.6 Customs
 *  - P6.37.7 Production
 *  - P6.37.8 Finished Goods
 *  - P6.37.9 HR
 *  - P6.37.10 Machines / Work Centers / Efficiency
 *  - P6.37.11 Finance
 *  - P6.37.12 Management (KPI)
 *
 * Until a group's items[] is populated, the Sidebar renders the header with
 * a "🚧 во изработка" affordance. The legacy flat sidebar still works side-by-side
 * until P6.37.14 flips redirects.
 *
 * See `docs/design/P6-37-ia.md` for the full role×group matrix and per-item
 * backend status inventory.
 */
export const NAV_GROUPS: NavGroup[] = [
  {
    key: 'warehouse',
    icon: '🏭',
    labelKey: 'nav.groups.warehouse',
    allowedRoles: [
      'Administrator',
      'Warehouse Manager',
      'Warehouse Operator',
      'Quality Controller',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'warehouse-receipts',
        labelKey: 'nav.warehouse.receipts',
        icon: '📥',
        path: '/warehouse/receipts',
        backendStatus: 'exists',
        existingDataHint:
          'Постои како /inventory. Прикажува сите примени материјали со lot + MRN + location.',
      },
      {
        key: 'warehouse-incoming',
        labelKey: 'nav.warehouse.incoming',
        icon: '🚚',
        path: '/warehouse/incoming',
        backendStatus: 'exists',
        existingDataHint: 'P7.1 — MRN регистирани но без прием (UsedQuantity=0) → очекувани физички пратки.',
        plannedBehavior:
          'Листа на најавени пратки: клиент, очекуван датум, shipment #, MRN (ако е познат), очекувана qty. Кога физички материјал пристигне, магационер go конвертира во Receipt.',
      },
      {
        key: 'warehouse-qc-hold',
        labelKey: 'nav.warehouse.qcHold',
        icon: '🔒',
        path: '/warehouse/qc-hold',
        backendStatus: 'exists',
        existingDataHint: 'P7.2 — QualityStatus ≠ OK со inline release action.',
      },
      {
        key: 'warehouse-issues-today',
        labelKey: 'nav.warehouse.issuesToday',
        icon: '📤',
        path: '/warehouse/issues-today',
        backendStatus: 'partial',
        workPlanRef: 'P2.X — daily issue plan',
        plannedBehavior:
          'Денешен план за издавање во кројална: материјал × налог × qty × локација од која се издава. Со bulk-pick action.',
        existingDataHint: 'Pick Tasks (/wms/pick-tasks) го покрива generic pick flow.',
      },
      {
        key: 'warehouse-transfers',
        labelKey: 'nav.warehouse.transfers',
        icon: '🔀',
        path: '/warehouse/transfers',
        backendStatus: 'exists',
        existingDataHint:
          'P5.2.7 Mass Location Transfer — филтрирај по артикал/batch/MRN/магацин/квалитет/LON и префрли во една таргет локација атомично.',
      },
      {
        key: 'warehouse-bulk-receipt',
        labelKey: 'nav.warehouse.bulkReceipt',
        icon: '📥',
        path: '/warehouse/bulk-receipt',
        backendStatus: 'exists',
        existingDataHint:
          'P5.2.3 Bulk Receipt — едно кликнување експлоадира декларациски линии во приемни линии.',
      },
      {
        key: 'warehouse-bulk-shipment',
        labelKey: 'nav.warehouse.bulkShipment',
        icon: '📤',
        path: '/warehouse/bulk-shipment',
        backendStatus: 'exists',
        existingDataHint:
          'P5.2.4 Bulk Shipment — филтрирај FG и автоматски креирај Shipment + (опц.) EX.',
      },
      {
        key: 'warehouse-stock-by-customer',
        labelKey: 'nav.warehouse.stockByCustomer',
        icon: '📋',
        path: '/warehouse/stock-by-customer',
        backendStatus: 'exists',
        existingDataHint: 'P7.5 — Inventory → MRN → Partner grouping со collapsible view.',
      },
      {
        key: 'warehouse-variance',
        labelKey: 'nav.warehouse.variance',
        icon: '⚠️',
        path: '/warehouse/variance',
        backendStatus: 'exists',
        existingDataHint: 'P7.3 — CycleCountLine variances (shortage/surplus) across сите counts.',
        plannedBehavior:
          'Разлики помеѓу очекувано и реално: (a) cycle count наспроти систем, (b) shortage спрема денешен план, (c) вишок без документ. Со експлицитни actions за поправка.',
      },
      {
        key: 'warehouse-ready-to-ship',
        labelKey: 'nav.warehouse.readyToShip',
        icon: '🚢',
        path: '/warehouse/ready-to-ship',
        backendStatus: 'exists',
        existingDataHint: 'P7.4 — Shipments WHERE Status=Packed со CSV export.',
      },
      {
        key: 'warehouse-search',
        labelKey: 'nav.warehouse.search',
        icon: '🔍',
        path: '/warehouse/search',
        backendStatus: 'exists',
        existingDataHint: 'P7.9 — scoped search across receipts, shipments, inventory.',
      },
    ],
  },
  {
    key: 'customs',
    icon: '🛃',
    labelKey: 'nav.groups.customs',
    allowedRoles: [
      'Administrator',
      'Customs Officer',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'customs-authorizations',
        labelKey: 'nav.customs.authorizations',
        icon: '📜',
        path: '/customs/authorizations',
        backendStatus: 'exists',
        existingDataHint: 'Листа со истек, статус, гаранција + days-left индикатор.',
        plannedBehavior:
          'Активни царински дозволи за облагородување (LON authorizations) по тенант: број, важност, procedure code, преостаната количина.',
      },
      {
        key: 'customs-import-docs',
        labelKey: 'nav.customs.importDocs',
        icon: '📄',
        path: '/customs/import-docs',
        backendStatus: 'exists',
        existingDataHint: 'Постои како /customs — главна листа на царински декларации.',
      },
      {
        key: 'customs-export-docs',
        labelKey: 'nav.customs.exportDocs',
        icon: '📤',
        path: '/customs/export-docs',
        backendStatus: 'exists',
        existingDataHint: 'Извозни декларации (IM/EX discrimination по procedureCode).',
        plannedBehavior:
          'Листа на извозни декларации со статус, linkiranи shipments и раздолжени увозни MRN позиции.',
      },
      {
        key: 'customs-traceability',
        labelKey: 'nav.customs.traceability',
        icon: '🔗',
        path: '/customs/traceability',
        backendStatus: 'exists',
        existingDataHint:
          'Постои како /traceability — стабло материјал ↔ налог ↔ shipment. Премести од top-level.',
      },
      {
        key: 'customs-deadlines',
        labelKey: 'nav.customs.deadlines',
        icon: '⏰',
        path: '/customs/deadlines',
        backendStatus: 'exists',
        existingDataHint: 'MRN со days-left, consumption + discharge meters, outstanding qty.',
        plannedBehavior:
          'Deadline-driven листа: MRN и authorizations што истекуваат за N дена. Со filter „истекува за: <7, <30, <90 дена“.',
      },
      {
        key: 'customs-open-items',
        labelKey: 'nav.customs.openItems',
        icon: '❗',
        path: '/customs/open-items',
        backendStatus: 'exists',
        existingDataHint: 'Истиот view како Deadlines со onlyOpen=true filter — нераздолжени MRN позиции.',
        plannedBehavior:
          'Нераздолжени ставки: MRN позиции каде consumed qty < imported qty и не е поврзано со извозна декларација. Критично за царински ризик.',
      },
      {
        key: 'customs-guarantees',
        labelKey: 'nav.customs.guarantees',
        icon: '💰',
        path: '/customs/guarantees',
        backendStatus: 'exists',
        existingDataHint:
          'Постои како /guarantees — царински гаранции со ledger и auto-debit logic (P2.2).',
      },
      {
        key: 'customs-search',
        labelKey: 'nav.customs.search',
        icon: '🔍',
        path: '/customs/search',
        backendStatus: 'exists',
        existingDataHint: 'P7.9 — scoped search across declarations, MRN registry, authorizations.',
      },
    ],
  },
  {
    key: 'production',
    icon: '✂️',
    labelKey: 'nav.groups.production',
    allowedRoles: [
      'Administrator',
      'Production Manager',
      'Production Operator',
      'Quality Controller',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'production-today',
        labelKey: 'nav.production.today',
        icon: '📅',
        path: '/production/today',
        backendStatus: 'exists',
        existingDataHint: 'P8.1 — POs active today (PlannedStart ≤ today ≤ PlannedEnd) with progress bars + CSV.',
      },
      {
        key: 'production-cutting-queue',
        labelKey: 'nav.production.cuttingQueue',
        icon: '✂️',
        path: '/production/cutting-queue',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — cutting queue',
        plannedBehavior:
          'Queue на налози чекаат кроење: prioritet, required material (со shortage flags), estimated minutes, allotted machine. Со drag-to-reorder приоритизација.',
      },
      {
        key: 'production-sewing-queue',
        labelKey: 'nav.production.sewingQueue',
        icon: '🧵',
        path: '/production/sewing-queue',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — sewing queue',
        plannedBehavior:
          'Queue на налози во шиење: по линија / оператор / машина. WIP visibility, required operations per route, capacity check.',
      },
      {
        key: 'production-wip',
        labelKey: 'nav.production.wip',
        icon: '⚙️',
        path: '/production/wip',
        backendStatus: 'exists',
        existingDataHint: 'P8.2 — InProgress POs + InventoryBalance(LonProcessState=InProduction) in one view.',
      },
      {
        key: 'production-at-risk',
        labelKey: 'nav.production.atRisk',
        icon: '🚨',
        path: '/production/at-risk',
        backendStatus: 'exists',
        existingDataHint: 'P8.4 — heuristic schedule_used% − progress% gap, red/amber banding.',
      },
      {
        key: 'production-shortage',
        labelKey: 'nav.production.shortage',
        icon: '📉',
        path: '/production/shortage',
        backendStatus: 'exists',
        existingDataHint: 'P8.5 — GET /Production/shortage: (Required − Issued) sum vs available inventory per material.',
      },
      {
        key: 'production-minutes-variance',
        labelKey: 'nav.production.minutesVariance',
        icon: '⏱️',
        path: '/production/minutes-variance',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — standard vs actual minutes',
        plannedBehavior:
          'Routing standard минути vs actual time log: отклони по налог / оператор / линија. Критично за billing (фабриката продава минути).',
      },
      {
        key: 'production-rework',
        labelKey: 'nav.production.rework',
        icon: '🔁',
        path: '/production/rework',
        backendStatus: 'exists',
        workPlanRef: 'P4.6 — waste slots (backend exists)',
        plannedBehavior:
          'Rework и waste парчиња: причина, cost impact, responsible operator/machine. P4.6 backend постои; UI недостасува.',
      },
      {
        key: 'production-completed',
        labelKey: 'nav.production.completed',
        icon: '✅',
        path: '/production/completed',
        backendStatus: 'exists',
        existingDataHint: 'P8.3 — Completed POs with period selector (7/30/90/365) + totals + CSV.',
      },
      {
        key: 'production-search',
        labelKey: 'nav.production.search',
        icon: '🔍',
        path: '/production/search',
        backendStatus: 'exists',
        existingDataHint: 'P7.9 — scoped search across production orders.',
      },
    ],
  },
  {
    key: 'finished-goods',
    icon: '📦',
    labelKey: 'nav.groups.finishedGoods',
    allowedRoles: [
      'Administrator',
      'Warehouse Manager',
      'Customs Officer',
      'Production Manager',
      'Quality Controller',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'finished-awaiting-pack',
        labelKey: 'nav.finishedGoods.awaitingPack',
        icon: '📥',
        path: '/finished/awaiting-pack',
        backendStatus: 'exists',
        existingDataHint: 'P9.1 — Completed POs where ProducedQuantity > SUM(ShipmentLine.Quantity joined on batch). Server-side aggregate.',
      },
      {
        key: 'finished-packing',
        labelKey: 'nav.finishedGoods.packing',
        icon: '📦',
        path: '/finished/packing',
        backendStatus: 'exists',
        workPlanRef: 'P4.X — packing in progress',
        plannedBehavior:
          'Налози во тек на пакување: % complete, pack station, оператор. Real-time update како парчињата се пакуваат.',
      },
      {
        key: 'finished-ready-to-ship',
        labelKey: 'nav.finishedGoods.readyToShip',
        icon: '🚢',
        path: '/finished/ready-to-ship',
        backendStatus: 'exists',
        existingDataHint: 'P9.3 — Shipments WHERE Status=Packed. Reuses ShipmentsByStatus component (same as /warehouse/ready-to-ship).',
      },
      {
        key: 'finished-shipped',
        labelKey: 'nav.finishedGoods.shipped',
        icon: '✈️',
        path: '/finished/shipped',
        backendStatus: 'exists',
        existingDataHint: 'P7.6 — Shipments WHERE Status=Shipped со CSV export.',
      },
      {
        key: 'finished-pack-lists',
        labelKey: 'nav.finishedGoods.packLists',
        icon: '📋',
        path: '/finished/pack-lists',
        backendStatus: 'exists',
        workPlanRef: 'P4.X — pack list / label generation',
        plannedBehavior:
          'Генерирање на pack lists + етикети во клиентски формат. Templates per клиент.',
      },
      {
        key: 'finished-packaging-stock',
        labelKey: 'nav.finishedGoods.packagingStock',
        icon: '📬',
        path: '/finished/packaging-stock',
        backendStatus: 'exists',
        existingDataHint: 'P9.6 — Items.Type=Packaging × InventoryBalance rollup. Reorder-point alerts deferred until the field exists on Item.',
      },
      {
        key: 'finished-returns',
        labelKey: 'nav.finishedGoods.returns',
        icon: '↩️',
        path: '/finished/returns',
        backendStatus: 'exists',
        workPlanRef: 'P4.X — RMA flow',
        plannedBehavior:
          'Враќања и рекламации од клиент: причина, qty, action (rework / replace / credit note). Со царински импликации.',
      },
      {
        key: 'finished-history-by-customer',
        labelKey: 'nav.finishedGoods.historyByCustomer',
        icon: '📊',
        path: '/finished/history-by-customer',
        backendStatus: 'exists',
        existingDataHint: 'P7.8 — customer × month матрица (count + qty) со CSV export.',
      },
      {
        key: 'finished-traceability',
        labelKey: 'nav.finishedGoods.traceability',
        icon: '🔗',
        path: '/finished/traceability',
        backendStatus: 'exists',
        existingDataHint: 'P7.7 — redirect на /customs/traceability (forward + reverse во истото дрво).',
      },
    ],
  },
  {
    key: 'hr',
    icon: '👥',
    labelKey: 'nav.groups.hr',
    allowedRoles: [
      'Administrator',
      'HR Manager',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'hr-employees',
        labelKey: 'nav.hr.employees',
        icon: '🧑‍💼',
        path: '/hr/employees',
        backendStatus: 'exists',
        existingDataHint:
          'Постои како /admin/employees — master list на вработени. Премести од Admin под HR.',
      },
      {
        key: 'hr-attendance-today',
        labelKey: 'nav.hr.attendanceToday',
        icon: '📅',
        path: '/hr/attendance-today',
        backendStatus: 'exists',
        existingDataHint: 'P10.1 — AttendanceRecord entity + Clock-in/Clock-out endpoints. One row per (Employee, Date); hours computed on clock-out.',
      },
      {
        key: 'hr-shifts',
        labelKey: 'nav.hr.shifts',
        icon: '🕐',
        path: '/hr/shifts',
        backendStatus: 'exists',
        existingDataHint:
          'Постои како /admin/shifts — shift management. Премести од Admin под HR.',
      },
      {
        key: 'hr-absences',
        labelKey: 'nav.hr.absences',
        icon: '🏥',
        path: '/hr/absences',
        backendStatus: 'exists',
        existingDataHint: 'P10.2 — Absence entity + create + decide (approve/reject) workflow with stamped ApprovedBy/At.',
      },
      {
        key: 'hr-overtime',
        labelKey: 'nav.hr.overtime',
        icon: '⏰',
        path: '/hr/overtime',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — overtime tracking',
        plannedBehavior:
          'Overtime по оператор: колку, причина, approved / pending. Кумулативно за месецот за плата.',
      },
      {
        key: 'hr-performance',
        labelKey: 'nav.hr.performance',
        icon: '📈',
        path: '/hr/performance',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — operator performance metrics',
        plannedBehavior:
          'Учин по оператор: минути искористени vs стандардни, парчиња произведени, quality score. Ranking и тренд низ време.',
      },
      {
        key: 'hr-assignment',
        labelKey: 'nav.hr.assignment',
        icon: '🔗',
        path: '/hr/assignment',
        backendStatus: 'exists',
        existingDataHint: 'P10.5 — OperatorMachineAssignment with ValidFrom/ValidTo windows + active-only filter.',
      },
      {
        key: 'hr-training',
        labelKey: 'nav.hr.training',
        icon: '🎓',
        path: '/hr/training',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — training & certifications',
        plannedBehavior:
          'Сертификати на вработени per machine type / operation. Рок на важност, предупредувања пред истек.',
      },
      {
        key: 'hr-payroll-export',
        labelKey: 'nav.hr.payrollExport',
        icon: '💳',
        path: '/hr/payroll-export',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — payroll hours aggregation',
        plannedBehavior:
          'Export на вкупни часови за плата: регуларни + overtime + bonus. Export формат компатибилен со надворешен payroll систем.',
      },
    ],
  },
  {
    key: 'machines',
    icon: '⚙️',
    labelKey: 'nav.groups.machines',
    allowedRoles: [
      'Administrator',
      'Maintenance Tech',
      'Production Manager',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'machines-status',
        labelKey: 'nav.machines.status',
        icon: '📡',
        path: '/machines/status',
        backendStatus: 'exists',
        existingDataHint: 'P11.1 — MachineStateEvent log + current-state resolver. Manual entry until telemetry lands.',
      },
      {
        key: 'machines-work-centers',
        labelKey: 'nav.machines.workCenters',
        icon: '🏭',
        path: '/machines/work-centers',
        backendStatus: 'exists',
        existingDataHint:
          'Постои како /master-data/workcenters. Линии / групи машини.',
      },
      {
        key: 'machines-downtime',
        labelKey: 'nav.machines.downtime',
        icon: '🔴',
        path: '/machines/downtime',
        backendStatus: 'exists',
        existingDataHint: 'P11.2 — DowntimeEvent log + close action + Pareto-by-category rollup.',
      },
      {
        key: 'machines-oee',
        labelKey: 'nav.machines.oee',
        icon: '📊',
        path: '/machines/oee',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — OEE calculation',
        plannedBehavior:
          'OEE (Availability × Performance × Quality) по машина / линија / смена. Benchmark vs target + тренд низ време.',
      },
      {
        key: 'machines-maintenance-plan',
        labelKey: 'nav.machines.maintenancePlan',
        icon: '📋',
        path: '/machines/maintenance-plan',
        backendStatus: 'exists',
        existingDataHint: 'P11.4 — MaintenanceSchedule per machine with interval-days-driven NextDue.',
      },
      {
        key: 'machines-maintenance-history',
        labelKey: 'nav.machines.maintenanceHistory',
        icon: '🗂️',
        path: '/machines/maintenance-history',
        backendStatus: 'exists',
        existingDataHint: 'P11.5 — MaintenanceWorkOrder list + Complete action that rolls schedule NextDue.',
      },
      {
        key: 'machines-capacity',
        labelKey: 'nav.machines.capacity',
        icon: '📈',
        path: '/machines/capacity',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — capacity utilization',
        plannedBehavior:
          'Искористеност: планиран vs actual часови per машина / линија / смена. Daily / weekly / monthly rollup.',
      },
      {
        key: 'machines-setup-time',
        labelKey: 'nav.machines.setupTime',
        icon: '⏱️',
        path: '/machines/setup-time',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — setup time analysis',
        plannedBehavior:
          'Changeover минути per машина: колку време се троши на setup/switchover. Идентификација на bottleneck setup-и.',
      },
      {
        key: 'machines-bottleneck',
        labelKey: 'nav.machines.bottleneck',
        icon: '🚧',
        path: '/machines/bottleneck',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — bottleneck analysis',
        plannedBehavior:
          'Constraint view за денешниот план: која машина е bottleneck? Што чекаат нејзе? Предложени corrective actions.',
      },
    ],
  },
  {
    key: 'finance',
    icon: '💵',
    labelKey: 'nav.groups.finance',
    allowedRoles: [
      'Administrator',
      'Finance Clerk',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'finance-invoicing',
        labelKey: 'nav.finance.invoicing',
        icon: '🧾',
        path: '/finance/invoicing',
        backendStatus: 'exists',
        workPlanRef: 'P12.2 — invoicing MVP',
        plannedBehavior:
          'Draft → Issued → Paid. Generate from PO via active contract + PerPiece rate. Shipped 2026-04-20.',
      },
      {
        key: 'finance-contracts',
        labelKey: 'nav.finance.contracts',
        icon: '📜',
        path: '/finance/contracts',
        backendStatus: 'exists',
        workPlanRef: 'P12.3 — client contracts + rate cards',
        plannedBehavior:
          'Клиентски договори + rate картички (PerPiece / PerMinute). Authoritative source за invoicing. Shipped 2026-04-20.',
      },
      {
        key: 'finance-guarantees',
        labelKey: 'nav.finance.guarantees',
        icon: '💰',
        path: '/finance/guarantees',
        backendStatus: 'partial',
        workPlanRef: 'P2.2 — guarantee auto-debit (backend exists)',
        plannedBehavior:
          'Finance lens на царинските гаранции: состојба, exposure, movements, кои ослободувања се чекаат.',
        existingDataHint: '/customs/guarantees е истата data со customs lens.',
      },
      {
        key: 'finance-cost-accounting',
        labelKey: 'nav.finance.costAccounting',
        icon: '🧮',
        path: '/finance/cost-accounting',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — cost per minute',
        plannedBehavior:
          'Cost accounting: чинење на минута на машина × оператор × shift. Support за pricing decisions.',
      },
      {
        key: 'finance-margin',
        labelKey: 'nav.finance.margin',
        icon: '📈',
        path: '/finance/margin',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — margin analysis',
        plannedBehavior:
          'Маржа по клиент / налог: revenue (invoiced rate × qty) минус cost (actual minutes × cost rate). Preview пред endgame.',
      },
      {
        key: 'finance-ap',
        labelKey: 'nav.finance.ap',
        icon: '📤',
        path: '/finance/ap',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — accounts payable',
        plannedBehavior:
          'Добавувач invoices: packaging, energy, spare parts. Отворени / платени, due dates.',
      },
      {
        key: 'finance-payroll',
        labelKey: 'nav.finance.payroll',
        icon: '💳',
        path: '/finance/payroll',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — payroll aggregate from HR',
        plannedBehavior:
          'Aggregate плати за месецот: вкупно часови регуларни + overtime + bonus × шифри. Feed од /hr/payroll-export.',
      },
      {
        key: 'finance-pnl',
        labelKey: 'nav.finance.pnl',
        icon: '📊',
        path: '/finance/pnl',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — P&L preview',
        plannedBehavior:
          'Месечен P&L preview: revenue, direct cost, overhead, margin. Со compare to target.',
      },
      {
        key: 'finance-cash-flow',
        labelKey: 'nav.finance.cashFlow',
        icon: '💸',
        path: '/finance/cash-flow',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — cash flow forecast',
        plannedBehavior:
          '30 / 60 / 90 day forecast: expected AR inflow, AP outflow, пресек. Risk flags.',
      },
      {
        key: 'finance-reports',
        labelKey: 'nav.finance.reports',
        icon: '📋',
        path: '/finance/reports',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — financial reports',
        plannedBehavior:
          'Сметководствени извештаи за управа, ревизор, финансиско: BS, IS, CF, статутарни формати.',
      },
    ],
  },
  {
    key: 'management',
    icon: '🎯',
    labelKey: 'nav.groups.management',
    allowedRoles: [
      'Administrator',
      'Manager',
      'Viewer',
    ],
    items: [
      {
        key: 'management-dashboard',
        labelKey: 'nav.management.dashboard',
        icon: '📊',
        path: '/management/dashboard',
        backendStatus: 'partial',
        workPlanRef: 'P6.X — KPI dashboard expansion',
        plannedBehavior:
          'Executive dashboard: on-time %, capacity %, margin %, active risks — со drill-down во секој KPI. Reuse постоечкиот /dashboard како стартна точка, expanded.',
        existingDataHint: '/dashboard постои; треба додатни KPI widgets.',
      },
      {
        key: 'management-on-time',
        labelKey: 'nav.management.onTime',
        icon: '⏰',
        path: '/management/on-time',
        backendStatus: 'exists',
        workPlanRef: 'P13.1 — on-time delivery metric',
        plannedBehavior:
          'On-time % joined Shipment.ShipmentDate ↔ PO.PlannedEndDate via batch. Per-customer rollup. Shipped 2026-04-20.',
      },
      {
        key: 'management-capacity',
        labelKey: 'nav.management.capacity',
        icon: '📈',
        path: '/management/capacity',
        backendStatus: 'exists',
        workPlanRef: 'P3.X — capacity rollup',
        plannedBehavior:
          'Capacity utilization: машини + оператори. Planned hours vs actual hours. Weekly / monthly.',
      },
      {
        key: 'management-by-customer',
        labelKey: 'nav.management.byCustomer',
        icon: '👤',
        path: '/management/by-customer',
        backendStatus: 'exists',
        workPlanRef: 'P13.3 — production + shipment + billing by customer',
        plannedBehavior:
          'Per-customer aggregate: open/completed POs, produced qty, shipment count + qty, invoices issued + outstanding + paid. Shipped 2026-04-20.',
      },
      {
        key: 'management-margin',
        labelKey: 'nav.management.margin',
        icon: '💹',
        path: '/management/margin',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — margin per customer (mgr lens)',
        plannedBehavior:
          'Management lens на margin: не detailed financial но trend + alerts за клиенти со падната маржа.',
      },
      {
        key: 'management-alerts',
        labelKey: 'nav.management.alerts',
        icon: '🚨',
        path: '/management/alerts',
        backendStatus: 'exists',
        workPlanRef: 'P13.5 — exception alerts feed',
        plannedBehavior:
          'Aggregated alerts: MRN expiring, overdue invoices, material shortage, at-risk POs, LON-auth expiring. Severity-sorted. Shipped 2026-04-20.',
      },
      {
        key: 'management-risks',
        labelKey: 'nav.management.risks',
        icon: '⚠️',
        path: '/management/risks',
        backendStatus: 'exists',
        workPlanRef: 'P6.X — open risks register',
        plannedBehavior:
          'Отворени ризици: царински (деадлине), operational (машина, кадар), финансиски (клиент overdue), legal.',
      },
      {
        key: 'management-trends',
        labelKey: 'nav.management.trends',
        icon: '📉',
        path: '/management/trends',
        backendStatus: 'exists',
        workPlanRef: 'P6.X — 3M / 6M / 12M trends',
        plannedBehavior:
          'Тренд analysis на ключни metrics: production volume, margin, on-time, headcount. Compare year-over-year.',
      },
      {
        key: 'management-escalations',
        labelKey: 'nav.management.escalations',
        icon: '🔥',
        path: '/management/escalations',
        backendStatus: 'exists',
        workPlanRef: 'P6.X — escalations',
        plannedBehavior:
          'Налози што бараат управувачка одлука: доцнат, клиент тражи extension, legal issue, budget override.',
      },
      {
        key: 'management-client-scorecard',
        labelKey: 'nav.management.clientScorecard',
        icon: '📇',
        path: '/management/client-scorecard',
        backendStatus: 'exists',
        workPlanRef: 'P5.X — per-client scorecard',
        plannedBehavior:
          'Client scorecard: volume, margin, on-time, payment behavior, риск rating. Помага за renewal / expansion одлуки.',
      },
      {
        key: 'management-monthly-pack',
        labelKey: 'nav.management.monthlyPack',
        icon: '📰',
        path: '/management/monthly-pack',
        backendStatus: 'exists',
        workPlanRef: 'P6.X — monthly review pack',
        plannedBehavior:
          'Printable / exportable pack: exec summary, key metrics, wins/losses, next month focus. За board meetings.',
      },
    ],
  },
];

/**
 * Admin-only group. Rendered AFTER the role-scoped groups, and only when the
 * user has the `Administrator` role. Master data + user/role management live
 * here; they were scattered across multiple top-level sections in the legacy sidebar.
 */
export const SETTINGS_GROUP: NavGroup = {
  key: 'settings',
  icon: '🧰',
  labelKey: 'nav.groups.settings',
  allowedRoles: ['Administrator'],
  items: [
    // Master data (from legacy /master-data/*)
    {
      key: 'settings-partners',
      labelKey: 'nav.settings.partners',
      path: '/master-data/partners',
      backendStatus: 'exists',
    },
    {
      key: 'settings-items',
      labelKey: 'nav.settings.items',
      path: '/master-data/items',
      backendStatus: 'exists',
    },
    {
      key: 'settings-items-backfill',
      labelKey: 'itemsBackfill.title',
      path: '/master-data/items/backfill',
      backendStatus: 'exists',
      existingDataHint:
        'P6.30 — Декомпозира legacy шифри во Base/Color/Size/ParentItemId. Dry-run + execute.',
    },
    {
      key: 'settings-boms',
      labelKey: 'nav.settings.boms',
      path: '/master-data/boms',
      backendStatus: 'exists',
    },
    {
      key: 'settings-routings',
      labelKey: 'nav.settings.routings',
      path: '/master-data/routings',
      backendStatus: 'exists',
    },
    {
      key: 'settings-warehouses',
      labelKey: 'nav.settings.warehouses',
      path: '/master-data/warehouses',
      backendStatus: 'exists',
    },
    {
      key: 'settings-locations',
      labelKey: 'nav.settings.locations',
      path: '/master-data/locations',
      backendStatus: 'exists',
    },
    {
      key: 'settings-uom',
      labelKey: 'nav.settings.uom',
      path: '/master-data/uom',
      backendStatus: 'exists',
    },
    {
      key: 'settings-code-lists',
      labelKey: 'nav.settings.codeLists',
      path: '/master-data/code-lists',
      backendStatus: 'exists',
    },
    {
      key: 'settings-work-centers',
      labelKey: 'nav.settings.workCenters',
      path: '/master-data/workcenters',
      backendStatus: 'exists',
    },
    {
      key: 'settings-machines',
      labelKey: 'nav.settings.machines',
      path: '/master-data/machines',
      backendStatus: 'exists',
    },
    // User / role / tenant (from legacy /admin/*)
    {
      key: 'settings-users',
      labelKey: 'nav.settings.users',
      path: '/admin/users',
      backendStatus: 'exists',
    },
    {
      key: 'settings-roles',
      labelKey: 'nav.settings.roles',
      path: '/admin/roles',
      backendStatus: 'exists',
    },
    {
      key: 'settings-tenants',
      labelKey: 'nav.settings.tenants',
      path: '/admin/tenants',
      backendStatus: 'partial',
      workPlanRef: 'P1.1',
      plannedBehavior:
        'Листа + CRUD на тенанти. Backend CRUD постои (TenantsController); UI страница е TODO.',
    },
    {
      key: 'settings-tenant-policies',
      labelKey: 'nav.settings.tenantPolicies',
      path: '/admin/tenant-settings',
      backendStatus: 'exists',
      existingDataHint:
        'P5.2.5 + I1 — toggle FEFO auto-pick + InflateImportForWaste по тенант. Персистира веднаш.',
    },
    {
      key: 'settings-audit-log',
      labelKey: 'nav.settings.auditLog',
      path: '/admin/audit-log',
      backendStatus: 'exists',
      existingDataHint: 'I8 — Admin view over GET /api/audit. Filter по entity/action/датум.',
    },
  ],
};
