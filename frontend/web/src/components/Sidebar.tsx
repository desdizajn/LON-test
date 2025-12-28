import React from 'react';
import { useNavigate } from 'react-router-dom';

interface SidebarProps {
  activeModule: string;
  setActiveModule: (module: string) => void;
}

const Sidebar: React.FC<SidebarProps> = ({ activeModule, setActiveModule }) => {
  const navigate = useNavigate();

  const menuItems = [
    { id: 'dashboard', label: 'Dashboard', icon: '📊' },
    { id: 'inventory', label: 'WMS & Inventory', icon: '📦' },
    { id: 'production', label: 'Production (LON)', icon: '🏭' },
    { id: 'customs', label: 'Customs & MRN', icon: '🛃' },
    { id: 'guarantees', label: 'Guarantees', icon: '💰' },
    { id: 'traceability', label: 'Traceability', icon: '🔍' },
  ];

  const handleMenuClick = (id: string) => {
    setActiveModule(id);
    navigate(`/${id}`);
  };

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
            onClick={() => handleMenuClick(item.id)}
          >
            <span style={{ marginRight: '10px' }}>{item.icon}</span>
            {item.label}
          </li>
        ))}
      </ul>
    </div>
  );
};

export default Sidebar;
