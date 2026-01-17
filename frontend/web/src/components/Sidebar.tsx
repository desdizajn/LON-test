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

  const menuItems = [
    { id: 'dashboard', label: 'Dashboard', icon: '📊', path: '/dashboard' },
    { id: 'inventory', label: 'WMS & Inventory', icon: '📦', path: '/inventory' },
    { id: 'production', label: 'Production (LON)', icon: '🏭', path: '/production' },
    { id: 'customs', label: 'Customs & MRN', icon: '🛃', path: '/customs' },
    { id: 'guarantees', label: 'Guarantees', icon: '💰', path: '/guarantees' },
    { id: 'traceability', label: 'Traceability', icon: '🔍', path: '/traceability' },
  ];

  const masterDataItems = [
    { id: 'items', label: 'Items', path: '/master-data/items' },
    { id: 'partners', label: 'Partners', path: '/master-data/partners' },
    { id: 'warehouses', label: 'Warehouses', path: '/master-data/warehouses' },
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

  useEffect(() => {
    if (activeModule.startsWith('admin-')) {
      setAdminExpanded(true);
    }
    if (['items', 'partners', 'warehouses', 'uom', 'boms', 'routings'].includes(activeModule)) {
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
          </li>
        ))}

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
