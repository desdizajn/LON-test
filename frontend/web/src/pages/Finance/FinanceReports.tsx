import React from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';

/**
 * P12.10 — Финансиски извештаи index page.
 * Hub that surfaces all finance-related reports as clickable cards. Each card
 * links to either a dedicated page or to a filtered view of an existing report.
 */

type ReportCard = {
  key: string;
  path: string;
  icon: string;
  color: string;
};

const REPORTS: ReportCard[] = [
  { key: 'invoicing', path: '/finance/invoicing', icon: '🧾', color: '#1e88e5' },
  { key: 'contracts', path: '/finance/contracts', icon: '📑', color: '#7b1fa2' },
  { key: 'guarantees', path: '/finance/guarantees', icon: '🛡️', color: '#f9a825' },
  { key: 'margin', path: '/finance/margin', icon: '📈', color: '#2e7d32' },
  { key: 'pnl', path: '/finance/pnl', icon: '💹', color: '#0d47a1' },
  { key: 'cashFlow', path: '/finance/cash-flow', icon: '💰', color: '#388e3c' },
  { key: 'costAccounting', path: '/finance/cost-accounting', icon: '⚙️', color: '#546e7a' },
  { key: 'ap', path: '/finance/ap', icon: '📥', color: '#c62828' },
  { key: 'payroll', path: '/finance/payroll', icon: '👥', color: '#ad1457' },
];

const FinanceReports: React.FC = () => {
  const { t } = useTranslation();
  return (
    <div style={{ padding: 16 }}>
      <h1>{t('financeReports.title')}</h1>
      <p style={{ color: '#666' }}>{t('financeReports.subtitle')}</p>

      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))',
        gap: 12,
        marginTop: 20,
      }}>
        {REPORTS.map((r) => (
          <Link
            key={r.key}
            to={r.path}
            style={{
              textDecoration: 'none',
              display: 'block',
              background: 'white',
              border: '1px solid var(--border, #e5e7eb)',
              borderRadius: 8,
              padding: 16,
              boxShadow: '0 1px 2px rgba(0,0,0,0.04)',
              transition: 'transform 0.1s, box-shadow 0.1s',
            }}
          >
            <div style={{ fontSize: 28, marginBottom: 8 }}>{r.icon}</div>
            <div style={{ color: r.color, fontWeight: 600, fontSize: 15 }}>
              {t(`financeReports.cards.${r.key}.title`)}
            </div>
            <div style={{ fontSize: 12, color: '#666', marginTop: 4 }}>
              {t(`financeReports.cards.${r.key}.subtitle`)}
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
};

export default FinanceReports;
