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

      <AlertEventsSection />
    </div>
  );
};

// Phase 17 §E10.5 — persistent alert events surface, driven by the
// AlertEvaluatorJob in LON.Worker. Sits below the computed feed; uses the
// same colour conventions but reads from /Management/alert-events.

type AlertEventDto = {
  id: string;
  alertRuleCode: string;
  alertRuleName: string;
  occurredAt: string;
  entityType: string;
  entityId: string | null;
  severity: number; // 1=Low,2=Med,3=High,4=Critical
  severityName: string;
  status: number; // 0=Open,1=Acknowledged,2=Resolved
  statusName: string;
  title: string;
  body: string;
  acknowledgedAt: string | null;
  acknowledgedBy: string | null;
  resolvedAt: string | null;
  resolvedBy: string | null;
};

const STATUS_FILTER: Array<{ value: '' | 0 | 1 | 2; key: string }> = [
  { value: '', key: 'all' },
  { value: 0, key: 'open' },
  { value: 1, key: 'acknowledged' },
  { value: 2, key: 'resolved' },
];

const SEVERITY_COLORS: Record<number, { bg: string; border: string; color: string }> = {
  4: { bg: '#ffebee', border: '#b71c1c', color: '#b71c1c' },
  3: { bg: '#ffebee', border: '#c62828', color: '#c62828' },
  2: { bg: '#fff3e0', border: '#ef6c00', color: '#ef6c00' },
  1: { bg: '#e3f2fd', border: '#1565c0', color: '#1565c0' },
};

const AlertEventsSection: React.FC = () => {
  const { t } = useTranslation();
  const [events, setEvents] = useState<AlertEventDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<'' | 0 | 1 | 2>(0);
  const [runningEvaluator, setRunningEvaluator] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = statusFilter === '' ? {} : { status: statusFilter };
      const resp = await managementApi.getAlertEvents(params);
      const env = resp.data as { data?: AlertEventDto[] };
      setEvents(env?.data ?? (resp.data as AlertEventDto[]));
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => { load(); }, [load]);

  const handleAck = async (id: string) => {
    try {
      await managementApi.acknowledgeAlertEvent(id);
      await load();
    } catch (err) { setError(translateError(err)); }
  };
  const handleResolve = async (id: string) => {
    try {
      await managementApi.resolveAlertEvent(id);
      await load();
    } catch (err) { setError(translateError(err)); }
  };
  const handleRunEvaluator = async () => {
    setRunningEvaluator(true);
    try {
      await managementApi.runAlertEvaluator();
      await load();
    } catch (err) { setError(translateError(err)); }
    finally { setRunningEvaluator(false); }
  };

  return (
    <div style={{ marginTop: 32 }}>
      <h2 style={{ marginBottom: 4 }}>{t('management.alertEvents.title')}</h2>
      <p style={{ color: '#666', marginTop: 0 }}>{t('management.alertEvents.subtitle')}</p>

      {error && (
        <div style={{ padding: 10, background: '#ffebee', color: '#c62828', marginBottom: 12, borderRadius: 4 }}>
          {error}
          <button onClick={() => setError(null)} style={{ marginLeft: 8 }}>×</button>
        </div>
      )}

      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap' }}>
        <label>
          {t('management.alertEvents.statusFilter')}:{' '}
          <select value={String(statusFilter)} onChange={(e) => setStatusFilter(e.target.value === '' ? '' : (Number(e.target.value) as 0 | 1 | 2))}>
            {STATUS_FILTER.map(s => (
              <option key={s.key} value={String(s.value)}>{t(`management.alertEvents.status.${s.key}`)}</option>
            ))}
          </select>
        </label>
        <button onClick={load} disabled={loading}>{loading ? t('common.loading') : t('common.refresh')}</button>
        <button onClick={handleRunEvaluator} disabled={runningEvaluator}>
          {runningEvaluator ? t('common.loading') : t('management.alertEvents.runEvaluator')}
        </button>
        <span style={{ color: '#666', marginLeft: 'auto' }}>{events.length} {t('management.alertEvents.events')}</span>
      </div>

      {events.length === 0 && !loading && (
        <div style={{ padding: 16, textAlign: 'center', color: '#2e7d32', background: '#e8f5e9', borderRadius: 4 }}>
          ✓ {t('management.alertEvents.empty')}
        </div>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {events.map(ev => {
          const sev = SEVERITY_COLORS[ev.severity] ?? SEVERITY_COLORS[1];
          return (
            <div key={ev.id} style={{
              padding: 12,
              background: sev.bg,
              borderLeft: `4px solid ${sev.border}`,
              borderRadius: 4,
              display: 'flex',
              gap: 12,
              alignItems: 'flex-start',
            }}>
              <div style={{ flex: 1 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <strong style={{ color: sev.color }}>{ev.title}</strong>
                  <span style={{ fontSize: 11, padding: '2px 6px', background: '#fff', border: `1px solid ${sev.border}`, borderRadius: 3, color: sev.color }}>{ev.severityName}</span>
                  <span style={{ fontSize: 11, padding: '2px 6px', background: '#e0e0e0', borderRadius: 3 }}>{ev.statusName}</span>
                </div>
                <div style={{ color: '#444', marginTop: 4 }}>{ev.body}</div>
                <div style={{ color: '#888', fontSize: 12, marginTop: 4 }}>
                  {ev.alertRuleName} · {formatDate(ev.occurredAt)} {new Date(ev.occurredAt).toLocaleTimeString()}
                  {ev.acknowledgedBy && (
                    <> · {t('management.alertEvents.acknowledgedBy', { user: ev.acknowledgedBy })}</>
                  )}
                  {ev.resolvedBy && (
                    <> · {t('management.alertEvents.resolvedBy', { user: ev.resolvedBy })}</>
                  )}
                </div>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4, minWidth: 100 }}>
                {ev.status === 0 && (
                  <button onClick={() => handleAck(ev.id)}>{t('management.alertEvents.acknowledge')}</button>
                )}
                {ev.status !== 2 && (
                  <button onClick={() => handleResolve(ev.id)}>{t('management.alertEvents.resolve')}</button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default Alerts;
