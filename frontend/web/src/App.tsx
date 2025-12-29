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

const App: React.FC = () => {
  const [activeModule, setActiveModule] = useState('dashboard');

  return (
    <Router>
      <div className="app">
        <Sidebar activeModule={activeModule} setActiveModule={setActiveModule} />
        <div className="main-content">
          <Routes>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/inventory" element={<Inventory />} />
            <Route path="/production" element={<Production />} />
            <Route path="/customs" element={<Customs />} />
            <Route path="/guarantees" element={<Guarantees />} />
            <Route path="/traceability" element={<Traceability />} />
            
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
          </Routes>
        </div>
      </div>
      <ToastContainer position="top-right" autoClose={3000} />
    </Router>
  );
};

export default App;
