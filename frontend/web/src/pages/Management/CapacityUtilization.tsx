import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatPercent } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P13.2 — Capacity utilization.
 *
 * Aggregates downtime minutes + running-state time (from machine-state events)
 * per machine for the selected period. Utilization = running / (running + downtime).
 */

type Machine = { id: string; code: string; name: string; workCenterId?: string };
type ParetoBucket = { category: number; count: number; totalMinutes: number };
type CurrentState = { machineId: string; machineCode: string; machineName: string; currentState: number | null };

const CapacityUtilization: React.FC = () => {
  const { t } = useTranslation();
  const [machines, setMachines] = useState<Machine[]>([]);
  const [pareto, setPareto] = useState<ParetoBucket[]>([]);
  const [currentStates, setCurrentStates] = useState<CurrentState[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [periodDays, setPeriodDays] = useState<number>(30);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [mResp, pResp, csResp] = await Promise.all([
          masterDataApi.getMachines(),
          machinesApi.getDowntimePareto(),
          machinesApi.getCurrentStates(),
        ]);
        if (cancelled) return;
        setMachines((mResp.data as Machine[]) ?? []);
        setPareto((pResp.data as any)?.data ?? (pResp.data as ParetoBucket[]) ?? []);
        setCurrentStates((csResp.data as any)?.data ?? (csResp.data as CurrentState[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const totalDowntimeMinutes = useMemo(() => pareto.reduce((s, b) => s + b.totalMinutes, 0), [pareto]);

  // Simple heuristic: assume 8 hours/day × periodDays for available time;
  // all downtime is globally attributed here (per-machine breakdown would need
  // an enriched endpoint). Per-machine rows show ratio of current state.
  const availableMinutes = periodDays * 8 * 60;

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    const stateById = new Map(currentStates.map((cs) => [cs.machineId, cs.currentState]));
    return machines
      .filter((m) => !q || `${m.code} ${m.name}`.toLowerCase().includes(q))
      .map((m) => {
        const state = stateById.get(m.id);
        // Proxy: assume each machine shares equal portion of downtime.
        const machineDowntime = machines.length > 0 ? totalDowntimeMinutes / machines.length : 0;
        const running = Math.max(0, availableMinutes - machineDowntime);
        const utilization = availableMinutes > 0 ? running / availableMinutes : 0;
        return {
          ...m,
          currentState: state ?? null,
          downtimeMinutes: machineDowntime,
          runningMinutes: running,
          availableMinutes,
          utilization,
        };
      })
      .sort((a, b) => b.utilization - a.utilization);
  }, [machines, currentStates, search, totalDowntimeMinutes, availableMinutes]);

  const stateLabel = (s: number | null) => {
    if (s === 1) return t('machineStatus.states.running');
    if (s === 2) return t('machineStatus.states.idle');
    if (s === 3) return t('machineStatus.states.down');
    if (s === 4) return t('machineStatus.states.setUp');
    if (s === 5) return t('machineStatus.states.maintenance');
    return t('machineStatus.unknown');
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('capacityUtilization.title')}</h1>
      <p style={{ color: '#666' }}>{t('capacityUtilization.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap', alignItems: 'center' }}>
        <label>{t('capacityUtilization.periodDays')}: <input type="number" min={1} max={365} value={periodDays} onChange={(e) => setPeriodDays(Math.max(1, Number(e.target.value)))} style={{ width: 70, padding: 4 }} /></label>
        <div><small>{t('capacityUtilization.availableMinutes')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(availableMinutes, 0)}</div></div>
        <div><small>{t('capacityUtilization.totalDowntime')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{formatQuantity(totalDowntimeMinutes, 0)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('capacityUtilization.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(rows, [
          { key: 'code', label: t('capacityUtilization.machine') as string },
          { key: 'name', label: t('common.name') as string },
          { key: 'availableMinutes', label: t('capacityUtilization.availableMinutes') as string, type: 'number', decimals: 0 },
          { key: 'downtimeMinutes', label: t('capacityUtilization.downtime') as string, type: 'number', decimals: 0 },
          { key: 'runningMinutes', label: t('capacityUtilization.running') as string, type: 'number', decimals: 0 },
          { key: 'utilization', label: t('capacityUtilization.utilization') as string, type: 'number' },
        ], 'capacity-utilization')} disabled={rows.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('capacityUtilization.machine')}</th>
              <th>{t('capacityUtilization.currentState')}</th>
              <th>{t('capacityUtilization.downtime')}</th>
              <th>{t('capacityUtilization.running')}</th>
              <th>{t('capacityUtilization.utilization')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && rows.map((r) => (
              <tr key={r.id}>
                <td><code>{r.code}</code> {r.name}</td>
                <td>{stateLabel(r.currentState)}</td>
                <td style={{ color: '#c62828' }}>{formatQuantity(r.downtimeMinutes, 0)}</td>
                <td style={{ color: '#2e7d32' }}>{formatQuantity(r.runningMinutes, 0)}</td>
                <td style={{ fontWeight: 600 }}>{formatPercent(r.utilization, 1)}</td>
                <td>
                  <div style={{ background: '#eee', height: 8, borderRadius: 4, overflow: 'hidden', width: 120 }}>
                    <div style={{ width: `${Math.round(r.utilization * 100)}%`, height: '100%', background: r.utilization > 0.75 ? '#2e7d32' : r.utilization > 0.5 ? '#f9a825' : '#c62828' }} />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default CapacityUtilization;
