import React, { useEffect, useState } from 'react';
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

  const [expanded, setExpanded] = useState<Record<string, boolean>>(() =>
    loadExpandedState()
  );

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

  const toggleGroup = (key: NavGroupKey) => {
    const next = { ...expanded, [key]: !expanded[key] };
    setExpanded(next);
    saveExpandedState(next);
  };

  const handleNavigate = (path: string, key: string) => {
    setActiveModule(key);
    navigate(path);
  };

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

  return (
    <div className="sidebar">
      <div className="sidebar-header">
        <h1>{t('app.name')}</h1>
        <p>{t('app.tagline')}</p>
      </div>

      <ul className="nav">
        {navGroups.map(renderGroup)}
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
