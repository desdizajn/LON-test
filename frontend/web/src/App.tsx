import React, { useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
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

const App: React.FC = () => {
  const [activeModule, setActiveModule] = useState('dashboard');

  return (
    <Router>
      <div className="app">
        <Routes>
          {/* Public Routes */}
          <Route path="/login" element={<Login />} />

          {/* Protected Routes */}
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<ProtectedRoute><Dashboard /></ProtectedRoute>} />
          <Route path="/inventory" element={<ProtectedRoute><Inventory /></ProtectedRoute>} />
          <Route path="/production" element={<ProtectedRoute><Production /></ProtectedRoute>} />
          <Route path="/customs" element={<ProtectedRoute><Customs /></ProtectedRoute>} />
          <Route path="/guarantees" element={<ProtectedRoute><Guarantees /></ProtectedRoute>} />
          <Route path="/traceability" element={<ProtectedRoute><Traceability /></ProtectedRoute>} />
          
          {/* User Management Routes */}
          <Route path="/admin/users" element={<ProtectedRoute><UserManagement /></ProtectedRoute>} />
          <Route path="/admin/employees" element={<ProtectedRoute><EmployeeManagement /></ProtectedRoute>} />
          <Route path="/admin/shifts" element={<ProtectedRoute><ShiftManagement /></ProtectedRoute>} />
          <Route path="/admin/roles" element={<ProtectedRoute><RoleManagement /></ProtectedRoute>} />
          
          {/* Master Data Routes */}
          <Route path="/master-data/items" element={<ProtectedRoute><ItemsList /></ProtectedRoute>} />
          <Route path="/master-data/items/:id" element={<ProtectedRoute><ItemDetail /></ProtectedRoute>} />
          <Route path="/master-data/partners" element={<ProtectedRoute><PartnersList /></ProtectedRoute>} />
          <Route path="/master-data/partners/:id" element={<ProtectedRoute><PartnerDetail /></ProtectedRoute>} />
          <Route path="/master-data/warehouses" element={<ProtectedRoute><WarehousesList /></ProtectedRoute>} />
          <Route path="/master-data/uom" element={<ProtectedRoute><UoMList /></ProtectedRoute>} />
          <Route path="/master-data/boms" element={<ProtectedRoute><BOMsList /></ProtectedRoute>} />
          <Route path="/master-data/boms/:id" element={<ProtectedRoute><BOMDetail /></ProtectedRoute>} />
          <Route path="/master-data/routings" element={<ProtectedRoute><RoutingsList /></ProtectedRoute>} />
          <Route path="/master-data/routings/:id" element={<ProtectedRoute><RoutingDetail /></ProtectedRoute>} />
        </Routes>
      </div>
      <ToastContainer position="top-right" autoClose={3000} />
    </Router>
  );
};

export default App;
