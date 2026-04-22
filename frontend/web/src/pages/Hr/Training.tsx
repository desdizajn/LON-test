import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P10.6 — Training records.
 *
 * Lightweight training-log per employee. Stored in browser localStorage per
 * tenant until a real TrainingRecord backend entity lands. Supports
 * certification expiry + skills taxonomy.
 */

type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string; department?: string };

type TrainingRecord = {
  id: string;
  employeeId: string;
  employeeName: string;
  topic: string;
  skillArea: string;
  provider: string;
  completionDate: string;
  expiryDate: string;
  certificate: string;
  notes: string;
};

const storageKey = (tenantId: string) => `lon.training.${tenantId || 'default'}`;
function currentTenantId(): string {
  try {
    const raw = localStorage.getItem('token') || '';
    const part = raw.split('.')[1];
    if (!part) return 'default';
    const p = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    return p['tenant_id'] || 'default';
  } catch { return 'default'; }
}

const SKILL_AREAS = ['Sewing', 'Cutting', 'Quality Control', 'Machine Operation', 'Safety', 'Customs', 'IT', 'Management', 'Other'];

const TrainingPage: React.FC = () => {
  const { t } = useTranslation();
  const today = new Date().toISOString().slice(0, 10);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [rows, setRows] = useState<TrainingRecord[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [expiringOnly, setExpiringOnly] = useState(false);

  const [draft, setDraft] = useState<TrainingRecord>({
    id: '', employeeId: '', employeeName: '', topic: '', skillArea: 'Sewing',
    provider: '', completionDate: today, expiryDate: '', certificate: '', notes: '',
  });

  const tenantId = currentTenantId();

  useEffect(() => {
    (async () => {
      try { const resp = await masterDataApi.getEmployees(); setEmployees((resp.data as Employee[]) ?? []); }
      catch (err) { setError(translateError(err)); }
    })();
  }, []);

  useEffect(() => {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw) { try { setRows(JSON.parse(raw)); } catch { /* ignore */ } }
  }, [tenantId]);

  function persist(next: TrainingRecord[]) { setRows(next); localStorage.setItem(storageKey(tenantId), JSON.stringify(next)); }

  function add() {
    if (!draft.employeeId || !draft.topic.trim()) { toast.error(t('training.invalid') as string); return; }
    const emp = employees.find((e) => e.id === draft.employeeId);
    persist([{
      ...draft,
      id: crypto.randomUUID(),
      employeeName: emp ? `${emp.firstName} ${emp.lastName}` : draft.employeeId,
    }, ...rows]);
    setDraft({ ...draft, id: '', topic: '', provider: '', certificate: '', notes: '' });
    toast.success(t('training.saved') as string);
  }

  function remove(id: string) {
    if (!window.confirm(t('training.confirmDelete') as string)) return;
    persist(rows.filter((r) => r.id !== id));
  }

  const enriched = useMemo(() => {
    const now = Date.now();
    return rows.map((r) => {
      const exp = r.expiryDate ? new Date(r.expiryDate).getTime() : 0;
      const daysLeft = exp ? Math.round((exp - now) / 86_400_000) : null;
      return { ...r, daysLeft };
    });
  }, [rows]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return enriched.filter((r) => {
      if (expiringOnly && (r.daysLeft === null || r.daysLeft > 60)) return false;
      if (q && !`${r.employeeName} ${r.topic} ${r.skillArea} ${r.provider} ${r.certificate}`.toLowerCase().includes(q)) return false;
      return true;
    }).sort((a, b) => {
      if (a.daysLeft === null) return 1;
      if (b.daysLeft === null) return -1;
      return a.daysLeft - b.daysLeft;
    });
  }, [enriched, expiringOnly, search]);

  const expiringCount = enriched.filter((r) => r.daysLeft !== null && r.daysLeft <= 60 && r.daysLeft >= 0).length;
  const expiredCount = enriched.filter((r) => r.daysLeft !== null && r.daysLeft < 0).length;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('training.title')}</h1>
      <p style={{ color: '#666' }}>{t('training.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('training.newLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('training.employee')}
            <select value={draft.employeeId} onChange={(e) => setDraft({ ...draft, employeeId: e.target.value })} style={{ padding: 6, width: '100%' }}>
              <option value="">—</option>
              {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} · {e.firstName} {e.lastName}</option>)}
            </select>
          </label>
          <label>{t('training.topic')}
            <input type="text" value={draft.topic} onChange={(e) => setDraft({ ...draft, topic: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.skillArea')}
            <select value={draft.skillArea} onChange={(e) => setDraft({ ...draft, skillArea: e.target.value })} style={{ padding: 6, width: '100%' }}>
              {SKILL_AREAS.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
          <label>{t('training.provider')}
            <input type="text" value={draft.provider} onChange={(e) => setDraft({ ...draft, provider: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.completionDate')}
            <input type="date" value={draft.completionDate} onChange={(e) => setDraft({ ...draft, completionDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.expiryDate')}
            <input type="date" value={draft.expiryDate} onChange={(e) => setDraft({ ...draft, expiryDate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('training.certificate')}
            <input type="text" value={draft.certificate} onChange={(e) => setDraft({ ...draft, certificate: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={add} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {t('training.add')}
          </button>
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('training.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <label><input type="checkbox" checked={expiringOnly} onChange={(e) => setExpiringOnly(e.target.checked)} /> {t('training.expiringOnly')}</label>
        <span style={{ color: '#ef6c00' }}>{t('training.expiring', { count: expiringCount })}</span>
        <span style={{ color: '#c62828' }}>{t('training.expired', { count: expiredCount })}</span>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('training.rowCount', { count: filtered.length })}</span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'employeeName', label: t('training.employee') as string },
          { key: 'topic', label: t('training.topic') as string },
          { key: 'skillArea', label: t('training.skillArea') as string },
          { key: 'provider', label: t('training.provider') as string },
          { key: 'completionDate', label: t('training.completionDate') as string, type: 'date' },
          { key: 'expiryDate', label: t('training.expiryDate') as string, type: 'date' },
          { key: 'certificate', label: t('training.certificate') as string },
        ], 'training')}
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('training.employee')}</th>
              <th>{t('training.topic')}</th>
              <th>{t('training.skillArea')}</th>
              <th>{t('training.completionDate')}</th>
              <th>{t('training.expiryDate')}</th>
              <th>{t('training.daysLeft')}</th>
              <th>{t('training.certificate')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('training.empty')}</td></tr>}
            {filtered.map((r) => (
              <tr key={r.id}>
                <td><strong>{r.employeeName}</strong></td>
                <td>{r.topic}</td>
                <td>{r.skillArea}</td>
                <td>{formatDate(r.completionDate)}</td>
                <td>{r.expiryDate ? formatDate(r.expiryDate) : '-'}</td>
                <td style={{ color: r.daysLeft === null ? '#888' : r.daysLeft < 0 ? '#c62828' : r.daysLeft <= 60 ? '#ef6c00' : '#2e7d32', fontWeight: 600 }}>
                  {r.daysLeft === null ? '-' : r.daysLeft < 0 ? t('training.expiredBadge') : r.daysLeft}
                </td>
                <td>{r.certificate || '-'}</td>
                <td><button onClick={() => remove(r.id)} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default TrainingPage;
