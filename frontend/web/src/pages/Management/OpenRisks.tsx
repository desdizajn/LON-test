import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P13.6 — Open risks register.
 *
 * Lightweight risk register with severity + owner + mitigation plan. Persists
 * to browser localStorage per tenant until a RiskRegisterItem backend entity
 * lands. Covers the „сакам да ги забележам ризиците" operator need without
 * blocking on a schema migration.
 */

type Severity = 'Low' | 'Medium' | 'High' | 'Critical';
type Status = 'Open' | 'Mitigating' | 'Closed';

type Risk = {
  id: string;
  title: string;
  category: string;
  severity: Severity;
  status: Status;
  owner: string;
  mitigation: string;
  reviewDate: string;
  createdAt: string;
};

const SEVERITIES: Severity[] = ['Low', 'Medium', 'High', 'Critical'];
const STATUSES: Status[] = ['Open', 'Mitigating', 'Closed'];
const CATEGORIES = ['Supplier', 'Quality', 'Machine', 'Legal', 'Financial', 'HR', 'Customs', 'Other'];

const storageKey = (tenantId: string) => `lon.risks.${tenantId || 'default'}`;
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

const OpenRisks: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Risk[]>([]);
  const [search, setSearch] = useState('');
  const [severityFilter, setSeverityFilter] = useState<Severity | 'All'>('All');
  const [statusFilter, setStatusFilter] = useState<Status | 'All'>('Open');

  const today = new Date().toISOString().slice(0, 10);
  const [draft, setDraft] = useState<Risk>({
    id: '', title: '', category: 'Other', severity: 'Medium', status: 'Open',
    owner: '', mitigation: '', reviewDate: today, createdAt: today,
  });

  const tenantId = currentTenantId();
  useEffect(() => {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw) { try { setRows(JSON.parse(raw)); } catch { /* ignore */ } }
  }, [tenantId]);

  function persist(next: Risk[]) { setRows(next); localStorage.setItem(storageKey(tenantId), JSON.stringify(next)); }

  function add() {
    if (!draft.title.trim()) { toast.error(t('risks.invalid') as string); return; }
    persist([{ ...draft, id: crypto.randomUUID(), createdAt: today }, ...rows]);
    setDraft({ ...draft, id: '', title: '', mitigation: '' });
    toast.success(t('risks.saved') as string);
  }

  function setStatus(id: string, status: Status) { persist(rows.map((r) => (r.id === id ? { ...r, status } : r))); }
  function remove(id: string) {
    if (!window.confirm(t('risks.confirmDelete') as string)) return;
    persist(rows.filter((r) => r.id !== id));
  }

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (severityFilter !== 'All' && r.severity !== severityFilter) return false;
      if (statusFilter !== 'All' && r.status !== statusFilter) return false;
      if (q && !`${r.title} ${r.owner} ${r.category} ${r.mitigation}`.toLowerCase().includes(q)) return false;
      return true;
    }).sort((a, b) => {
      const rank: Record<Severity, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };
      return rank[a.severity] - rank[b.severity];
    });
  }, [rows, severityFilter, statusFilter, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('risks.title')}</h1>
      <p style={{ color: '#666' }}>{t('risks.subtitle')}</p>

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('risks.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('risks.titleLabel')}
            <input type="text" value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('risks.category')}
            <select value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} style={{ padding: 6, width: '100%' }}>
              {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>
          <label>{t('risks.severity')}
            <select value={draft.severity} onChange={(e) => setDraft({ ...draft, severity: e.target.value as Severity })} style={{ padding: 6, width: '100%' }}>
              {SEVERITIES.map((s) => <option key={s} value={s}>{t(`risks.severities.${s}`)}</option>)}
            </select>
          </label>
          <label>{t('risks.owner')}
            <input type="text" value={draft.owner} onChange={(e) => setDraft({ ...draft, owner: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('risks.reviewDate')}
            <input type="date" value={draft.reviewDate} onChange={(e) => setDraft({ ...draft, reviewDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>{t('risks.mitigation')}
            <textarea rows={2} value={draft.mitigation} onChange={(e) => setDraft({ ...draft, mitigation: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {t('risks.add')}
          </button>
        </div>
        <div style={{ fontSize: 11, color: '#888', marginTop: 8 }}>{t('risks.storageHint')}</div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('risks.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value as typeof severityFilter)} style={{ padding: 6 }}>
          <option value="All">{t('risks.allSeverities')}</option>
          {SEVERITIES.map((s) => <option key={s} value={s}>{t(`risks.severities.${s}`)}</option>)}
        </select>
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} style={{ padding: 6 }}>
          <option value="All">{t('risks.allStatuses')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{t(`risks.statuses.${s}`)}</option>)}
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('risks.rowCount', { count: filtered.length })}</span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'title', label: t('risks.titleLabel') as string },
          { key: 'category', label: t('risks.category') as string },
          { key: 'severity', label: t('risks.severity') as string },
          { key: 'status', label: t('risks.statusCol') as string },
          { key: 'owner', label: t('risks.owner') as string },
          { key: 'reviewDate', label: t('risks.reviewDate') as string, type: 'date' },
          { key: 'mitigation', label: t('risks.mitigation') as string },
        ], 'risks')} disabled={filtered.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('risks.severity')}</th>
              <th>{t('risks.titleLabel')}</th>
              <th>{t('risks.category')}</th>
              <th>{t('risks.owner')}</th>
              <th>{t('risks.reviewDate')}</th>
              <th>{t('risks.mitigation')}</th>
              <th>{t('risks.statusCol')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('risks.empty')}</td></tr>}
            {filtered.map((r) => (
              <tr key={r.id}>
                <td><span style={{ padding: '2px 8px', borderRadius: 3, background: SEVERITY_COLOR[r.severity], color: 'white', fontSize: 12, fontWeight: 600 }}>{t(`risks.severities.${r.severity}`)}</span></td>
                <td><strong>{r.title}</strong></td>
                <td>{r.category}</td>
                <td>{r.owner || '-'}</td>
                <td>{formatDate(r.reviewDate)}</td>
                <td style={{ fontSize: 13 }}>{r.mitigation || '-'}</td>
                <td>
                  <select value={r.status} onChange={(e) => setStatus(r.id, e.target.value as Status)} style={{ padding: 4 }}>
                    {STATUSES.map((s) => <option key={s} value={s}>{t(`risks.statuses.${s}`)}</option>)}
                  </select>
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

export default OpenRisks;
