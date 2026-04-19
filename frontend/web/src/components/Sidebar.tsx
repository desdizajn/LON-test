import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import LanguageSwitcher from './LanguageSwitcher';

interface SidebarProps {
  activeModule: string;
  setActiveModule: (module: string) => void;
}

const Sidebar: React.FC<SidebarProps> = ({ activeModule, setActiveModule }) => {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [masterDataExpanded, setMasterDataExpanded] = useState(false);
  const [adminExpanded, setAdminExpanded] = useState(false);
  const [wmsExpanded, setWmsExpanded] = useState(false);
  const [reportsExpanded, setReportsExpanded] = useState(false);
  const [advancedExpanded, setAdvancedExpanded] = useState(false);

  const menuItems = [
    { id: 'dashboard', label: t('nav.dashboard'), icon: '📊', path: '/dashboard' },
    { id: 'inventory', label: t('nav.wmsInventory'), icon: '📦', path: '/inventory' },
    { id: 'production', label: t('nav.production'), icon: '🏭', path: '/production' },
    { id: 'customs', label: t('nav.customsMrn'), icon: '🛃', path: '/customs' },
    { id: 'guarantees', label: t('nav.guarantees'), icon: '💰', path: '/guarantees' },
    { id: 'traceability', label: t('nav.traceability'), icon: '🔍', path: '/traceability' },
    { id: 'knowledge-base', label: t('nav.knowledgeBase'), icon: '🧠', path: '/knowledge-base' },
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
    { id: 'mozni-minusi', label: '⚠️ ' + t('mozniMinusi.title'), path: '/reports/mozni-minusi' },
  ];

  const advancedSubItems = [
    { id: 'batch-traceability', label: '🔍 Batch Traceability', path: '/advanced/batch-traceability' },
    { id: 'mrn-usage-tracking', label: '🛃 MRN Usage Tracking', path: '/advanced/mrn-usage-tracking' },
    { id: 'location-inquiry', label: '📍 Location Inquiry', path: '/advanced/location-inquiry' },
    { id: 'item-inquiry', label: '📦 Item Inquiry', path: '/advanced/item-inquiry' },
    { id: 'import-wizard', label: '📥 ' + t('import.title'), path: '/tools/import' },
  ];

  const masterDataItems = [
    { id: 'items', label: 'Items', path: '/master-data/items' },
    { id: 'partners', label: 'Partners', path: '/master-data/partners' },
    { id: 'warehouses', label: '📦 Warehouses', path: '/master-data/warehouses' },
    { id: 'locations', label: '📍 Locations', path: '/master-data/locations' },
    { id: 'uom', label: 'Units of Measure', path: '/master-data/uom' },
    { id: 'boms', label: 'Bills of Materials', path: '/master-data/boms' },
    { id: 'routings', label: 'Routings', path: '/master-data/routings' },
    { id: 'code-lists', label: 'Code Lists', path: '/master-data/code-lists' },
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
        <h1>{t('app.name')}</h1>
        <p>{t('app.tagline')}</p>
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
          {t('nav.reports')}
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
          {t('nav.advancedFeatures')}
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
          {t('nav.administration')}
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
          {t('nav.masterData')}
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
      <div style={{ padding: '12px 16px', marginTop: 'auto', borderTop: '1px solid rgba(255,255,255,0.1)' }}>
        <LanguageSwitcher compact />
      </div>
    </div>
  );
};

export default Sidebar;
