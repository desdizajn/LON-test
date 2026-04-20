import React, { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import LanguageSwitcher from './LanguageSwitcher';
import { useNavForRoles } from '../nav/useNavForRoles';
import { NavGroup, NavGroupKey } from '../nav/types';
import { useLayout } from './layout/LayoutContext';

interface SidebarProps {
  activeModule: string;
  setActiveModule: (module: string) => void;
}

const EXPAND_STATE_KEY = 'lon.nav.expandedGroups';

const loadExpandedState = (): Record<string, boolean> => {
  try {
    const raw = localStorage.getItem(EXPAND_STATE_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch { return {}; }
};

const saveExpandedState = (state: Record<string, boolean>): void => {
  try { localStorage.setItem(EXPAND_STATE_KEY, JSON.stringify(state)); }
  catch { /* private mode */ }
};

const Sidebar: React.FC<SidebarProps> = ({ activeModule, setActiveModule }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useTranslation();
  const { mobileNavOpen, closeMobileNav } = useLayout();

  const navGroups = useNavForRoles();

  const [expanded, setExpanded] = useState<Record<string, boolean>>(() => loadExpandedState());

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

  const toggleGroup = (key: NavGroupKey) => {
    const next = { ...expanded, [key]: !expanded[key] };
    setExpanded(next);
    saveExpandedState(next);
  };

  const handleNavigate = (path: string, key: string) => {
    setActiveModule(key);
    navigate(path);
    closeMobileNav();
  };

  const renderGroup = (group: NavGroup) => {
    const isExpanded = expanded[group.key] ?? false;
    const isEmpty = group.items.length === 0;

    return (
      <React.Fragment key={group.key}>
        <li
          className="menu-section"
          onClick={() => toggleGroup(group.key)}
          style={{ opacity: isEmpty ? 0.6 : 1 }}
          aria-expanded={isExpanded}
          role="button"
        >
          <span style={{ marginRight: 10, fontSize: 16 }}>{group.icon}</span>
          <span style={{ flex: 1 }}>{t(group.labelKey)}</span>
          {isEmpty && (
            <span title={t('nav.groupEmptyHint')} style={{ marginRight: 6, fontSize: 11 }}>🚧</span>
          )}
          <span style={{ marginLeft: 'auto', fontSize: 10, opacity: 0.7 }}>{isExpanded ? '▼' : '▶'}</span>
        </li>

        {isExpanded && (
          <ul className="submenu">
            {isEmpty ? (
              <li style={{ fontStyle: 'italic', opacity: 0.5, cursor: 'default', fontSize: 12, padding: '6px 16px 6px 52px' }}>
                {t('nav.groupEmpty')}
              </li>
            ) : (
              group.items.map((item) => (
                <li
                  key={item.key}
                  className={activeModule === item.key ? 'active' : ''}
                  onClick={(e) => { e.stopPropagation(); handleNavigate(item.path, item.key); }}
                  title={item.backendStatus === 'missing' ? t('placeholder.comingSoon') : undefined}
                >
                  {item.icon && <span style={{ marginRight: 8 }}>{item.icon}</span>}
                  {t(item.labelKey)}
                  {item.backendStatus === 'missing' && (
                    <span style={{ marginLeft: 6, fontSize: 10, opacity: 0.7 }} aria-label="coming soon">🚧</span>
                  )}
                  {item.backendStatus === 'partial' && (
                    <span style={{ marginLeft: 6, fontSize: 10, opacity: 0.7 }} aria-label="partial">⚠️</span>
                  )}
                </li>
              ))
            )}
          </ul>
        )}
      </React.Fragment>
    );
  };

  return (
    <>
      {/* Backdrop shown only when drawer is open on narrow viewports. */}
      <div
        className={`sidebar__backdrop${mobileNavOpen ? ' sidebar__backdrop--visible' : ''}`}
        onClick={closeMobileNav}
        aria-hidden="true"
      />

      <aside className={`sidebar${mobileNavOpen ? ' sidebar--open' : ''}`} aria-label="Primary navigation">
        <div className="sidebar-header">
          <div className="sidebar-header__mark">
            <img src={`${process.env.PUBLIC_URL}/taris-favicon.png`} alt="Taris" />
          </div>
          <div className="sidebar-header__text">
            <h1>TARIS</h1>
            <p>LON management</p>
          </div>
        </div>

        <div className="sidebar__scroll">
          <ul className="nav">
            {navGroups.map(renderGroup)}
          </ul>
        </div>

        <div className="sidebar__footer">
          <LanguageSwitcher compact />
          <div className="sidebar__footer-brand">
            © {new Date().getFullYear()} Elbosoft Consulting DOOEL
          </div>
        </div>
      </aside>
    </>
  );
};

export default Sidebar;
