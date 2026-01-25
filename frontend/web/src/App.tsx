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

// Advanced Features
import BatchTraceability from './pages/Advanced/BatchTraceability';
import MRNUsageTracking from './pages/Advanced/MRNUsageTracking';
import LocationInquiry from './pages/Advanced/LocationInquiry';
import ItemInquiry from './pages/Advanced/ItemInquiry';

// Protected Route Component
const ProtectedRoute: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const isAuthenticated = authService.isAuthenticated();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
};

const resolveActiveModule = (path: string) => {
  if (path.startsWith('/inventory')) return 'inventory';
  if (path.startsWith('/production')) return 'production';
  if (path.startsWith('/customs')) return 'customs';
  if (path.startsWith('/guarantees')) return 'guarantees';
  if (path.startsWith('/traceability')) return 'traceability';
  if (path.startsWith('/admin/users')) return 'admin-users';
  if (path.startsWith('/admin/employees')) return 'admin-employees';
  if (path.startsWith('/admin/shifts')) return 'admin-shifts';
  if (path.startsWith('/admin/roles')) return 'admin-roles';
  if (path.startsWith('/master-data/items')) return 'items';
  if (path.startsWith('/master-data/partners')) return 'partners';
  if (path.startsWith('/master-data/warehouses')) return 'warehouses';
  if (path.startsWith('/master-data/uom')) return 'uom';
  if (path.startsWith('/master-data/boms')) return 'boms';
  if (path.startsWith('/master-data/routings')) return 'routings';
  return 'dashboard';
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
      <div className="main-content">
        <Outlet />
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
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/inventory" element={<Inventory />} />
            <Route path="/production" element={<Production />} />
            <Route path="/customs" element={<Customs />} />
            <Route path="/guarantees" element={<Guarantees />} />
            <Route path="/traceability" element={<Traceability />} />

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

            {/* Advanced Features Routes */}
            <Route path="/advanced/batch-traceability" element={<BatchTraceability />} />
            <Route path="/advanced/mrn-usage-tracking" element={<MRNUsageTracking />} />
            <Route path="/advanced/location-inquiry" element={<LocationInquiry />} />
            <Route path="/advanced/item-inquiry" element={<ItemInquiry />} />

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

            {/* Knowledge Base Routes */}
            <Route path="/knowledge-base" element={<KnowledgeBaseChat />} />
          </Route>
        </Routes>
      </div>
      <ToastContainer position="top-right" autoClose={3000} />
    </Router>
  );
};

export default App;
