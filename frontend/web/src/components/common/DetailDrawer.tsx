import React, { useEffect } from 'react';
import { useTranslation } from 'react-i18next';

interface Props {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
  width?: number | string;
  footer?: React.ReactNode;
  children: React.ReactNode;
}

/**
 * Right-side slide-in drawer for row detail / edit views. Replaces the pattern
 * where lists showed data but never let the user drill into a single row.
 * Keeps the list visible behind a scrim so the context of „which record" is
 * preserved.
 */
const DetailDrawer: React.FC<Props> = ({ open, onClose, title, subtitle, width = 560, footer, children }) => {
  const { t } = useTranslation();

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = prev;
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <>
      <div
        onClick={onClose}
        aria-hidden
        style={{
          position: 'fixed',
          inset: 0,
          background: 'rgba(15, 23, 42, 0.35)',
          zIndex: 100,
        }}
      />
      <aside
        role="dialog"
        aria-label={title}
        style={{
          position: 'fixed',
          top: 0,
          right: 0,
          bottom: 0,
          width,
          maxWidth: '100vw',
          background: 'white',
          boxShadow: '-8px 0 24px rgba(15, 23, 42, 0.12)',
          zIndex: 101,
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <header
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 12,
            padding: '14px 18px',
            borderBottom: '1px solid var(--border, #e5e7eb)',
          }}
        >
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 16, fontWeight: 600 }}>{title}</div>
            {subtitle && (
              <div style={{ fontSize: 12, color: '#777', marginTop: 2 }}>{subtitle}</div>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('common.close') as string}
            style={{
              border: '1px solid var(--border, #e5e7eb)',
              background: 'white',
              borderRadius: 6,
              padding: '4px 10px',
              cursor: 'pointer',
              fontSize: 14,
            }}
          >
            ×
          </button>
        </header>
        <div style={{ flex: 1, overflowY: 'auto', padding: 18 }}>{children}</div>
        {footer && (
          <footer
            style={{
              padding: '12px 18px',
              borderTop: '1px solid var(--border, #e5e7eb)',
              display: 'flex',
              justifyContent: 'flex-end',
              gap: 8,
              background: 'var(--ink-50, #f8fafc)',
            }}
          >
            {footer}
          </footer>
        )}
      </aside>
    </>
  );
};

export default DetailDrawer;
