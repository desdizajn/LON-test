import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authService } from '../services/authService';

/**
 * Global top bar — rendered above the main content area for every authenticated route.
 * Hosts cross-cutting tools (search, AI, import, logout) that every role uses.
 *
 * Design rationale (P6.37):
 *  - Knowledge Base (AI) is a tool, not a domain — moved out of role-specific nav.
 *  - Universal search spans everything the role can see (declarations, MRN, lots,
 *    orders, shipments). Backend is a stub for now; wired up in a follow-up.
 *  - Import Wizard stays admin-only. Non-admins don't see the button.
 */
const TopBar: React.FC = () => {
  const navigate = useNavigate();
  const { t } = useTranslation();
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

  const btnStyle: React.CSSProperties = {
    background: 'transparent',
    border: '1px solid #e2e8f0',
    borderRadius: '8px',
    padding: '6px 12px',
    cursor: 'pointer',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: '13px',
    color: '#2d3748',
    whiteSpace: 'nowrap',
  };

  return (
    <>
      <div
        style={{
          height: '56px',
          background: 'white',
          borderBottom: '1px solid #e2e8f0',
          padding: '0 24px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'flex-end',
          gap: '8px',
          position: 'sticky',
          top: 0,
          zIndex: 10,
        }}
        role="banner"
      >
        {/* Universal search trigger */}
        <button
          onClick={() => setSearchOpen(true)}
          style={btnStyle}
          aria-label={t('topBar.search')}
          title={t('topBar.search')}
        >
          <span>🔍</span>
          <span>{t('topBar.search')}</span>
        </button>

        {/* AI / Knowledge Base */}
        <button
          onClick={() => navigate('/knowledge-base')}
          style={btnStyle}
          aria-label={t('topBar.aiAssistant')}
          title={t('topBar.aiAssistant')}
        >
          <span>🧠</span>
          <span>{t('topBar.aiAssistant')}</span>
        </button>

        {/* KB semantic search (P6.38) */}
        <button
          onClick={() => navigate('/knowledge-base/search')}
          style={btnStyle}
          aria-label={t('kbSearch.title')}
          title={t('kbSearch.title')}
        >
          <span>🔍</span>
          <span>KB</span>
        </button>

        {/* Import Wizard — admin only */}
        {isAdmin && (
          <>
            <button
              onClick={() => navigate('/tools/import')}
              style={btnStyle}
              aria-label={t('topBar.import')}
              title={t('topBar.import')}
            >
              <span>📥</span>
              <span>{t('topBar.import')}</span>
            </button>
            <button
              onClick={() => navigate('/tools/import/kw12')}
              style={btnStyle}
              aria-label={t('kw12Wizard.title')}
              title={t('kw12Wizard.title')}
            >
              <span>📦</span>
              <span>KW12</span>
            </button>
          </>
        )}

        {/* User identity */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'flex-end',
            marginLeft: '12px',
            lineHeight: 1.2,
          }}
        >
          <span style={{ fontSize: '13px', fontWeight: 600, color: '#2d3748' }}>
            {displayName}
          </span>
          {primaryRole && (
            <span style={{ fontSize: '11px', color: '#718096' }}>{primaryRole}</span>
          )}
        </div>

        {/* Logout */}
        <button
          onClick={handleLogout}
          style={{
            ...btnStyle,
            background: '#fc8181',
            color: 'white',
            border: 'none',
            marginLeft: '8px',
          }}
          aria-label={t('nav.logout')}
        >
          <span>🚪</span>
          <span>{t('nav.logout')}</span>
        </button>
      </div>

      {/* Search modal — stub until universal search backend lands */}
      {searchOpen && (
        <div
          role="dialog"
          aria-modal="true"
          onClick={() => setSearchOpen(false)}
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'center',
            paddingTop: '15vh',
            zIndex: 1000,
          }}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              borderRadius: '12px',
              width: '90%',
              maxWidth: '640px',
              padding: '24px',
              boxShadow: '0 20px 40px rgba(0,0,0,0.2)',
            }}
          >
            <div style={{ marginBottom: '16px', fontSize: '18px', fontWeight: 600 }}>
              🔍 {t('topBar.search')}
            </div>
            <input
              autoFocus
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={t('topBar.searchPlaceholder')}
              style={{
                width: '100%',
                padding: '12px 16px',
                fontSize: '15px',
                border: '1px solid #cbd5e0',
                borderRadius: '8px',
                marginBottom: '16px',
                boxSizing: 'border-box',
              }}
            />
            <div
              style={{
                background: '#fff5f5',
                border: '1px dashed #fc8181',
                borderRadius: '8px',
                padding: '16px',
                fontSize: '13px',
                color: '#742a2a',
              }}
            >
              <strong>🚧 {t('placeholder.comingSoon')}</strong>
              <div style={{ marginTop: '6px', lineHeight: 1.6 }}>
                {t('topBar.searchComingSoonBody')}
              </div>
            </div>
            <div
              style={{
                display: 'flex',
                justifyContent: 'flex-end',
                marginTop: '16px',
              }}
            >
              <button
                onClick={() => setSearchOpen(false)}
                style={{
                  padding: '8px 16px',
                  background: '#4299e1',
                  color: 'white',
                  border: 'none',
                  borderRadius: '6px',
                  cursor: 'pointer',
                  fontSize: '14px',
                }}
              >
                {t('common.close')}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default TopBar;
