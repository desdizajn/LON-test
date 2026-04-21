import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.4 — Preventive maintenance schedules per machine.
 *
 * List sorted by NextDue ascending; rows coloured by days-until-due
 * (red < 0, amber ≤ 7, green > 7). Inline create form at the top.
 * Editing inline (future); for now the plan is advanced when a work
 * order completes (P11.5 hooks into this via handler).
 */

type Schedule = {
  id: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  taskDescription: string;
  intervalDays: number;
  lastDone: string | null;
  nextDue: string;
  daysUntilDue: number;
  isActive: boolean;
};

type Machine = { id: string; code: string; name: string };

const MaintenancePlan: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Schedule[]>([]);
  const [machines, setMachines] = useState<Machine[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [machineId, setMachineId] = useState<string>('');
  const [task, setTask] = useState<string>('');
  const [intervalDays, setIntervalDays] = useState<number>(30);
  const [nextDue, setNextDue] = useState<string>('');
  const [lastDone, setLastDone] = useState<string>('');
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [riskFilter, setRiskFilter] = useState<'all' | 'overdue' | 'soon'>('all');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await machinesApi.getSchedules(false);
      const envelope = resp.data as { isSuccess?: boolean; data?: Schedule[] };
      setRows(envelope?.data ?? (resp.data as Schedule[]) ?? []);
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
    if (!machineId || !task.trim() || !intervalDays) {
      setError(t('maintenancePlan.invalid'));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await machinesApi.createSchedule({
        machineId,
        taskDescription: task.trim(),
        intervalDays,
        lastDone: lastDone || null,
        nextDue: nextDue || null,
      });
      setTask('');
      setNextDue('');
      setLastDone('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const riskColor = (d: number) => d < 0 ? '#c62828' : d <= 7 ? '#f9a825' : '#2e7d32';

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (riskFilter === 'overdue' && r.daysUntilDue >= 0) return false;
      if (riskFilter === 'soon' && (r.daysUntilDue < 0 || r.daysUntilDue > 7)) return false;
      if (q) {
        const hay = `${r.machineCode} ${r.machineName} ${r.taskDescription}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [rows, search, riskFilter]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('maintenancePlan.title')}</h1>
      <p style={{ color: '#666' }}>{t('maintenancePlan.subtitle')}</p>

      <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 4, marginBottom: 12 }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <div>
            <label>{t('maintenancePlan.machine')}</label>
            <select value={machineId} onChange={(e) => setMachineId(e.target.value)} style={{ padding: 6, display: 'block' }}>
              {machines.map((m) => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}
            </select>
          </div>
          <div style={{ flex: 1, minWidth: 240 }}>
            <label>{t('maintenancePlan.task')}</label>
            <input value={task} onChange={(e) => setTask(e.target.value)} placeholder={t('maintenancePlan.taskPlaceholder') as string} style={{ padding: 6, width: '100%' }} />
          </div>
          <div>
            <label>{t('maintenancePlan.intervalDays')}</label>
            <input type="number" value={intervalDays} onChange={(e) => setIntervalDays(Number(e.target.value))} min={1} style={{ padding: 6, width: 90 }} />
          </div>
          <div>
            <label>{t('maintenancePlan.lastDone')}</label>
            <input type="date" value={lastDone} onChange={(e) => setLastDone(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div>
            <label>{t('maintenancePlan.nextDue')}</label>
            <input type="date" value={nextDue} onChange={(e) => setNextDue(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <button onClick={submit} disabled={saving || !machineId || !task.trim()} style={{ padding: '8px 16px', background: '#1976d2', color: '#fff', border: 'none' }}>
            {saving ? t('common.loading') : t('maintenancePlan.addSchedule')}
          </button>
        </div>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 8, marginBottom: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('maintenancePlan.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 200 }}
        />
        <select value={riskFilter} onChange={(e) => setRiskFilter(e.target.value as typeof riskFilter)} style={{ padding: 6 }}>
          <option value="all">{t('maintenancePlan.riskAll')}</option>
          <option value="overdue">{t('maintenancePlan.riskOverdue')}</option>
          <option value="soon">{t('maintenancePlan.riskSoon')}</option>
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('maintenancePlan.rowCount', { count: filteredRows.length })}</span>
        <button
          onClick={() => exportToCsv(
            filteredRows,
            [
              { key: 'machineCode', label: 'Machine' },
              { key: 'taskDescription', label: 'Task' },
              { key: 'intervalDays', label: 'IntervalDays', type: 'number' },
              { key: 'lastDone', label: 'LastDone', type: 'date' },
              { key: 'nextDue', label: 'NextDue', type: 'date' },
              { key: 'daysUntilDue', label: 'DaysUntilDue', type: 'number' },
              { key: 'isActive', label: 'Active' },
            ],
            'maintenance-plan'
          )}
          disabled={filteredRows.length === 0}
          style={{ padding: '4px 10px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('maintenancePlan.machine')}</th>
              <th>{t('maintenancePlan.task')}</th>
              <th>{t('maintenancePlan.intervalDays')}</th>
              <th>{t('maintenancePlan.lastDone')}</th>
              <th>{t('maintenancePlan.nextDue')}</th>
              <th>{t('maintenancePlan.daysUntilDue')}</th>
              <th>{t('maintenancePlan.active')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filteredRows.length === 0 && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{search || riskFilter !== 'all' ? t('inventory.filters.noResults') : t('common.noData')}</td></tr>}
            {!loading && filteredRows.map((r) => (
              <tr key={r.id}>
                <td><code>{r.machineCode}</code> {r.machineName}</td>
                <td style={{ fontSize: 13 }}>{r.taskDescription}</td>
                <td>{r.intervalDays}</td>
                <td>{formatDate(r.lastDone)}</td>
                <td>{formatDate(r.nextDue)}</td>
                <td style={{ color: riskColor(r.daysUntilDue), fontWeight: 600 }}>{r.daysUntilDue}</td>
                <td>{r.isActive ? '✓' : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default MaintenancePlan;
