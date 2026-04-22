import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { masterDataApi, api } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P12.5 — Cost accounting.
 *
 * Cost-per-minute matrix per Work Center × Shift. Used downstream by margin
 * and P&L calculations when a real operation-time feed is available.
 *
 * Persistence: browser localStorage scoped by tenant. When the backend
 * CostRate entity lands, the load/save helpers below will swap to an API.
 */

type WorkCenter = { id: string; code: string; name: string };
type Shift = { id: string; code: string; name: string };

type CostRow = {
  workCenterId: string;
  shiftId: string;
  ratePerMinute: number;
  currency: string;
  notes: string;
};

const storageKey = (tenantId: string) => `lon.costAccounting.${tenantId || 'default'}`;

function currentTenantId(): string {
  try {
    const raw = localStorage.getItem('token') || '';
    const part = raw.split('.')[1];
    if (!part) return 'default';
    const payload = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    return payload['tenant_id'] || 'default';
  } catch { return 'default'; }
}

const CostAccounting: React.FC = () => {
  const { t } = useTranslation();
  const [workCenters, setWorkCenters] = useState<WorkCenter[]>([]);
  const [shifts, setShifts] = useState<Shift[]>([]);
  const [rows, setRows] = useState<CostRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  // Inline editor
  const [draft, setDraft] = useState<CostRow>({ workCenterId: '', shiftId: '', ratePerMinute: 0, currency: 'EUR', notes: '' });

  const tenantId = currentTenantId();

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [wcResp, shResp] = await Promise.all([
          masterDataApi.getWorkCenters(),
          api.get('/shifts'),
        ]);
        if (cancelled) return;
        setWorkCenters((wcResp.data as WorkCenter[]) ?? []);
        setShifts((shResp.data as Shift[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw) {
      try { setRows(JSON.parse(raw)); } catch { /* ignore */ }
    }
  }, [tenantId]);

  function persist(next: CostRow[]) {
    setRows(next);
    localStorage.setItem(storageKey(tenantId), JSON.stringify(next));
  }

  function upsert() {
    if (!draft.workCenterId || !draft.shiftId || draft.ratePerMinute <= 0) {
      toast.error(t('costAccounting.invalid') as string);
      return;
    }
    const next = rows.filter((r) => !(r.workCenterId === draft.workCenterId && r.shiftId === draft.shiftId));
    next.push({ ...draft });
    persist(next);
    toast.success(t('costAccounting.saved') as string);
    setDraft({ workCenterId: '', shiftId: '', ratePerMinute: 0, currency: draft.currency, notes: '' });
  }

  function remove(workCenterId: string, shiftId: string) {
    persist(rows.filter((r) => !(r.workCenterId === workCenterId && r.shiftId === shiftId)));
  }

  const enriched = useMemo(() => {
    const wcById = new Map(workCenters.map((w) => [w.id, w]));
    const shById = new Map(shifts.map((s) => [s.id, s]));
    return rows.map((r) => ({
      ...r,
      workCenterCode: wcById.get(r.workCenterId)?.code ?? r.workCenterId,
      workCenterName: wcById.get(r.workCenterId)?.name ?? '-',
      shiftCode: shById.get(r.shiftId)?.code ?? r.shiftId,
      shiftName: shById.get(r.shiftId)?.name ?? '-',
    }));
  }, [rows, workCenters, shifts]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return enriched;
    return enriched.filter((r) => `${r.workCenterCode} ${r.workCenterName} ${r.shiftCode} ${r.shiftName}`.toLowerCase().includes(q));
  }, [enriched, search]);

  const avgRate = useMemo(() => {
    if (filtered.length === 0) return 0;
    return filtered.reduce((s, r) => s + r.ratePerMinute, 0) / filtered.length;
  }, [filtered]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('costAccounting.title')}</h1>
      <p style={{ color: '#666' }}>{t('costAccounting.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <fieldset style={{ border: '1px solid #ddd', borderRadius: 4, padding: 12, marginBottom: 12 }}>
        <legend>{t('costAccounting.upsertLegend')}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8, alignItems: 'end' }}>
          <label>{t('costAccounting.workCenter')}
            <select value={draft.workCenterId} onChange={(e) => setDraft({ ...draft, workCenterId: e.target.value })} style={{ padding: 6, width: '100%' }}>
              <option value="">—</option>
              {workCenters.map((w) => <option key={w.id} value={w.id}>{w.code} · {w.name}</option>)}
            </select>
          </label>
          <label>{t('costAccounting.shift')}
            <select value={draft.shiftId} onChange={(e) => setDraft({ ...draft, shiftId: e.target.value })} style={{ padding: 6, width: '100%' }}>
              <option value="">—</option>
              {shifts.map((s) => <option key={s.id} value={s.id}>{s.code} · {s.name}</option>)}
            </select>
          </label>
          <label>{t('costAccounting.ratePerMinute')}
            <input type="number" step="0.01" min={0} value={draft.ratePerMinute} onChange={(e) => setDraft({ ...draft, ratePerMinute: Number(e.target.value) })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('costAccounting.currency')}
            <input type="text" maxLength={3} value={draft.currency} onChange={(e) => setDraft({ ...draft, currency: e.target.value.toUpperCase() })} style={{ padding: 6, width: '100%' }} />
          </label>
          <label>{t('costAccounting.notes')}
            <input type="text" value={draft.notes} onChange={(e) => setDraft({ ...draft, notes: e.target.value })} style={{ padding: 6, width: '100%' }} />
          </label>
          <button onClick={upsert} style={{ padding: '8px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {t('costAccounting.upsert')}
          </button>
        </div>
        <div style={{ fontSize: 11, color: '#888', marginTop: 8 }}>
          {t('costAccounting.storageHint')}
        </div>
      </fieldset>

      <div style={{ display: 'flex', gap: 12, marginBottom: 10, alignItems: 'center' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('costAccounting.searchPlaceholder') as string} style={{ padding: 6, minWidth: 240 }} />
        <span style={{ color: '#888' }}>{t('costAccounting.rowCount', { count: filtered.length })}</span>
        <span style={{ color: '#555', marginLeft: 'auto' }}>
          {t('costAccounting.avgRate')}: <strong>{formatQuantity(avgRate, 4)}</strong> / {t('costAccounting.minute')}
        </span>
        <button onClick={() => exportToCsv(enriched, [
          { key: 'workCenterCode', label: t('costAccounting.workCenter') as string },
          { key: 'workCenterName', label: t('common.name') as string },
          { key: 'shiftCode', label: t('costAccounting.shift') as string },
          { key: 'shiftName', label: t('common.name') as string },
          { key: 'ratePerMinute', label: t('costAccounting.ratePerMinute') as string, type: 'number', decimals: 4 },
          { key: 'currency', label: t('costAccounting.currency') as string },
          { key: 'notes', label: t('costAccounting.notes') as string },
        ], 'cost-accounting')}
          disabled={enriched.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('costAccounting.workCenter')}</th>
              <th>{t('costAccounting.shift')}</th>
              <th>{t('costAccounting.ratePerMinute')}</th>
              <th>{t('costAccounting.currency')}</th>
              <th>{t('costAccounting.notes')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('costAccounting.empty')}</td></tr>}
            {!loading && filtered.map((r) => (
              <tr key={`${r.workCenterId}-${r.shiftId}`}>
                <td><code>{r.workCenterCode}</code> {r.workCenterName}</td>
                <td><code>{r.shiftCode}</code> {r.shiftName}</td>
                <td><strong>{formatQuantity(r.ratePerMinute, 4)}</strong></td>
                <td>{r.currency}</td>
                <td style={{ fontSize: 13 }}>{r.notes || '-'}</td>
                <td>
                  <button onClick={() => remove(r.workCenterId, r.shiftId)} style={{ padding: '4px 10px', fontSize: 12, color: '#c62828' }}>×</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default CostAccounting;
