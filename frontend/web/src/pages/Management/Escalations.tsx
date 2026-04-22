import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { formatDate } from '../../utils/format';

/**
 * P13.8 — Escalations.
 *
 * Lightweight escalation tracker: customer/partner issue → severity → owner
 * → status (Open / In Review / Resolved / Deferred). Stored in browser
 * localStorage per tenant until a dedicated Escalation backend entity lands.
 */

type Severity = 'Low' | 'Medium' | 'High' | 'Critical';
type Status = 'Open' | 'InReview' | 'Resolved' | 'Deferred';

type Escalation = {
  id: string;
  title: string;
  party: string; // customer, supplier, internal
  severity: Severity;
  status: Status;
  owner: string;
  dueDate: string;
  description: string;
  resolution: string;
  createdAt: string;
};

const SEVERITIES: Severity[] = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES: Status[] = ['Open', 'InReview', 'Resolved', 'Deferred'];

const storageKey = (tenantId: string) => `lon.escalations.${tenantId || 'default'}`;
function currentTenantId(): string {
  try {
    const raw = localStorage.getItem('token') || '';
    const part = raw.split('.')[1];
    if (!part) return 'default';
    const p = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    return p['tenant_id'] || 'default';
  } catch { return 'default'; }
}

const SEVERITY_COLOR: Record<Severity, string> = {
  Low: '#2e7d32', Medium: '#f9a825', High: '#ef6c00', Critical: '#c62828',
};
const STATUS_COLOR: Record<Status, string> = {
  Open: '#ef6c00', InReview: '#1976d2', Resolved: '#2e7d32', Deferred: '#616161',
};

const Escalations: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Escalation[]>([]);
  const [search, setSearch] = useState('');
  const [severityFilter, setSeverityFilter] = useState<Severity | 'All'>('All');
  const [statusFilter, setStatusFilter] = useState<Status | 'All'>('Open');

  const today = new Date().toISOString().slice(0, 10);
  const [draft, setDraft] = useState<Escalation>({
    id: '', title: '', party: '', severity: 'Medium', status: 'Open',
    owner: '', dueDate: today, description: '', resolution: '', createdAt: today,
  });

  const tenantId = currentTenantId();
  useEffect(() => {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw) { try { setRows(JSON.parse(raw)); } catch { /* ignore */ } }
  }, [tenantId]);

  function persist(next: Escalation[]) { setRows(next); localStorage.setItem(storageKey(tenantId), JSON.stringify(next)); }
  function add() {
    if (!draft.title.trim()) { toast.error(t('escalations.invalid') as string); return; }
    persist([{ ...draft, id: crypto.randomUUID(), createdAt: today }, ...rows]);
    setDraft({ ...draft, id: '', title: '', description: '', resolution: '' });
    toast.success(t('escalations.saved') as string);
  }
  function setStatus(id: string, status: Status) { persist(rows.map((r) => (r.id === id ? { ...r, status } : r))); }
  function updateResolution(id: string, resolution: string) { persist(rows.map((r) => (r.id === id ? { ...r, resolution } : r))); }
  function remove(id: string) {
    if (!window.confirm(t('escalations.confirmDelete') as string)) return;
    persist(rows.filter((r) => r.id !== id));
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (severityFilter !== 'All' && r.severity !== severityFilter) return false;
      if (statusFilter !== 'All' && r.status !== statusFilter) return false;
      if (q && !`${r.title} ${r.party} ${r.owner} ${r.description}`.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [rows, severityFilter, statusFilter, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('escalations.title')}</h1>
      <p style={{ color: '#666' }}>{t('escalations.subtitle')}</p>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('escalations.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('escalations.titleLabel')}
            <input type="text" value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('escalations.party')}
            <input type="text" value={draft.party} onChange={(e) => setDraft({ ...draft, party: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('escalations.severity')}
            <select value={draft.severity} onChange={(e) => setDraft({ ...draft, severity: e.target.value as Severity })} style={{ padding: 6, width: '100%' }}>
              {SEVERITIES.map((s) => <option key={s} value={s}>{t(`risks.severities.${s}`)}</option>)}
            </select>
          </label>
          <label>{t('escalations.owner')}
            <input type="text" value={draft.owner} onChange={(e) => setDraft({ ...draft, owner: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('escalations.dueDate')}
            <input type="date" value={draft.dueDate} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>{t('escalations.description')}
            <textarea rows={2} value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {t('escalations.add')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('escalations.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value as typeof severityFilter)} style={{ padding: 6 }}>
          <option value="All">{t('risks.allSeverities')}</option>
          {SEVERITIES.map((s) => <option key={s} value={s}>{t(`risks.severities.${s}`)}</option>)}
        </select>
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} style={{ padding: 6 }}>
          <option value="All">{t('escalations.allStatuses')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{t(`escalations.statuses.${s}`)}</option>)}
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('escalations.rowCount', { count: filtered.length })}</span>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('escalations.severity')}</th>
              <th>{t('escalations.titleLabel')}</th>
              <th>{t('escalations.party')}</th>
              <th>{t('escalations.owner')}</th>
              <th>{t('escalations.dueDate')}</th>
              <th>{t('escalations.statusCol')}</th>
              <th>{t('escalations.resolution')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('escalations.empty')}</td></tr>}
            {filtered.map((r) => (
              <tr key={r.id}>
                <td><span style={{ padding: '2px 8px', borderRadius: 3, background: SEVERITY_COLOR[r.severity], color: 'white', fontSize: 12, fontWeight: 600 }}>{t(`risks.severities.${r.severity}`)}</span></td>
                <td><strong>{r.title}</strong>{r.description && <div style={{ fontSize: 12, color: '#666', marginTop: 4 }}>{r.description}</div>}</td>
                <td>{r.party || '-'}</td>
                <td>{r.owner || '-'}</td>
                <td>{formatDate(r.dueDate)}</td>
                <td>
                  <select value={r.status} onChange={(e) => setStatus(r.id, e.target.value as Status)} style={{ padding: 4, borderColor: STATUS_COLOR[r.status] }}>
                    {STATUSES.map((s) => <option key={s} value={s}>{t(`escalations.statuses.${s}`)}</option>)}
                  </select>
                </td>
                <td>
                  <input type="text" value={r.resolution} onChange={(e) => updateResolution(r.id, e.target.value)} style={{ padding: 4, width: 180 }} />
                </td>
                <td><button onClick={() => remove(r.id)} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Escalations;
