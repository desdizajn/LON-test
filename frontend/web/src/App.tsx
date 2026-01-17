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

          {/* Protected Routes */}
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
            <Route path="/master-data/warehouses" element={<WarehousesList />} />
            <Route path="/master-data/uom" element={<UoMList />} />
            <Route path="/master-data/boms" element={<BOMsList />} />
            <Route path="/master-data/boms/:id" element={<BOMDetail />} />
            <Route path="/master-data/routings" element={<RoutingsList />} />
            <Route path="/master-data/routings/:id" element={<RoutingDetail />} />
          </Route>
        </Routes>
      </div>
      <ToastContainer position="top-right" autoClose={3000} />
    </Router>
  );
};

export default App;
