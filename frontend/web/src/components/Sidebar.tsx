import React, { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import LanguageSwitcher from './LanguageSwitcher';
import { useNavForRoles } from '../nav/useNavForRoles';
import { NavGroup, NavGroupKey } from '../nav/types';

interface SidebarProps {
  activeModule: string;
  setActiveModule: (module: string) => void;
}

/** localStorage key remembering which nav groups are expanded. */
const EXPAND_STATE_KEY = 'lon.nav.expandedGroups';

const loadExpandedState = (): Record<string, boolean> => {
  try {
    const raw = localStorage.getItem(EXPAND_STATE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
};

const saveExpandedState = (state: Record<string, boolean>): void => {
  try {
    localStorage.setItem(EXPAND_STATE_KEY, JSON.stringify(state));
  } catch {
    // localStorage may be unavailable (private mode) — ignore.
  }
};

const Sidebar: React.FC<SidebarProps> = ({ activeModule, setActiveModule }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useTranslation();

  const navGroups = useNavForRoles();

  // Expand-state per group, persisted to localStorage.
  const [expanded, setExpanded] = useState<Record<string, boolean>>(() =>
    loadExpandedState()
  );

  // Legacy sidebar expansion (top-down from the previous design).
  const [masterDataExpanded, setMasterDataExpanded] = useState(false);
  const [adminExpanded, setAdminExpanded] = useState(false);
  const [reportsExpanded, setReportsExpanded] = useState(false);
  const [advancedExpanded, setAdvancedExpanded] = useState(false);

  /** Auto-expand the group that contains the currently-active route. */
  useEffect(() => {
    const matchGroup = navGroups.find((g) =>
      g.items.some((i) => location.pathname.startsWith(i.path))
    );
    if (matchGroup && !expanded[matchGroup.key]) {
      const next = { ...expanded, [matchGroup.key]: true };
      setExpanded(next);
      saveExpandedState(next);
    }
  }, [location.pathname, navGroups, expanded]);

  /** Legacy sidebar auto-expand keeping compat with the old activeModule prop. */
  useEffect(() => {
    if (activeModule.startsWith('admin-')) setAdminExpanded(true);
    if (
      ['items', 'partners', 'warehouses', 'locations', 'uom', 'boms', 'routings'].includes(
        activeModule
      )
    ) {
      setMasterDataExpanded(true);
    }
  }, [activeModule]);

  const toggleGroup = (key: NavGroupKey) => {
    const next = { ...expanded, [key]: !expanded[key] };
    setExpanded(next);
    saveExpandedState(next);
  };

  const handleNavigate = (path: string, key: string) => {
    setActiveModule(key);
    navigate(path);
  };

  /** Legacy submenu items — kept alive until P6.37.14 cutover. */
  const legacyItems = useMemo(
    () => ({
      wms: [{ id: 'pick-tasks', label: 'Pick Tasks', path: '/wms/pick-tasks' }],
      reports: [
        { id: 'wms-dashboard', label: '📊 WMS Dashboard', path: '/reports/wms-dashboard' },
        {
          id: 'inventory-by-location',
          label: '📍 Inventory by Location',
          path: '/reports/inventory-by-location',
        },
        { id: 'inventory-by-mrn', label: '🛃 Inventory by MRN', path: '/reports/inventory-by-mrn' },
        { id: 'blocked-inventory', label: '🔒 Blocked Inventory', path: '/reports/blocked-inventory' },
        { id: 'inventory-by-batch', label: '📦 Inventory by Batch', path: '/reports/inventory-by-batch' },
        { id: 'movement-reports', label: '📈 Movement Reports', path: '/reports/movement-reports' },
        { id: 'cycle-count-accuracy', label: '🎯 Cycle Count Accuracy', path: '/reports/cycle-count-accuracy' },
        {
          id: 'warehouse-utilization',
          label: '🏭 Warehouse Utilization',
          path: '/reports/warehouse-utilization',
        },
        { id: 'mozni-minusi', label: '⚠️ ' + t('mozniMinusi.title'), path: '/reports/mozni-minusi' },
      ],
      advanced: [
        { id: 'batch-traceability', label: '🔍 Batch Traceability', path: '/advanced/batch-traceability' },
        { id: 'mrn-usage-tracking', label: '🛃 MRN Usage Tracking', path: '/advanced/mrn-usage-tracking' },
        { id: 'location-inquiry', label: '📍 Location Inquiry', path: '/advanced/location-inquiry' },
        { id: 'item-inquiry', label: '📦 Item Inquiry', path: '/advanced/item-inquiry' },
        { id: 'import-wizard', label: '📥 ' + t('import.title'), path: '/tools/import' },
      ],
      legacyTop: [
        { id: 'dashboard', label: t('nav.dashboard'), icon: '📊', path: '/dashboard' },
        { id: 'inventory', label: t('nav.wmsInventory'), icon: '📦', path: '/inventory' },
        { id: 'production', label: t('nav.production'), icon: '🏭', path: '/production' },
        { id: 'customs', label: t('nav.customsMrn'), icon: '🛃', path: '/customs' },
        { id: 'guarantees', label: t('nav.guarantees'), icon: '💰', path: '/guarantees' },
        { id: 'traceability', label: t('nav.traceability'), icon: '🔍', path: '/traceability' },
        { id: 'knowledge-base', label: t('nav.knowledgeBase'), icon: '🧠', path: '/knowledge-base' },
      ],
    }),
    [t]
  );

  const renderGroup = (group: NavGroup) => {
    const isExpanded = expanded[group.key] ?? false;
    const isEmpty = group.items.length === 0;

    return (
      <React.Fragment key={group.key}>
        <li
          className="menu-section"
          onClick={() => toggleGroup(group.key)}
          style={{ opacity: isEmpty ? 0.7 : 1 }}
          aria-expanded={isExpanded}
          role="button"
        >
          <span style={{ marginRight: '10px' }}>{group.icon}</span>
          <span style={{ flex: 1 }}>{t(group.labelKey)}</span>
          {isEmpty && (
            <span
              title={t('nav.groupEmptyHint')}
              style={{ marginRight: '6px', fontSize: '11px' }}
            >
              🚧
            </span>
          )}
          <span style={{ marginLeft: 'auto' }}>{isExpanded ? '▼' : '▶'}</span>
        </li>

        {isExpanded && (
          <ul className="submenu">
            {isEmpty ? (
              <li
                style={{
                  fontStyle: 'italic',
                  opacity: 0.6,
                  cursor: 'default',
                  fontSize: '12px',
                  padding: '6px 16px',
                }}
              >
                {t('nav.groupEmpty')}
              </li>
            ) : (
              group.items.map((item) => (
                <li
                  key={item.key}
                  className={activeModule === item.key ? 'active' : ''}
                  onClick={(e) => {
                    e.stopPropagation();
                    handleNavigate(item.path, item.key);
                  }}
                  title={item.backendStatus === 'missing' ? t('placeholder.comingSoon') : undefined}
                >
                  {item.icon && <span style={{ marginRight: '8px' }}>{item.icon}</span>}
                  {t(item.labelKey)}
                  {item.backendStatus === 'missing' && (
                    <span
                      style={{ marginLeft: '6px', fontSize: '10px', opacity: 0.7 }}
                      aria-label="coming soon"
                    >
                      🚧
                    </span>
                  )}
                  {item.backendStatus === 'partial' && (
                    <span
                      style={{ marginLeft: '6px', fontSize: '10px', opacity: 0.7 }}
                      aria-label="partial"
                    >
                      ⚠️
                    </span>
                  )}
                </li>
              ))
            )}
          </ul>
        )}
      </React.Fragment>
    );
  };

  const hasRoleBasedNav = navGroups.length > 0;

  return (
    <div className="sidebar">
      <div className="sidebar-header">
        <h1>{t('app.name')}</h1>
        <p>{t('app.tagline')}</p>
      </div>

      <ul className="nav">
        {/* ──────────────── NEW: role-aware, process-driven nav (P6.37) ──────────────── */}
        {hasRoleBasedNav && navGroups.map(renderGroup)}

        {/* ──────────────── LEGACY: flat nav kept until P6.37.14 cutover ──────────────── */}
        <li
          className="menu-section"
          style={{
            marginTop: '16px',
            opacity: 0.8,
            fontSize: '11px',
            textTransform: 'uppercase',
            letterSpacing: '0.5px',
            cursor: 'default',
            pointerEvents: 'none',
            borderTop: '1px solid rgba(255,255,255,0.1)',
            paddingTop: '12px',
          }}
        >
          {t('nav.legacySectionLabel')}
        </li>

        {legacyItems.legacyTop.map((item) => (
          <li
            key={item.id}
            className={activeModule === item.id ? 'active' : ''}
            onClick={() => handleNavigate(item.path, item.id)}
          >
            <span style={{ marginRight: '10px' }}>{item.icon}</span>
            {item.label}
          </li>
        ))}

        <li className="menu-section" onClick={() => setReportsExpanded((v) => !v)}>
          <span style={{ marginRight: '10px' }}>📊</span>
          {t('nav.reports')}
          <span style={{ marginLeft: 'auto' }}>{reportsExpanded ? '▼' : '▶'}</span>
        </li>
        {reportsExpanded && (
          <ul className="submenu">
            {legacyItems.reports.map((item) => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleNavigate(item.path, item.id);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}

        <li className="menu-section" onClick={() => setAdvancedExpanded((v) => !v)}>
          <span style={{ marginRight: '10px' }}>🚀</span>
          {t('nav.advancedFeatures')}
          <span style={{ marginLeft: 'auto' }}>{advancedExpanded ? '▼' : '▶'}</span>
        </li>
        {advancedExpanded && (
          <ul className="submenu">
            {legacyItems.advanced.map((item) => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleNavigate(item.path, item.id);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}

        <li className="menu-section" onClick={() => setAdminExpanded((v) => !v)}>
          <span style={{ marginRight: '10px' }}>🧑‍💼</span>
          {t('nav.administration')}
          <span style={{ marginLeft: 'auto' }}>{adminExpanded ? '▼' : '▶'}</span>
        </li>
        {adminExpanded && (
          <ul className="submenu">
            {[
              { id: 'admin-users', label: 'Users', path: '/admin/users' },
              { id: 'admin-employees', label: 'Employees', path: '/admin/employees' },
              { id: 'admin-shifts', label: 'Shifts', path: '/admin/shifts' },
              { id: 'admin-roles', label: 'Roles', path: '/admin/roles' },
            ].map((item) => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleNavigate(item.path, item.id);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}

        <li className="menu-section" onClick={() => setMasterDataExpanded((v) => !v)}>
          <span style={{ marginRight: '10px' }}>⚙️</span>
          {t('nav.masterData')}
          <span style={{ marginLeft: 'auto' }}>{masterDataExpanded ? '▼' : '▶'}</span>
        </li>
        {masterDataExpanded && (
          <ul className="submenu">
            {[
              { id: 'items', label: 'Items', path: '/master-data/items' },
              { id: 'partners', label: 'Partners', path: '/master-data/partners' },
              { id: 'warehouses', label: '📦 Warehouses', path: '/master-data/warehouses' },
              { id: 'locations', label: '📍 Locations', path: '/master-data/locations' },
              { id: 'uom', label: 'Units of Measure', path: '/master-data/uom' },
              { id: 'boms', label: 'Bills of Materials', path: '/master-data/boms' },
              { id: 'routings', label: 'Routings', path: '/master-data/routings' },
              { id: 'code-lists', label: 'Code Lists', path: '/master-data/code-lists' },
            ].map((item) => (
              <li
                key={item.id}
                className={activeModule === item.id ? 'active' : ''}
                onClick={(e) => {
                  e.stopPropagation();
                  handleNavigate(item.path, item.id);
                }}
              >
                {item.label}
              </li>
            ))}
          </ul>
        )}
      </ul>

      <div
        style={{
          padding: '12px 16px',
          marginTop: 'auto',
          borderTop: '1px solid rgba(255,255,255,0.1)',
        }}
      >
        <LanguageSwitcher compact />
      </div>
    </div>
  );
};

export default Sidebar;
