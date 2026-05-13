import React, { useEffect, useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate, Outlet, useLocation } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import theme from './theme';
import ErrorBoundary from './components/common/ErrorBoundary';
import Dashboard from './pages/Dashboard';
import Inventory from './pages/Inventory';
import Production from './pages/Production';
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


// P5.2.7 — mass location change
import MassTransfer from './pages/Warehouse/MassTransfer';
import BulkReceiptFromDeclaration from './pages/Warehouse/BulkReceiptFromDeclaration';
import BulkShipmentFromFG from './pages/Warehouse/BulkShipmentFromFG';
import LONAuthorizationsList from './pages/Customs/LONAuthorizationsList';
import DeclarationsByType from './pages/Customs/DeclarationsByType';
import MrnDeadlines from './pages/Customs/MrnDeadlines';
import IncomingShipments from './pages/Warehouse/IncomingShipments';
import QcHold from './pages/Warehouse/QcHold';
import Skart from './pages/Warehouse/Skart';
import Podelba from './pages/Warehouse/Podelba';
import DeliveryNotes from './pages/Warehouse/DeliveryNotes';
import DeliveryNoteDetail from './pages/Warehouse/DeliveryNoteDetail';
import CommercialInvoiceList from './pages/Customs/CommercialInvoiceList';
import CommercialInvoiceDetail from './pages/Customs/CommercialInvoiceDetail';
import TariffBrowser from './pages/MasterData/TariffCodes/TariffBrowser';
import SizeBreakdown from './pages/Production/SizeBreakdown';
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

// P9.1/9.6 — Finished Goods simple queries
import AwaitingPack from './pages/FinishedGoods/AwaitingPack';
import PackagingStock from './pages/FinishedGoods/PackagingStock';

// P5.2.8 — quick-entry command bar
import QuickEntry from './pages/QuickEntry';

// Admin — tenant policy settings + audit log
import TenantSettings from './pages/Admin/TenantSettings';
import TenantList from './pages/Admin/TenantList';
import AuditLog from './pages/Admin/AuditLog';

// P12.2 / P12.3 — Finance
import ClientContracts from './pages/Finance/ClientContracts';
import Invoicing from './pages/Finance/Invoicing';
// P12.4–P12.10 — Finance extensions (placeholder-to-real conversion)
import FinanceMargin from './pages/Finance/FinanceMargin';
import FinanceReports from './pages/Finance/FinanceReports';
import PnLPreview from './pages/Finance/PnLPreview';
import CashFlow from './pages/Finance/CashFlow';
import CostAccounting from './pages/Finance/CostAccounting';
import SupplierInvoices from './pages/Finance/SupplierInvoices';
import PayrollAggregate from './pages/Finance/PayrollAggregate';

// P13.1 / P13.3 / P13.5 — Management KPIs
import OnTimeDelivery from './pages/Management/OnTimeDelivery';
import ByCustomer from './pages/Management/ByCustomer';
import Alerts from './pages/Management/Alerts';
// P13.2 / P13.4 / P13.6–P13.10 — Management extensions
import CapacityUtilization from './pages/Management/CapacityUtilization';
import MarginByCustomer from './pages/Management/MarginByCustomer';
import OpenRisks from './pages/Management/OpenRisks';
import Trends from './pages/Management/Trends';
import Escalations from './pages/Management/Escalations';
import ClientScorecard from './pages/Management/ClientScorecard';
import MonthlyPack from './pages/Management/MonthlyPack';

// HR extensions
import Overtime from './pages/Hr/Overtime';
import Performance from './pages/Hr/Performance';
import Training from './pages/Hr/Training';
import PayrollExport from './pages/Hr/PayrollExport';

// Machines extensions
import MachineOEE from './pages/Machines/MachineOEE';
import MachineCapacity from './pages/Machines/MachineCapacity';
import SetupTime from './pages/Machines/SetupTime';
import Bottleneck from './pages/Machines/Bottleneck';

// Production + Finished Goods extensions
import OperationQueue from './pages/Production/CuttingQueue';
import MinutesVariance from './pages/Production/MinutesVariance';
import Rework from './pages/Production/Rework';
import Packing from './pages/FinishedGoods/Packing';
import PackLists from './pages/FinishedGoods/PackLists';
import Returns from './pages/FinishedGoods/Returns';

// Phase 17 §E2 — ClientOrder hub
import OrderList from './pages/Orders/OrderList';
import OrderHub from './pages/Orders/OrderHub';

import { LayoutProvider } from './components/layout/LayoutContext';

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
  // Phase 17 §E2 — Orders hub
  if (path.startsWith('/orders')) return 'orders-list';

  // Warehouse
  if (path.startsWith('/warehouse/receipts') || path.startsWith('/inventory')) return 'warehouse-receipts';
  if (path.startsWith('/warehouse/incoming')) return 'warehouse-incoming';
  if (path.startsWith('/warehouse/qc-hold')) return 'warehouse-qc-hold';
  if (path.startsWith('/warehouse/skart')) return 'warehouse-skart';
  if (path.startsWith('/warehouse/podelba')) return 'warehouse-podelba';
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
  if (path.startsWith('/master-data/tariff-codes')) return 'settings-tariff-codes';
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
    <LayoutProvider>
      <Sidebar activeModule={activeModule} setActiveModule={setActiveModule} />
      <div className="main-content">
        <TopBar />
        <div style={{ flex: 1, overflow: 'auto' }}>
          <ErrorBoundary routeLabel={location.pathname} key={location.pathname}>
            <Outlet />
          </ErrorBoundary>
        </div>
      </div>
    </LayoutProvider>
  );
};

// P16.B1 — shared QueryClient. staleTime 30s keeps mutations from
// triggering avalanches; refetchOnWindowFocus picks up cross-tab edits.
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: true,
      retry: 1,
    },
  },
});

const App: React.FC = () => {
  const [activeModule, setActiveModule] = useState('dashboard');

  return (
    <QueryClientProvider client={queryClient}>
    <ThemeProvider theme={theme}>
    <CssBaseline />
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

            {/* ───────── Phase 17 §E2 — ClientOrder hub ───────── */}
            <Route path="/orders" element={<OrderList />} />
            <Route path="/orders/:id" element={<OrderHub />} />

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
            <Route path="/admin/tenants" element={<TenantList />} />
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
            <Route path="/warehouse/skart" element={<Skart />} />
            <Route path="/warehouse/podelba" element={<Podelba />} />
            <Route path="/warehouse/delivery-notes" element={<DeliveryNotes />} />
            <Route path="/warehouse/delivery-notes/:id" element={<DeliveryNoteDetail />} />
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
            {/* Phase 17 §E8.5 — CommercialInvoice (D4 — new entity). */}
            <Route path="/customs/commercial-invoices" element={<CommercialInvoiceList />} />
            <Route path="/customs/commercial-invoices/:id" element={<CommercialInvoiceDetail />} />

            {/* ───────── P6.37.7 — ✂️ Production group ───────── */}
            <Route path="/production/today" element={<ProductionToday />} />
            <Route path="/production/orders" element={<Production />} />
            <Route path="/production/cutting-queue" element={<OperationQueue operationType="cutting" />} />
            <Route path="/production/sewing-queue" element={<OperationQueue operationType="sewing" />} />
            <Route path="/production/wip" element={<ProductionWip />} />
            <Route path="/production/at-risk" element={<ProductionAtRisk />} />
            <Route path="/production/shortage" element={<ProductionShortage />} />
            <Route path="/production/minutes-variance" element={<MinutesVariance />} />
            <Route path="/production/rework" element={<Rework />} />
            <Route path="/production/size-breakdown" element={<SizeBreakdown />} />
            <Route path="/production/completed" element={<ProductionCompleted />} />
            <Route path="/production/search" element={<ScopedSearch scope="production" />} />

            {/* ───────── P6.37.8 — 📦 Finished Goods group ───────── */}
            <Route path="/finished/awaiting-pack" element={<AwaitingPack />} />
            <Route path="/finished/packing" element={<Packing />} />
            <Route
              path="/finished/ready-to-ship"
              element={
                <ShipmentsByStatus
                  title="Готови за испорака"
                  subtitle="Shipments со Status=Packed — pending извозна декларација или transport."
                  filterStatus={4}
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
            <Route path="/finished/pack-lists" element={<PackLists />} />
            <Route path="/finished/packaging-stock" element={<PackagingStock />} />
            <Route path="/finished/returns" element={<Returns />} />
            <Route path="/finished/history-by-customer" element={<ShipmentsHistoryByCustomer />} />
            <Route path="/finished/traceability" element={<Navigate to="/customs/traceability" replace />}
            />

            {/* ───────── P6.37.9 — 👥 HR group ───────── */}
            <Route path="/hr/employees" element={<EmployeeManagement />} />
            <Route path="/hr/attendance-today" element={<AttendanceToday />} />
            <Route path="/hr/shifts" element={<ShiftManagement />} />
            <Route path="/hr/absences" element={<Absences />} />
            <Route path="/hr/overtime" element={<Overtime />} />
            <Route path="/hr/performance" element={<Performance />} />
            <Route path="/hr/assignment" element={<OperatorAssignment />} />
            <Route path="/hr/training" element={<Training />} />
            <Route path="/hr/payroll-export" element={<PayrollExport />} />

            {/* ───────── P6.37.10 — ⚙️ Machines / Work Centers / Efficiency ───────── */}
            <Route path="/machines/status" element={<MachineStatus />} />
            <Route path="/machines/work-centers" element={<WorkCenterList />} />
            <Route path="/machines/downtime" element={<MachineDowntime />} />
            <Route path="/machines/oee" element={<MachineOEE />} />
            <Route path="/machines/maintenance-plan" element={<MaintenancePlan />} />
            <Route path="/machines/maintenance-history" element={<MaintenanceHistory />} />
            <Route path="/machines/capacity" element={<MachineCapacity />} />
            <Route path="/machines/setup-time" element={<SetupTime />} />
            <Route path="/machines/bottleneck" element={<Bottleneck />} />

            {/* ───────── P6.37.11 — 💵 Finance group ───────── */}
            {/* P12.2 — invoicing MVP (shipped) */}
            <Route path="/finance/invoicing" element={<Invoicing />} />
            {/* P12.3 — client contracts + rate cards (shipped) */}
            <Route path="/finance/contracts" element={<ClientContracts />} />
            <Route path="/finance/guarantees" element={<Guarantees />} />
            <Route path="/finance/cost-accounting" element={<CostAccounting />} />
            <Route path="/finance/margin" element={<FinanceMargin />} />
            <Route path="/finance/ap" element={<SupplierInvoices />} />
            <Route path="/finance/payroll" element={<PayrollAggregate />} />
            <Route path="/finance/pnl" element={<PnLPreview />} />
            <Route path="/finance/cash-flow" element={<CashFlow />} />
            <Route path="/finance/reports" element={<FinanceReports />} />

            {/* ───────── P6.37.12 — 🎯 Management (KPI) group ───────── */}
            <Route path="/management/dashboard" element={<Dashboard />} />
            <Route path="/management/on-time" element={<OnTimeDelivery />} />
            <Route path="/management/capacity" element={<CapacityUtilization />} />
            <Route path="/management/by-customer" element={<ByCustomer />} />
            <Route path="/management/margin" element={<MarginByCustomer />} />
            <Route path="/management/alerts" element={<Alerts />} />
            <Route path="/management/risks" element={<OpenRisks />} />
            <Route path="/management/trends" element={<Trends />} />
            <Route path="/management/escalations" element={<Escalations />} />
            <Route path="/management/client-scorecard" element={<ClientScorecard />} />
            <Route path="/management/monthly-pack" element={<MonthlyPack />} />

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
            <Route path="/master-data/warehouses" element={<WarehouseList />} />
            <Route path="/master-data/warehouses/:id" element={<WarehouseForm />} />
            <Route path="/master-data/locations" element={<LocationList />} />
            <Route path="/master-data/locations/:id" element={<LocationForm />} />
            <Route path="/master-data/workcenters" element={<WorkCenterList />} />
            <Route path="/master-data/machines" element={<MachineList />} />
            <Route path="/master-data/uom" element={<UoMList />} />
            <Route path="/master-data/tariff-codes" element={<TariffBrowser />} />
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
    </ThemeProvider>
    {process.env.NODE_ENV === 'development' && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
};

export default App;
