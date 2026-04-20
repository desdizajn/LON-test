import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDateTime, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.2 — Downtime log with Pareto-by-reason summary.
 *
 * Two sections: (1) downtime event list (open events highlighted, close
 * button per row), (2) category rollup `{category, count, totalMinutes}`
 * sorted desc by totalMinutes. New event logged via inline form at the
 * top. All durations are computed server-side when an event is closed.
 */

type Event = {
  id: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  start: string;
  end: string | null;
  durationMinutes: number | null;
  category: number;
  reason: string;
  costImpact: number | null;
  reportedByEmployeeId: string | null;
};

type ParetoBucket = { category: number; count: number; totalMinutes: number };
type Machine = { id: string; code: string; name: string };

const CATEGORIES = [
  { value: 1, key: 'breakdown' },
  { value: 2, key: 'missingMaterial' },
  { value: 3, key: 'missingOperator' },
  { value: 4, key: 'changeover' },
  { value: 5, key: 'quality' },
  { value: 6, key: 'powerOrUtility' },
  { value: 99, key: 'other' },
];

const toIsoLocal = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);

const MachineDowntime: React.FC = () => {
  const { t } = useTranslation();
  const [events, setEvents] = useState<Event[]>([]);
  const [pareto, setPareto] = useState<ParetoBucket[]>([]);
  const [machines, setMachines] = useState<Machine[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form
  const [machineId, setMachineId] = useState<string>('');
  const [start, setStart] = useState<string>(toIsoLocal(new Date()));
  const [end, setEnd] = useState<string>('');
  const [category, setCategory] = useState<number>(1);
  const [reason, setReason] = useState<string>('');
  const [costImpact, setCostImpact] = useState<string>('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [evResp, paretoResp] = await Promise.all([
        machinesApi.getDowntime(),
        machinesApi.getDowntimePareto(),
      ]);
      const unwrap = <T,>(resp: any): T => resp.data?.data ?? resp.data ?? [];
      setEvents(unwrap<Event[]>(evResp));
      setPareto(unwrap<ParetoBucket[]>(paretoResp));
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const mResp = await masterDataApi.getMachines();
        if (!cancelled) {
          const list = (mResp.data as Machine[]) ?? [];
          setMachines(list);
          if (list.length > 0 && !machineId) setMachineId(list[0].id);
        }
      } catch { /* ignore */ }
    })();
    load();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const submit = async () => {
    if (!machineId || !reason.trim()) {
      setError(t('downtime.reasonRequired'));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await machinesApi.logDowntime({
        machineId,
        start: new Date(start).toISOString(),
        end: end ? new Date(end).toISOString() : null,
        category,
        reason: reason.trim(),
        costImpact: costImpact ? Number(costImpact) : null,
      });
      setReason('');
      setEnd('');
      setCostImpact('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const close = async (id: string) => {
    if (!window.confirm(t('downtime.confirmClose'))) return;
    try {
      await machinesApi.closeDowntime(id, new Date().toISOString());
      await load();
    } catch (err) {
      setError(translateError(err));
    }
  };

  const openCount = useMemo(() => events.filter((e) => !e.end).length, [events]);
  const totalMinutes = useMemo(() => pareto.reduce((acc, b) => acc + (b.totalMinutes || 0), 0), [pareto]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('downtime.title')}</h1>
      <p style={{ color: '#666' }}>{t('downtime.subtitle')}</p>

      {/* Logger form */}
      <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 4, marginBottom: 12 }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <div>
            <label>{t('downtime.machine')}</label>
            <select value={machineId} onChange={(e) => setMachineId(e.target.value)} style={{ padding: 6, display: 'block' }}>
              {machines.map((m) => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}
            </select>
          </div>
          <div>
            <label>{t('downtime.category')}</label>
            <select value={category} onChange={(e) => setCategory(Number(e.target.value))} style={{ padding: 6, display: 'block' }}>
              {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{t(`downtime.categories.${c.key}`)}</option>)}
            </select>
          </div>
          <div>
            <label>{t('downtime.start')}</label>
            <input type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div>
            <label>{t('downtime.endOptional')}</label>
            <input type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div style={{ flex: 1, minWidth: 200 }}>
            <label>{t('downtime.reason')}</label>
            <input value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t('downtime.reasonPlaceholder') as string} style={{ padding: 6, width: '100%' }} />
          </div>
          <div>
            <label>{t('downtime.costImpact')}</label>
            <input type="number" value={costImpact} onChange={(e) => setCostImpact(e.target.value)} style={{ padding: 6, width: 100 }} />
          </div>
          <button onClick={submit} disabled={saving || !machineId || !reason.trim()} style={{ padding: '8px 16px', background: '#1976d2', color: '#fff', border: 'none' }}>
            {saving ? t('common.loading') : t('downtime.log')}
          </button>
        </div>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      {/* Pareto */}
      <h2>{t('downtime.paretoSection')}</h2>
      <p style={{ color: '#666', marginTop: -8 }}>{t('downtime.totalMinutes', { total: formatQuantity(totalMinutes) })}</p>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('downtime.category')}</th>
              <th>{t('downtime.count')}</th>
              <th>{t('downtime.totalMinutesCol')}</th>
              <th>{t('downtime.share')}</th>
            </tr>
          </thead>
          <tbody>
            {pareto.length === 0 && <tr><td colSpan={4} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {pareto.map((b) => {
              const key = CATEGORIES.find((c) => c.value === b.category)?.key ?? 'other';
              const share = totalMinutes > 0 ? b.totalMinutes / totalMinutes : 0;
              return (
                <tr key={b.category}>
                  <td>{t(`downtime.categories.${key}`)}</td>
                  <td>{b.count}</td>
                  <td>{formatQuantity(b.totalMinutes)}</td>
                  <td>
                    <div style={{ background: '#eee', height: 8, borderRadius: 4, overflow: 'hidden', minWidth: 120 }}>
                      <div style={{ width: `${Math.round(share * 100)}%`, height: '100%', background: '#1976d2' }} />
                    </div>
                    <small>{(share * 100).toFixed(1)}%</small>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Event list */}
      <h2 style={{ marginTop: 24 }}>{t('downtime.eventsSection', { count: events.length, open: openCount })}</h2>
      <div style={{ display: 'flex', marginBottom: 8 }}>
        <button
          onClick={() => exportToCsv(
            events,
            [
              { key: 'machineCode', label: 'Machine' },
              { key: 'start', label: 'Start', type: 'date' },
              { key: 'end', label: 'End', type: 'date' },
              { key: 'durationMinutes', label: 'Minutes', type: 'number' },
              { key: 'category', label: 'Category', get: (e) => CATEGORIES.find((c) => c.value === e.category)?.key ?? 'other' },
              { key: 'reason', label: 'Reason' },
              { key: 'costImpact', label: 'CostImpact', type: 'number' },
            ],
            'downtime-events'
          )}
          disabled={events.length === 0}
          style={{ padding: '4px 10px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('downtime.machine')}</th>
              <th>{t('downtime.start')}</th>
              <th>{t('downtime.end')}</th>
              <th>{t('downtime.minutes')}</th>
              <th>{t('downtime.category')}</th>
              <th>{t('downtime.reason')}</th>
              <th>{t('downtime.costImpact')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && events.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && events.map((e) => {
              const key = CATEGORIES.find((c) => c.value === e.category)?.key ?? 'other';
              const isOpen = !e.end;
              return (
                <tr key={e.id} style={isOpen ? { background: '#fff3e0' } : undefined}>
                  <td><code>{e.machineCode}</code></td>
                  <td>{formatDateTime(e.start)}</td>
                  <td>{e.end ? formatDateTime(e.end) : <span style={{ color: '#ef6c00', fontWeight: 600 }}>{t('downtime.open')}</span>}</td>
                  <td>{e.durationMinutes != null ? formatQuantity(e.durationMinutes) : '-'}</td>
                  <td>{t(`downtime.categories.${key}`)}</td>
                  <td style={{ fontSize: 13 }}>{e.reason}</td>
                  <td>{e.costImpact != null ? formatQuantity(e.costImpact) : '-'}</td>
                  <td>{isOpen && <button onClick={() => close(e.id)} style={{ padding: '4px 10px', fontSize: 12 }}>{t('downtime.closeBtn')}</button>}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default MachineDowntime;
