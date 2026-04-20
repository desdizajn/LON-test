import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authService } from '../services/authService';
import { useLayout } from './layout/LayoutContext';

/**
 * Global top bar — rendered above the main content area for every authenticated route.
 * Hosts cross-cutting tools (search, AI, import, logout) that every role uses
 * plus the mobile drawer toggle (hamburger), which is hidden ≥ 900px.
 */
const TopBar: React.FC = () => {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { toggleMobileNav } = useLayout();
  const [user] = useState(() => authService.getCurrentUser());
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  const isAdmin = user?.roles.includes('Administrator') ?? false;
  const displayName = user?.fullName || user?.username || '';
  const primaryRole = user?.roles[0] || '';

  const handleLogout = () => {
    authService.logout();
    navigate('/login');
  };

  return (
    <>
      <header className="topbar" role="banner">
        <button
          className="topbar__hamburger"
          onClick={toggleMobileNav}
          aria-label={t('topBar.openMenu')}
          title={t('topBar.openMenu')}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
            <path d="M3 6h18M3 12h18M3 18h18" />
          </svg>
        </button>

        <div style={{ flex: 1 }} />

        <button onClick={() => setSearchOpen(true)} title={t('topBar.search')} aria-label={t('topBar.search')}>
          <span aria-hidden="true">🔍</span>
          <span className="topbar__btn-label" style={{ marginLeft: 6 }}>{t('topBar.search')}</span>
        </button>

        <button onClick={() => navigate('/knowledge-base')} title={t('topBar.aiAssistant')} aria-label={t('topBar.aiAssistant')}>
          <span aria-hidden="true">🧠</span>
          <span className="topbar__btn-label" style={{ marginLeft: 6 }}>{t('topBar.aiAssistant')}</span>
        </button>

        <button onClick={() => navigate('/knowledge-base/search')} title={t('kbSearch.title')}>
          <span aria-hidden="true">📚</span>
          <span className="topbar__btn-label" style={{ marginLeft: 6 }}>KB</span>
        </button>

        {isAdmin && (
          <>
            <button onClick={() => navigate('/tools/import')} title={t('topBar.import')}>
              <span aria-hidden="true">📥</span>
              <span className="topbar__btn-label" style={{ marginLeft: 6 }}>{t('topBar.import')}</span>
            </button>
            <button onClick={() => navigate('/tools/import/kw12')} title={t('kw12Wizard.title')}>
              <span aria-hidden="true">📦</span>
              <span className="topbar__btn-label" style={{ marginLeft: 6 }}>KW12</span>
            </button>
          </>
        )}

        {/* User identity pill */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'flex-end',
            marginLeft: 12,
            lineHeight: 1.2,
          }}
        >
          <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--ink-900)' }}>{displayName}</span>
          {primaryRole && <span style={{ fontSize: 11, color: 'var(--ink-500)' }}>{primaryRole}</span>}
        </div>

        <button
          onClick={handleLogout}
          className="btn-danger"
          style={{ background: 'var(--danger)', color: 'white', borderColor: 'var(--danger)', marginLeft: 4 }}
          aria-label={t('nav.logout')}
        >
          <span aria-hidden="true">🚪</span>
          <span className="topbar__btn-label" style={{ marginLeft: 6 }}>{t('nav.logout')}</span>
        </button>
      </header>

      {searchOpen && (
        <div
          role="dialog"
          aria-modal="true"
          onClick={() => setSearchOpen(false)}
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15, 23, 42, 0.55)',
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'center',
            paddingTop: '14vh',
            zIndex: 1000,
            backdropFilter: 'blur(2px)',
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              borderRadius: 12,
              width: '92%',
              maxWidth: 640,
              padding: 24,
              boxShadow: 'var(--shadow-lg)',
            }}
          >
            <div style={{ marginBottom: 16, fontSize: 16, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 8 }}>
              <span>🔍</span> {t('topBar.search')}
            </div>
            <input
              autoFocus
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={t('topBar.searchPlaceholder') ?? ''}
              style={{ marginBottom: 14 }}
            />
            <div
              style={{
                background: 'var(--warning-bg)',
                border: '1px dashed var(--warning)',
                borderRadius: 8,
                padding: 14,
                fontSize: 13,
                color: 'var(--ink-800)',
              }}
            >
              <strong>🚧 {t('placeholder.comingSoon')}</strong>
              <div style={{ marginTop: 6, lineHeight: 1.6 }}>
                {t('topBar.searchComingSoonBody')}
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
              <button onClick={() => setSearchOpen(false)} className="btn-primary">
                {t('common.close')}
              </button>
            </div>
          </div>
        </div>
      )}

      <style>{`
        @media (max-width: 640px) {
          .topbar__btn-label { display: none; }
        }
      `}</style>
    </>
  );
};

export default TopBar;
