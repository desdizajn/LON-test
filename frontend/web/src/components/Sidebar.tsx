import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';

interface SidebarProps {
  activeModule: string;
  setActiveModule: (module: string) => void;
}

const Sidebar: React.FC<SidebarProps> = ({ activeModule, setActiveModule }) => {
  const navigate = useNavigate();
  const [masterDataExpanded, setMasterDataExpanded] = useState(false);
  const [adminExpanded, setAdminExpanded] = useState(false);
  const [wmsExpanded, setWmsExpanded] = useState(false);
  const [reportsExpanded, setReportsExpanded] = useState(false);
  const [advancedExpanded, setAdvancedExpanded] = useState(false);

  const menuItems = [
    { id: 'dashboard', label: 'Dashboard', icon: '📊', path: '/dashboard' },
    { id: 'inventory', label: 'WMS & Inventory', icon: '📦', path: '/inventory' },
    { id: 'production', label: 'Production (LON)', icon: '🏭', path: '/production' },
    { id: 'customs', label: 'Customs & MRN', icon: '🛃', path: '/customs' },
    { id: 'guarantees', label: 'Guarantees', icon: '💰', path: '/guarantees' },
    { id: 'traceability', label: 'Traceability', icon: '🔍', path: '/traceability' },
    { id: 'knowledge-base', label: 'Knowledge Base', icon: '🧠', path: '/knowledge-base' },
  ];

  const wmsSubItems = [
    { id: 'pick-tasks', label: 'Pick Tasks', path: '/wms/pick-tasks' },
  ];

  const reportsSubItems = [
    { id: 'wms-dashboard', label: '📊 WMS Dashboard', path: '/reports/wms-dashboard' },
    { id: 'inventory-by-location', label: '📍 Inventory by Location', path: '/reports/inventory-by-location' },
    { id: 'inventory-by-mrn', label: '🛃 Inventory by MRN', path: '/reports/inventory-by-mrn' },
    { id: 'blocked-inventory', label: '🔒 Blocked Inventory', path: '/reports/blocked-inventory' },
    { id: 'inventory-by-batch', label: '📦 Inventory by Batch', path: '/reports/inventory-by-batch' },
    { id: 'movement-reports', label: '📈 Movement Reports', path: '/reports/movement-reports' },
    { id: 'cycle-count-accuracy', label: '🎯 Cycle Count Accuracy', path: '/reports/cycle-count-accuracy' },
    { id: 'warehouse-utilization', label: '🏭 Warehouse Utilization', path: '/reports/warehouse-utilization' },
  ];

  const advancedSubItems = [
    { id: 'batch-traceability', label: '🔍 Batch Traceability', path: '/advanced/batch-traceability' },
    { id: 'mrn-usage-tracking', label: '🛃 MRN Usage Tracking', path: '/advanced/mrn-usage-tracking' },
    { id: 'location-inquiry', label: '📍 Location Inquiry', path: '/advanced/location-inquiry' },
    { id: 'item-inquiry', label: '📦 Item Inquiry', path: '/advanced/item-inquiry' },
  ];

  const masterDataItems = [
    { id: 'items', label: 'Items', path: '/master-data/items' },
    { id: 'partners', label: 'Partners', path: '/master-data/partners' },
    { id: 'warehouses', label: '📦 Warehouses', path: '/master-data/warehouses' },
    { id: 'locations', label: '📍 Locations', path: '/master-data/locations' },
    { id: 'uom', label: 'Units of Measure', path: '/master-data/uom' },
    { id: 'boms', label: 'Bills of Materials', path: '/master-data/boms' },
    { id: 'routings', label: 'Routings', path: '/master-data/routings' },
  ];

  const adminItems = [
    { id: 'admin-users', label: 'Users', path: '/admin/users' },
    { id: 'admin-employees', label: 'Employees', path: '/admin/employees' },
    { id: 'admin-shifts', label: 'Shifts', path: '/admin/shifts' },
    { id: 'admin-roles', label: 'Roles', path: '/admin/roles' },
  ];

  const handleMenuClick = (id: string, path: string) => {
    setActiveModule(id);
    navigate(path);
  };

  const toggleMasterData = () => {
    setMasterDataExpanded(!masterDataExpanded);
  };

  const toggleAdmin = () => {
    setAdminExpanded(!adminExpanded);
  };

  const toggleWMS = () => {
    setWmsExpanded(!wmsExpanded);
  };

  const toggleReports = () => {
    setReportsExpanded(!reportsExpanded);
  };

  const toggleAdvanced = () => {
    setAdvancedExpanded(!advancedExpanded);
  };

  useEffect(() => {
    if (activeModule.startsWith('admin-')) {
      setAdminExpanded(true);
    }
    if (['items', 'partners', 'warehouses', 'locations', 'uom', 'boms', 'routings'].includes(activeModule)) {
      setMasterDataExpanded(true);
    }
  }, [activeModule]);

  return (
    <div className="sidebar">
      <div className="sidebar-header">
        <h1>LON System</h1>
        <p>Production + WMS + Customs</p>
      </div>
      <ul className="nav">
        {menuItems.map(item => (
          <li
            key={item.id}
            className={activeModule === item.id ? 'active' : ''}
            onClick={() => handleMenuClick(item.id, item.path)}
          >
            <span style={{ marginRight: '10px' }}>{item.icon}</span>
            {item.label}
            {item.id === 'inventory' && (
              <span
                style={{ marginLeft: 'auto', cursor: 'pointer' }}
                onClick={(e) => {
                  e.stopPropagation();
                  toggleWMS();
                }}
              >
                {wmsExpanded ? '▼' : '▶'}
              </span>
            )}
          </li>
        ))}
        
        {wmsExpanded && (
          <ul className="submenu">
            {wmsSubItems.map(item => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleMenuClick(item.id, item.path);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}
        
        <li className="menu-section" onClick={toggleReports}>
          <span style={{ marginRight: '10px' }}>📊</span>
          Reports
          <span style={{ marginLeft: 'auto' }}>{reportsExpanded ? '▼' : '▶'}</span>
        </li>
        
        {reportsExpanded && (
          <ul className="submenu">
            {reportsSubItems.map(item => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleMenuClick(item.id, item.path);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}
        
        <li className="menu-section" onClick={toggleAdvanced}>
          <span style={{ marginRight: '10px' }}>🚀</span>
          Advanced Features
          <span style={{ marginLeft: 'auto' }}>{advancedExpanded ? '▼' : '▶'}</span>
        </li>
        
        {advancedExpanded && (
          <ul className="submenu">
            {advancedSubItems.map(item => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleMenuClick(item.id, item.path);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}

        <li className="menu-section" onClick={toggleAdmin}>
          <span style={{ marginRight: '10px' }}>🧑‍💼</span>
          Administration
          <span style={{ marginLeft: 'auto' }}>{adminExpanded ? '▼' : '▶'}</span>
        </li>

        {adminExpanded && (
          <ul className="submenu">
            {adminItems.map(item => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleMenuClick(item.id, item.path);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}
        
        <li className="menu-section" onClick={toggleMasterData}>
          <span style={{ marginRight: '10px' }}>⚙️</span>
          Master Data
          <span style={{ marginLeft: 'auto' }}>{masterDataExpanded ? '▼' : '▶'}</span>
        </li>
        
        {masterDataExpanded && (
          <ul className="submenu">
            {masterDataItems.map(item => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleMenuClick(item.id, item.path);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}
      </ul>
    </div>
  );
};

export default Sidebar;
