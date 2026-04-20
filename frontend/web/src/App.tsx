import React, { useEffect, useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate, Outlet, useLocation } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import Dashboard from './pages/Dashboard';
import Inventory from './pages/Inventory';
import Production from './pages/Production';
import Customs from './pages/Customs';
import Guarantees from './pages/Guarantees';
import Traceability from './pages/Traceability';
import Sidebar from './components/Sidebar';
import TopBar from './components/TopBar';

// User Management
import Login from './pages/Login';
import UserManagement from './pages/UserManagement';
import EmployeeManagement from './pages/EmployeeManagement';
import ShiftManagement from './pages/ShiftManagement';
import RoleManagement from './pages/RoleManagement';
import { authService } from './services/authService';

// Master Data
import ItemsList from './pages/MasterData/Items/ItemsList';
import ItemDetail from './pages/MasterData/Items/ItemDetail';
import PartnersList from './pages/MasterData/Partners/PartnersList';
import PartnerDetail from './pages/MasterData/Partners/PartnerDetail';
import WarehousesList from './pages/MasterData/Warehouses/WarehousesList';
import UoMList from './pages/MasterData/UoM/UoMList';
import BOMsList from './pages/MasterData/BOMs/BOMsList';
import BOMDetail from './pages/MasterData/BOMs/BOMDetail';
import RoutingsList from './pages/MasterData/Routings/RoutingsList';
import RoutingDetail from './pages/MasterData/Routings/RoutingDetail';
import WarehouseList from './pages/MasterData/WarehouseList';
import WarehouseForm from './pages/MasterData/WarehouseForm';
import LocationList from './pages/MasterData/LocationList';
import LocationForm from './pages/MasterData/LocationForm';
import WorkCenterList from './pages/MasterData/WorkCenters/WorkCenterList';
import MachineList from './pages/MasterData/Machines/MachineList';

// Knowledge Base
import KnowledgeBaseChat from './pages/KnowledgeBase/KnowledgeBaseChat';
import KnowledgeBaseSearch from './pages/KnowledgeBase/KnowledgeBaseSearch';
import CodeListManagement from './pages/CodeListManagement';

// WMS Pages
import PickTaskList from './pages/WMS/PickTaskList';

// Reports
import InventoryByLocation from './pages/Reports/InventoryByLocation';
import InventoryByMRN from './pages/Reports/InventoryByMRN';
import BlockedInventory from './pages/Reports/BlockedInventory';
import InventoryByBatch from './pages/Reports/InventoryByBatch';
import MovementReports from './pages/Reports/MovementReports';
import WMSDashboard from './pages/Reports/WMSDashboard';
import CycleCountAccuracy from './pages/Reports/CycleCountAccuracy';
import WarehouseUtilization from './pages/Reports/WarehouseUtilization';
import MozniMinusi from './pages/MozniMinusi';
import ImportWizard from './pages/ImportWizard';
import Kw12Wizard from './pages/Kw12Wizard';
import ItemsBackfill from './pages/MasterData/Items/ItemsBackfill';

// Advanced Features
import BatchTraceability from './pages/Advanced/BatchTraceability';
import MRNUsageTracking from './pages/Advanced/MRNUsageTracking';
import LocationInquiry from './pages/Advanced/LocationInquiry';
import ItemInquiry from './pages/Advanced/ItemInquiry';

// P6.37 IA — placeholder component for unbuilt views
import PlaceholderPage from './components/common/PlaceholderPage';

// P5.2.7 — mass location change
import MassTransfer from './pages/Warehouse/MassTransfer';
import BulkReceiptFromDeclaration from './pages/Warehouse/BulkReceiptFromDeclaration';
import BulkShipmentFromFG from './pages/Warehouse/BulkShipmentFromFG';
import LONAuthorizationsList from './pages/Customs/LONAuthorizationsList';
import DeclarationsByType from './pages/Customs/DeclarationsByType';
import MrnDeadlines from './pages/Customs/MrnDeadlines';
import IncomingShipments from './pages/Warehouse/IncomingShipments';
import QcHold from './pages/Warehouse/QcHold';
import VarianceReport from './pages/Warehouse/VarianceReport';
import ShipmentsByStatus from './pages/Warehouse/ShipmentsByStatus';
import StockByCustomer from './pages/Warehouse/StockByCustomer';
import ShipmentsHistoryByCustomer from './pages/Warehouse/ShipmentsHistoryByCustomer';
import ScopedSearch from './pages/ScopedSearch';

// P8.1–P8.5 — production visibility
import ProductionToday from './pages/Production/ProductionToday';
import ProductionWip from './pages/Production/ProductionWip';
import ProductionCompleted from './pages/Production/ProductionCompleted';
import ProductionAtRisk from './pages/Production/ProductionAtRisk';
import ProductionShortage from './pages/Production/ProductionShortage';

// P11.1/11.2/11.4/11.5 — machine operations
import MachineStatus from './pages/Machines/MachineStatus';
import MachineDowntime from './pages/Machines/MachineDowntime';
import MaintenancePlan from './pages/Machines/MaintenancePlan';
import MaintenanceHistory from './pages/Machines/MaintenanceHistory';

// P10.1/10.2/10.5 — HR operations
import AttendanceToday from './pages/Hr/AttendanceToday';
import Absences from './pages/Hr/Absences';
import OperatorAssignment from './pages/Hr/OperatorAssignment';

// P5.2.8 — quick-entry command bar
import QuickEntry from './pages/QuickEntry';

// Admin — tenant policy settings + audit log
import TenantSettings from './pages/Admin/TenantSettings';
import AuditLog from './pages/Admin/AuditLog';

// Protected Route Component
const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const isAuthenticated = authService.isAuthenticated();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
};

/**
 * Maps the active URL to a NavItem.key from `navGroups.ts`. Best-effort — when
 * there is no exact match, we fall back to a sensible stub so the sidebar can
 * still highlight *something*. The mapping is intentionally literal: add an
 * entry here whenever a route is introduced that should show as active.
 */
const resolveActiveModule = (path: string) => {
  // Warehouse
  if (path.startsWith('/warehouse/receipts') || path.startsWith('/inventory')) return 'warehouse-receipts';
  if (path.startsWith('/warehouse/incoming')) return 'warehouse-incoming';
  if (path.startsWith('/warehouse/qc-hold')) return 'warehouse-qc-hold';
  if (path.startsWith('/warehouse/issues-today') || path.startsWith('/wms/pick-tasks')) return 'warehouse-issues-today';
  if (path.startsWith('/warehouse/transfers')) return 'warehouse-transfers';
  if (path.startsWith('/warehouse/stock-by-customer')) return 'warehouse-stock-by-customer';
  if (path.startsWith('/warehouse/variance')) return 'warehouse-variance';
  if (path.startsWith('/warehouse/ready-to-ship')) return 'warehouse-ready-to-ship';
  if (path.startsWith('/warehouse/search')) return 'warehouse-search';

  // Customs
  if (path.startsWith('/customs/authorizations')) return 'customs-authorizations';
  if (path.startsWith('/customs/import-docs') || path === '/customs') return 'customs-import-docs';
  if (path.startsWith('/customs/export-docs')) return 'customs-export-docs';
  if (path.startsWith('/customs/traceability') || path.startsWith('/traceability')) return 'customs-traceability';
  if (path.startsWith('/customs/deadlines')) return 'customs-deadlines';
  if (path.startsWith('/customs/open-items')) return 'customs-open-items';
  if (path.startsWith('/customs/guarantees') || path.startsWith('/guarantees')) return 'customs-guarantees';
  if (path.startsWith('/customs/search')) return 'customs-search';

  // Production
  if (path.startsWith('/production/today') || path === '/production') return 'production-today';
  if (path.startsWith('/production/cutting-queue')) return 'production-cutting-queue';
  if (path.startsWith('/production/sewing-queue')) return 'production-sewing-queue';
  if (path.startsWith('/production/wip')) return 'production-wip';
  if (path.startsWith('/production/at-risk')) return 'production-at-risk';
  if (path.startsWith('/production/shortage')) return 'production-shortage';
  if (path.startsWith('/production/minutes-variance')) return 'production-minutes-variance';
  if (path.startsWith('/production/rework')) return 'production-rework';
  if (path.startsWith('/production/completed')) return 'production-completed';
  if (path.startsWith('/production/search')) return 'production-search';

  // Finished goods
  if (path.startsWith('/finished/')) return path.replace('/finished/', 'finished-');

  // HR
  if (path.startsWith('/hr/employees') || path.startsWith('/admin/employees')) return 'hr-employees';
  if (path.startsWith('/hr/shifts') || path.startsWith('/admin/shifts')) return 'hr-shifts';
  if (path.startsWith('/hr/')) return path.replace('/hr/', 'hr-');

  // Machines
  if (path.startsWith('/machines/work-centers')) return 'machines-work-centers';
  if (path.startsWith('/machines/')) return path.replace('/machines/', 'machines-');

  // Finance
  if (path.startsWith('/finance/guarantees')) return 'finance-guarantees';
  if (path.startsWith('/finance/')) return path.replace('/finance/', 'finance-');

  // Management
  if (path.startsWith('/management/dashboard') || path === '/dashboard') return 'management-dashboard';
  if (path.startsWith('/management/')) return path.replace('/management/', 'management-');

  // Settings (admin + master-data)
  if (path.startsWith('/admin/users')) return 'settings-users';
  if (path.startsWith('/admin/roles')) return 'settings-roles';
  if (path.startsWith('/admin/tenant-settings')) return 'settings-tenant-policies';
  if (path.startsWith('/admin/tenants')) return 'settings-tenants';
  if (path.startsWith('/admin/audit-log')) return 'settings-audit-log';
  if (path.startsWith('/master-data/partners')) return 'settings-partners';
  if (path.startsWith('/master-data/items')) return 'settings-items';
  if (path.startsWith('/master-data/boms')) return 'settings-boms';
  if (path.startsWith('/master-data/routings')) return 'settings-routings';
  if (path.startsWith('/master-data/warehouses')) return 'settings-warehouses';
  if (path.startsWith('/master-data/locations')) return 'settings-locations';
  if (path.startsWith('/master-data/uom')) return 'settings-uom';
  if (path.startsWith('/master-data/code-lists')) return 'settings-code-lists';
  if (path.startsWith('/master-data/workcenters')) return 'settings-workcenters';
  if (path.startsWith('/master-data/machines')) return 'settings-machines';

  return 'management-dashboard';
};

const ProtectedLayout: React.FC<{
  activeModule: string;
  setActiveModule: (module: string) => void;
}> = ({ activeModule, setActiveModule }) => {
  const location = useLocation();

  useEffect(() => {
    setActiveModule(resolveActiveModule(location.pathname));
  }, [location.pathname, setActiveModule]);

  return (
    <>
      <Sidebar activeModule={activeModule} setActiveModule={setActiveModule} />
      <div className="main-content" style={{ display: 'flex', flexDirection: 'column' }}>
        <TopBar />
        <div style={{ flex: 1, overflow: 'auto' }}>
          <Outlet />
        </div>
      </div>
    </>
  );
};

const App: React.FC = () => {
  const [activeModule, setActiveModule] = useState('dashboard');

  return (
    <Router>
      <div className="app">
        <Routes>
          {/* Public Routes */}
          <Route path="/login" element={<Login />} />

          {/* Protected Routes with Layout */}
          <Route
            element={
              <ProtectedRoute>
                <ProtectedLayout activeModule={activeModule} setActiveModule={setActiveModule} />
              </ProtectedRoute>
            }
          >
            <Route path="/" element={<Navigate to="/management/dashboard" replace />} />

            {/* ──────────────── P6.37.14 — legacy-route redirects ────────────────
             * Old top-level routes kept as `<Navigate>` so bookmarks / external
             * links resolve to the canonical IA routes. Remove only after we're
             * sure no external system (docs, emails, OAuth callbacks) depends on
             * the old paths. */}
            <Route path="/dashboard" element={<Navigate to="/management/dashboard" replace />} />
            <Route path="/inventory" element={<Navigate to="/warehouse/receipts" replace />} />
            <Route path="/production" element={<Navigate to="/production/today" replace />} />
            <Route path="/customs" element={<Navigate to="/customs/import-docs" replace />} />
            <Route path="/guarantees" element={<Navigate to="/finance/guarantees" replace />} />
            <Route path="/traceability" element={<Navigate to="/customs/traceability" replace />} />

            {/* WMS Routes */}
            <Route path="/wms/pick-tasks" element={<PickTaskList />} />

            {/* Reports Routes */}
            <Route path="/reports/wms-dashboard" element={<WMSDashboard />} />
            <Route path="/reports/inventory-by-location" element={<InventoryByLocation />} />
            <Route path="/reports/inventory-by-mrn" element={<InventoryByMRN />} />
            <Route path="/reports/blocked-inventory" element={<BlockedInventory />} />
            <Route path="/reports/inventory-by-batch" element={<InventoryByBatch />} />
            <Route path="/reports/movement-reports" element={<MovementReports />} />
            <Route path="/reports/cycle-count-accuracy" element={<CycleCountAccuracy />} />
            <Route path="/reports/warehouse-utilization" element={<WarehouseUtilization />} />
            <Route path="/reports/mozni-minusi" element={<MozniMinusi />} />

            {/* P5.1 — generic importer wizard */}
            <Route path="/tools/import" element={<ImportWizard />} />
            <Route path="/tools/quick-entry" element={<QuickEntry />} />
            <Route path="/admin/tenant-settings" element={<TenantSettings />} />
            <Route path="/admin/audit-log" element={<AuditLog />} />

            {/* Advanced Features Routes */}
            <Route path="/advanced/batch-traceability" element={<BatchTraceability />} />
            <Route path="/advanced/mrn-usage-tracking" element={<MRNUsageTracking />} />
            <Route path="/advanced/location-inquiry" element={<LocationInquiry />} />
            <Route path="/advanced/item-inquiry" element={<ItemInquiry />} />

            {/* ───────── P6.37.5 — 🏭 Warehouse group (pilot) ───────── */}
            <Route path="/warehouse/receipts" element={<Inventory />} />
            <Route path="/warehouse/incoming" element={<IncomingShipments />} />
            <Route path="/warehouse/qc-hold" element={<QcHold />} />
            <Route path="/warehouse/issues-today" element={<PickTaskList />} />
            <Route path="/warehouse/transfers" element={<MassTransfer />} />
            <Route path="/warehouse/bulk-receipt" element={<BulkReceiptFromDeclaration />} />
            <Route path="/warehouse/bulk-shipment" element={<BulkShipmentFromFG />} />
            <Route path="/warehouse/stock-by-customer" element={<StockByCustomer />} />
            <Route path="/warehouse/variance" element={<VarianceReport />} />
            <Route
              path="/warehouse/ready-to-ship"
              element={
                <ShipmentsByStatus
                  title="Готово за shipment"
                  subtitle="Shipments со Status=Packed — чекаат извозна декларација или transport."
                  filterStatus={4}
                />
              }
            />
            <Route path="/warehouse/search" element={<ScopedSearch scope="warehouse" />} />

            {/* ───────── P6.37.6 — 🛃 Customs group ───────── */}
            <Route path="/customs/authorizations" element={<LONAuthorizationsList />} />
            <Route path="/customs/import-docs" element={<DeclarationsByType type="import" />} />
            <Route path="/customs/export-docs" element={<DeclarationsByType type="export" />} />
            <Route path="/customs/traceability" element={<Traceability />} />
            <Route path="/customs/deadlines" element={<MrnDeadlines />} />
            <Route path="/customs/open-items" element={<MrnDeadlines />} />
            <Route path="/customs/guarantees" element={<Guarantees />} />
            <Route path="/customs/search" element={<ScopedSearch scope="customs" />} />

            {/* ───────── P6.37.7 — ✂️ Production group ───────── */}
            <Route path="/production/today" element={<ProductionToday />} />
            <Route path="/production/orders" element={<Production />} />
            <Route
              path="/production/cutting-queue"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.production"
                  titleKey="nav.production.cuttingQueue"
                  workPlanRef="P8.6 — cutting queue"
                  backendStatus="missing"
                  plannedBehavior="Queue на налози чекаат кроење: приоритет, required material (со shortage flags), estimated minutes, allotted machine. Drag-to-reorder приоритизација."
                />
              }
            />
            <Route
              path="/production/sewing-queue"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.production"
                  titleKey="nav.production.sewingQueue"
                  workPlanRef="P8.7 — sewing queue"
                  backendStatus="missing"
                  plannedBehavior="Queue на налози во шиење: по линија / оператор / машина. WIP visibility, required operations per route, capacity check."
                />
              }
            />
            <Route path="/production/wip" element={<ProductionWip />} />
            <Route path="/production/at-risk" element={<ProductionAtRisk />} />
            <Route path="/production/shortage" element={<ProductionShortage />} />
            <Route
              path="/production/minutes-variance"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.production"
                  titleKey="nav.production.minutesVariance"
                  workPlanRef="P8.9 — standard vs actual minutes (needs piece-level time log)"
                  backendStatus="missing"
                  plannedBehavior="Routing standard минути vs actual time log: отклони по налог / оператор / линија. Критично за billing (фабриката продава минути)."
                />
              }
            />
            <Route
              path="/production/rework"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.production"
                  titleKey="nav.production.rework"
                  workPlanRef="P8.8 — rework + waste (backend exists, UI missing)"
                  backendStatus="partial"
                  plannedBehavior="Rework и waste парчиња: причина, cost impact, responsible operator/machine. P4.6 backend постои; UI недостасува."
                />
              }
            />
            <Route path="/production/completed" element={<ProductionCompleted />} />
            <Route path="/production/search" element={<ScopedSearch scope="production" />} />

            {/* ───────── P6.37.8 — 📦 Finished Goods group ───────── */}
            <Route
              path="/finished/awaiting-pack"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finishedGoods"
                  titleKey="nav.finishedGoods.awaitingPack"
                  workPlanRef="P4.X — packing queue"
                  backendStatus="missing"
                  plannedBehavior="Завршени налози кои чекаат пакување: налог, кол., стандарди за пакување, клиентски барања (box size, етикети)."
                />
              }
            />
            <Route
              path="/finished/packing"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finishedGoods"
                  titleKey="nav.finishedGoods.packing"
                  workPlanRef="P4.X — packing in progress"
                  backendStatus="missing"
                  plannedBehavior="Налози во тек на пакување: % complete, pack station, оператор. Real-time update како парчињата се пакуваат."
                />
              }
            />
            <Route
              path="/finished/ready-to-ship"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finishedGoods"
                  titleKey="nav.finishedGoods.readyToShip"
                  workPlanRef="P4.X — shipment staging"
                  backendStatus="missing"
                  plannedBehavior="Спакувани налози кои чекаат извозна декларација или transport. Pending документи + рок за pickup."
                />
              }
            />
            <Route
              path="/finished/shipped"
              element={
                <ShipmentsByStatus
                  title="Испратени"
                  subtitle="Shipments со Status=Shipped — журнал на сите испратени пратки."
                  filterStatus={5}
                />
              }
            />
            <Route
              path="/finished/pack-lists"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finishedGoods"
                  titleKey="nav.finishedGoods.packLists"
                  workPlanRef="P4.X — pack list / label generation"
                  backendStatus="missing"
                  plannedBehavior="Генерирање на pack lists + етикети во клиентски формат. Templates per клиент."
                />
              }
            />
            <Route
              path="/finished/packaging-stock"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finishedGoods"
                  titleKey="nav.finishedGoods.packagingStock"
                  workPlanRef="P1.X — packaging inventory view"
                  backendStatus="missing"
                  plannedBehavior="Состојба на паковни материјали (картони, етикети, најлон): alert кога паѓа под reorder point."
                />
              }
            />
            <Route
              path="/finished/returns"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finishedGoods"
                  titleKey="nav.finishedGoods.returns"
                  workPlanRef="P4.X — RMA flow"
                  backendStatus="missing"
                  plannedBehavior="Враќања и рекламации од клиент: причина, qty, action (rework / replace / credit note). Со царински импликации."
                />
              }
            />
            <Route path="/finished/history-by-customer" element={<ShipmentsHistoryByCustomer />} />
            <Route path="/finished/traceability" element={<Navigate to="/customs/traceability" replace />}
            />

            {/* ───────── P6.37.9 — 👥 HR group ───────── */}
            <Route path="/hr/employees" element={<EmployeeManagement />} />
            <Route path="/hr/attendance-today" element={<AttendanceToday />} />
            <Route path="/hr/shifts" element={<ShiftManagement />} />
            <Route path="/hr/absences" element={<Absences />} />
            <Route
              path="/hr/overtime"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.hr"
                  titleKey="nav.hr.overtime"
                  workPlanRef="P3.X — overtime tracking"
                  backendStatus="missing"
                  plannedBehavior="Overtime по оператор: колку, причина, approved / pending. Кумулативно за месецот за плата."
                />
              }
            />
            <Route
              path="/hr/performance"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.hr"
                  titleKey="nav.hr.performance"
                  workPlanRef="P3.X — operator performance metrics"
                  backendStatus="missing"
                  plannedBehavior="Учин по оператор: минути искористени vs стандардни, парчиња произведени, quality score. Ranking и тренд низ време."
                />
              }
            />
            <Route path="/hr/assignment" element={<OperatorAssignment />} />
            <Route
              path="/hr/training"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.hr"
                  titleKey="nav.hr.training"
                  workPlanRef="P3.X — training & certifications"
                  backendStatus="missing"
                  plannedBehavior="Сертификати на вработени per machine type / operation. Рок на важност, предупредувања пред истек."
                />
              }
            />
            <Route
              path="/hr/payroll-export"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.hr"
                  titleKey="nav.hr.payrollExport"
                  workPlanRef="P3.X — payroll hours aggregation"
                  backendStatus="missing"
                  plannedBehavior="Export на вкупни часови за плата: регуларни + overtime + bonus. Export формат компатибилен со надворешен payroll систем."
                />
              }
            />

            {/* ───────── P6.37.10 — ⚙️ Machines / Work Centers / Efficiency ───────── */}
            <Route path="/machines/status" element={<MachineStatus />} />
            <Route path="/machines/work-centers" element={<WorkCenterList />} />
            <Route path="/machines/downtime" element={<MachineDowntime />} />
            <Route
              path="/machines/oee"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.machines"
                  titleKey="nav.machines.oee"
                  workPlanRef="P11.3 — OEE (depends on P11.1 + P11.2 + P8.9 time log)"
                  backendStatus="missing"
                  plannedBehavior="OEE (Availability × Performance × Quality) по машина / линија / смена. Benchmark vs target + тренд низ време."
                />
              }
            />
            <Route path="/machines/maintenance-plan" element={<MaintenancePlan />} />
            <Route path="/machines/maintenance-history" element={<MaintenanceHistory />} />
            <Route
              path="/machines/capacity"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.machines"
                  titleKey="nav.machines.capacity"
                  workPlanRef="P3.X — capacity utilization"
                  backendStatus="missing"
                  plannedBehavior="Искористеност: планиран vs actual часови per машина / линија / смена. Daily / weekly / monthly rollup."
                />
              }
            />
            <Route
              path="/machines/setup-time"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.machines"
                  titleKey="nav.machines.setupTime"
                  workPlanRef="P3.X — setup time analysis"
                  backendStatus="missing"
                  plannedBehavior="Changeover минути per машина: колку време се троши на setup/switchover. Идентификација на bottleneck setup-и."
                />
              }
            />
            <Route
              path="/machines/bottleneck"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.machines"
                  titleKey="nav.machines.bottleneck"
                  workPlanRef="P3.X — bottleneck analysis"
                  backendStatus="missing"
                  plannedBehavior="Constraint view за денешниот план: која машина е bottleneck? Што чекаат нејзе? Предложени corrective actions."
                />
              }
            />

            {/* ───────── P6.37.11 — 💵 Finance group ───────── */}
            <Route
              path="/finance/invoicing"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.invoicing"
                  workPlanRef="P5.X — invoicing flow"
                  backendStatus="missing"
                  plannedBehavior="AR tracking: за фактурирање (завршени shipments), фактурирано, платено, overdue. Со aging buckets 0-30-60-90."
                />
              }
            />
            <Route
              path="/finance/contracts"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.contracts"
                  workPlanRef="P5.X — customer contracts + rates"
                  backendStatus="missing"
                  plannedBehavior="Клиентски договори: per-минута / per-парче rates, срок на важност, specijalni conditions. Authoritative source за invoicing."
                />
              }
            />
            <Route path="/finance/guarantees" element={<Guarantees />} />
            <Route
              path="/finance/cost-accounting"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.costAccounting"
                  workPlanRef="P5.X — cost per minute"
                  backendStatus="missing"
                  plannedBehavior="Cost accounting: чинење на минута на машина × оператор × shift. Support за pricing decisions."
                />
              }
            />
            <Route
              path="/finance/margin"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.margin"
                  workPlanRef="P5.X — margin analysis"
                  backendStatus="missing"
                  plannedBehavior="Маржа по клиент / налог: revenue (invoiced rate × qty) минус cost (actual minutes × cost rate). Preview пред endgame."
                />
              }
            />
            <Route
              path="/finance/ap"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.ap"
                  workPlanRef="P5.X — accounts payable"
                  backendStatus="missing"
                  plannedBehavior="Добавувач invoices: packaging, energy, spare parts. Отворени / платени, due dates."
                />
              }
            />
            <Route
              path="/finance/payroll"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.payroll"
                  workPlanRef="P3.X — payroll aggregate from HR"
                  backendStatus="missing"
                  plannedBehavior="Aggregate плати за месецот: вкупно часови регуларни + overtime + bonus × шифри. Feed од /hr/payroll-export."
                />
              }
            />
            <Route
              path="/finance/pnl"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.pnl"
                  workPlanRef="P5.X — P&L preview"
                  backendStatus="missing"
                  plannedBehavior="Месечен P&L preview: revenue, direct cost, overhead, margin. Со compare to target."
                />
              }
            />
            <Route
              path="/finance/cash-flow"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.cashFlow"
                  workPlanRef="P5.X — cash flow forecast"
                  backendStatus="missing"
                  plannedBehavior="30 / 60 / 90 day forecast: expected AR inflow, AP outflow, пресек. Risk flags."
                />
              }
            />
            <Route
              path="/finance/reports"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.finance"
                  titleKey="nav.finance.reports"
                  workPlanRef="P5.X — financial reports"
                  backendStatus="missing"
                  plannedBehavior="Сметководствени извештаи за управа, ревизор, финансиско: BS, IS, CF, статутарни формати."
                />
              }
            />

            {/* ───────── P6.37.12 — 🎯 Management (KPI) group ───────── */}
            <Route path="/management/dashboard" element={<Dashboard />} />
            <Route
              path="/management/on-time"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.onTime"
                  workPlanRef="P5.X — on-time delivery metric"
                  backendStatus="missing"
                  plannedBehavior="On-time delivery % по клиент / период. Distribution: on-time / late by <7d / late by >7d."
                />
              }
            />
            <Route
              path="/management/capacity"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.capacity"
                  workPlanRef="P3.X — capacity rollup"
                  backendStatus="missing"
                  plannedBehavior="Capacity utilization: машини + оператори. Planned hours vs actual hours. Weekly / monthly."
                />
              }
            />
            <Route
              path="/management/by-customer"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.byCustomer"
                  workPlanRef="P5.X — production by customer"
                  backendStatus="missing"
                  plannedBehavior="Aggregated: парчиња, минути, маржа по клиент за период. Ranked list + тренд."
                />
              }
            />
            <Route
              path="/management/margin"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.margin"
                  workPlanRef="P5.X — margin per customer (mgr lens)"
                  backendStatus="missing"
                  plannedBehavior="Management lens на margin: не detailed financial но trend + alerts за клиенти со падната маржа."
                />
              }
            />
            <Route
              path="/management/alerts"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.alerts"
                  workPlanRef="P6.X — exception alerts"
                  backendStatus="missing"
                  plannedBehavior="Активни exceptions кои бараат управување: shortage, overdue налог, истекуван MRN, gaps во capacity."
                />
              }
            />
            <Route
              path="/management/risks"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.risks"
                  workPlanRef="P6.X — open risks register"
                  backendStatus="missing"
                  plannedBehavior="Отворени ризици: царински (деадлине), operational (машина, кадар), финансиски (клиент overdue), legal."
                />
              }
            />
            <Route
              path="/management/trends"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.trends"
                  workPlanRef="P6.X — 3M / 6M / 12M trends"
                  backendStatus="missing"
                  plannedBehavior="Тренд analysis на ключни metrics: production volume, margin, on-time, headcount. Compare year-over-year."
                />
              }
            />
            <Route
              path="/management/escalations"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.escalations"
                  workPlanRef="P6.X — escalations"
                  backendStatus="missing"
                  plannedBehavior="Налози што бараат управувачка одлука: доцнат, клиент тражи extension, legal issue, budget override."
                />
              }
            />
            <Route
              path="/management/client-scorecard"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.clientScorecard"
                  workPlanRef="P5.X — per-client scorecard"
                  backendStatus="missing"
                  plannedBehavior="Client scorecard: volume, margin, on-time, payment behavior, риск rating. Помага за renewal / expansion одлуки."
                />
              }
            />
            <Route
              path="/management/monthly-pack"
              element={
                <PlaceholderPage
                  groupKey="nav.groups.management"
                  titleKey="nav.management.monthlyPack"
                  workPlanRef="P6.X — monthly review pack"
                  backendStatus="missing"
                  plannedBehavior="Printable / exportable pack: exec summary, key metrics, wins/losses, next month focus. За board meetings."
                />
              }
            />

            {/* User Management Routes */}
            <Route path="/admin/users" element={<UserManagement />} />
            <Route path="/admin/employees" element={<EmployeeManagement />} />
            <Route path="/admin/shifts" element={<ShiftManagement />} />
            <Route path="/admin/roles" element={<RoleManagement />} />

            {/* Master Data Routes */}
            <Route path="/master-data/items" element={<ItemsList />} />
            <Route path="/master-data/items/:id" element={<ItemDetail />} />
            <Route path="/master-data/partners" element={<PartnersList />} />
            <Route path="/master-data/partners/:id" element={<PartnerDetail />} />
            <Route path="/master-data/warehouses-old" element={<WarehousesList />} />
            <Route path="/master-data/warehouses" element={<WarehouseList />} />
            <Route path="/master-data/warehouses/:id" element={<WarehouseForm />} />
            <Route path="/master-data/locations" element={<LocationList />} />
            <Route path="/master-data/locations/:id" element={<LocationForm />} />
            <Route path="/master-data/workcenters" element={<WorkCenterList />} />
            <Route path="/master-data/machines" element={<MachineList />} />
            <Route path="/master-data/uom" element={<UoMList />} />
            <Route path="/master-data/boms" element={<BOMsList />} />
            <Route path="/master-data/boms/:id" element={<BOMDetail />} />
            <Route path="/master-data/routings" element={<RoutingsList />} />
            <Route path="/master-data/routings/:id" element={<RoutingDetail />} />
            <Route path="/master-data/code-lists" element={<CodeListManagement />} />

            {/* Knowledge Base Routes */}
            <Route path="/knowledge-base" element={<KnowledgeBaseChat />} />
            <Route path="/knowledge-base/search" element={<KnowledgeBaseSearch />} />

            {/* P6.38 — Import presets + admin backfills */}
            <Route path="/tools/import/kw12" element={<Kw12Wizard />} />
            <Route path="/master-data/items/backfill" element={<ItemsBackfill />} />
          </Route>
        </Routes>
      </div>
      <ToastContainer position="top-right" autoClose={3000} />
    </Router>
  );
};

export default App;
