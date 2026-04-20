import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { managementApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';

/**
 * P13.5 — exception alerts feed.
 *
 * Aggregates 5 alert sources: MRN expiring, overdue invoices, material
 * shortage on active POs, at-risk POs (progress vs schedule gap), LON
 * authorisation expiring. Sorted by severity (Critical → Warning → Info).
 */

type AlertCategory = 1 | 2 | 3 | 4 | 5;
type AlertSeverity = 1 | 2 | 3;

type Alert = {
  category: AlertCategory;
  severity: AlertSeverity;
  title: string;
  detail: string;
  linkPath: string | null;
  relatedDate: string | null;
  amount: number | null;
  currency: string | null;
};

type Feed = { generatedAt: string; rows: Alert[] };

const CATEGORY = {
  1: { key: 'mrnExpiring', icon: '📅' },
  2: { key: 'overdueInvoice', icon: '💰' },
  3: { key: 'materialShortage', icon: '📦' },
  4: { key: 'atRiskPO', icon: '⚠️' },
  5: { key: 'lonAuthExpiring', icon: '📜' },
} as const;

const SEVERITY = {
  3: { key: 'critical', bg: '#ffebee', color: '#c62828', border: '#c62828' },
  2: { key: 'warning', bg: '#fff3e0', color: '#ef6c00', border: '#ef6c00' },
  1: { key: 'info', bg: '#e3f2fd', color: '#1565c0', border: '#1565c0' },
} as const;

const Alerts: React.FC = () => {
  const { t } = useTranslation();
  const [feed, setFeed] = useState<Feed | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<number | ''>('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await managementApi.getAlerts();
      const env = resp.data as { data?: Feed };
      setFeed(env?.data ?? (resp.data as Feed));
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = useMemo(() => {
    if (!feed) return [];
    if (categoryFilter === '') return feed.rows;
    return feed.rows.filter((r) => r.category === categoryFilter);
  }, [feed, categoryFilter]);

  const counts = useMemo(() => {
    if (!feed) return { total: 0, critical: 0, warning: 0, info: 0 };
    return {
      total: feed.rows.length,
      critical: feed.rows.filter((r) => r.severity === 3).length,
      warning: feed.rows.filter((r) => r.severity === 2).length,
      info: feed.rows.filter((r) => r.severity === 1).length,
    };
  }, [feed]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('management.alerts.title')}</h1>
      <p style={{ color: '#666' }}>{t('management.alerts.subtitle')}</p>

      {error && (
        <div style={{ padding: 10, background: '#ffebee', color: '#c62828', marginBottom: 12, borderRadius: 4 }}>
          {error}
          <button onClick={() => setError(null)} style={{ marginLeft: 8 }}>×</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: 16, marginBottom: 16, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap', alignItems: 'center' }}>
        <div><small>{t('management.alerts.total')}</small><div style={{ fontWeight: 700, fontSize: 22 }}>{counts.total}</div></div>
        <div><small>{t('management.alerts.severity.critical')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{counts.critical}</div></div>
        <div><small>{t('management.alerts.severity.warning')}</small><div style={{ fontWeight: 600, color: '#ef6c00' }}>{counts.warning}</div></div>
        <div><small>{t('management.alerts.severity.info')}</small><div style={{ fontWeight: 600, color: '#1565c0' }}>{counts.info}</div></div>
        <label>
          {t('management.alerts.category')}:{' '}
          <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value === '' ? '' : Number(e.target.value))}>
            <option value="">{t('common.all')}</option>
            {Object.entries(CATEGORY).map(([k, v]) => (
              <option key={k} value={k}>{v.icon} {t(`management.alerts.categories.${v.key}`)}</option>
            ))}
          </select>
        </label>
        <button onClick={load} disabled={loading}>{loading ? t('common.loading') : t('common.refresh')}</button>
        {feed && (
          <small style={{ color: '#666', marginLeft: 'auto' }}>
            {t('management.alerts.generatedAt')}: {formatDate(feed.generatedAt)} {new Date(feed.generatedAt).toLocaleTimeString()}
          </small>
        )}
      </div>

      {filtered.length === 0 && !loading && (
        <div style={{ padding: 24, textAlign: 'center', color: '#2e7d32', background: '#e8f5e9', borderRadius: 4 }}>
          ✓ {t('management.alerts.noAlerts')}
        </div>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {filtered.map((a, idx) => {
          const sev = SEVERITY[a.severity];
          const cat = CATEGORY[a.category];
          return (
            <div key={idx} style={{
              padding: 12,
              background: sev.bg,
              borderLeft: `4px solid ${sev.border}`,
              borderRadius: 4,
              display: 'flex',
              gap: 12,
              alignItems: 'center',
            }}>
              <div style={{ fontSize: 24 }}>{cat.icon}</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, color: sev.color }}>{a.title}</div>
                <div style={{ fontSize: 13, color: '#555', marginTop: 2 }}>{a.detail}</div>
                <div style={{ fontSize: 11, color: '#888', marginTop: 4 }}>
                  <span style={{ padding: '1px 6px', background: '#fff', borderRadius: 2, marginRight: 6 }}>
                    {t(`management.alerts.categories.${cat.key}`)}
                  </span>
                  {a.relatedDate && <span>{formatDate(a.relatedDate)} · </span>}
                  {a.amount !== null && a.currency && <span>{formatQuantity(a.amount, 2)} {a.currency}</span>}
                  {a.amount !== null && !a.currency && <span>{formatQuantity(a.amount, 2)}</span>}
                </div>
              </div>
              {a.linkPath && (
                <Link to={a.linkPath} style={{
                  padding: '4px 10px',
                  background: '#fff',
                  color: sev.color,
                  textDecoration: 'none',
                  borderRadius: 3,
                  fontSize: 12,
                  border: `1px solid ${sev.border}`,
                }}>
                  {t('management.alerts.goTo')} →
                </Link>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default Alerts;
