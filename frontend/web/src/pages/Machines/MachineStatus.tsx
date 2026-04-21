import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDateTime } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.1 — Live machine status dashboard.
 *
 * Consumes `GET /Machines/current-states` (one row per active Machine, latest
 * MachineStateEvent joined). Inline state-change: click a pill → pick the new
 * state in a modal → POST /Machines/{id}/state-events → reload. Manual input
 * until telemetry lands.
 */

type CurrentState = {
  machineId: string;
  machineCode: string;
  machineName: string;
  workCenterId: string;
  workCenterCode: string;
  currentState: number | null;
  since: string | null;
  notes: string | null;
};

// Matches MachineState enum
const STATES = [
  { value: 1, key: 'running', color: '#2e7d32', bg: '#c8e6c9' },
  { value: 2, key: 'idle', color: '#616161', bg: '#e0e0e0' },
  { value: 3, key: 'down', color: '#c62828', bg: '#ffcdd2' },
  { value: 4, key: 'setUp', color: '#1565c0', bg: '#bbdefb' },
  { value: 5, key: 'maintenance', color: '#ef6c00', bg: '#ffe0b2' },
];

const StatePill: React.FC<{ state: number | null; t: (k: string) => string }> = ({ state, t }) => {
  if (state == null) {
    return <span style={{ padding: '2px 8px', borderRadius: 12, background: '#eee', color: '#888', fontSize: 12 }}>{t('machineStatus.unknown')}</span>;
  }
  const s = STATES.find((x) => x.value === state) ?? STATES[1];
  return <span style={{ padding: '2px 8px', borderRadius: 12, background: s.bg, color: s.color, fontSize: 12, fontWeight: 600 }}>{t(`machineStatus.states.${s.key}`)}</span>;
};

const MachineStatus: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<CurrentState[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<CurrentState | null>(null);
  const [newState, setNewState] = useState<number>(1);
  const [newNotes, setNewNotes] = useState<string>('');
  const [saving, setSaving] = useState(false);
  const [search, setSearch] = useState('');
  const [stateFilter, setStateFilter] = useState<string>('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await machinesApi.getCurrentStates();
      const envelope = resp.data as { isSuccess?: boolean; data?: CurrentState[] };
      setRows(envelope?.data ?? (resp.data as CurrentState[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const summary = useMemo(() => {
    const counts = Object.fromEntries(STATES.map((s) => [s.value, 0])) as Record<number, number>;
    let unknown = 0;
    rows.forEach((r) => {
      if (r.currentState == null) unknown += 1;
      else counts[r.currentState] = (counts[r.currentState] || 0) + 1;
    });
    return { counts, unknown };
  }, [rows]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (stateFilter === '-1' && r.currentState !== null) return false;
      if (stateFilter && stateFilter !== '-1' && String(r.currentState) !== stateFilter) return false;
      if (q) {
        const hay = `${r.machineCode} ${r.machineName} ${r.workCenterCode} ${r.notes ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [rows, search, stateFilter]);

  const openEdit = (row: CurrentState) => {
    setEditing(row);
    setNewState(row.currentState ?? 1);
    setNewNotes('');
  };

  const saveState = async () => {
    if (!editing) return;
    setSaving(true);
    try {
      await machinesApi.logState(editing.machineId, { state: newState, notes: newNotes || undefined });
      setEditing(null);
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('machineStatus.title')}</h1>
      <p style={{ color: '#666' }}>{t('machineStatus.subtitle')}</p>

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap' }}>
        {STATES.map((s) => (
          <div key={s.value}>
            <small>{t(`machineStatus.states.${s.key}`)}</small>
            <div style={{ fontWeight: 600, color: s.color }}>{summary.counts[s.value]}</div>
          </div>
        ))}
        <div>
          <small>{t('machineStatus.unknown')}</small>
          <div style={{ fontWeight: 600, color: '#888' }}>{summary.unknown}</div>
        </div>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('machineStatus.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 200 }}
        />
        <select value={stateFilter} onChange={(e) => setStateFilter(e.target.value)} style={{ padding: 6 }}>
          <option value="">{t('machineStatus.stateAll')}</option>
          {STATES.map((s) => (
            <option key={s.value} value={String(s.value)}>{t(`machineStatus.states.${s.key}`)}</option>
          ))}
          <option value="-1">{t('machineStatus.unknown')}</option>
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('machineStatus.rowCount', { count: filtered.length })}</span>
        <button
          onClick={() => exportToCsv(
            filtered,
            [
              { key: 'machineCode', label: 'Code' },
              { key: 'machineName', label: 'Name' },
              { key: 'workCenterCode', label: 'WorkCenter' },
              { key: 'currentState', label: 'State', get: (r) => r.currentState == null ? '-' : (STATES.find((s) => s.value === r.currentState)?.key ?? '-') },
              { key: 'since', label: 'Since', get: (r) => r.since, type: 'date' },
              { key: 'notes', label: 'Notes', get: (r) => r.notes ?? '' },
            ],
            'machine-status'
          )}
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && (
        <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>
      )}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('machineStatus.code')}</th>
              <th>{t('machineStatus.name')}</th>
              <th>{t('machineStatus.workCenter')}</th>
              <th>{t('machineStatus.state')}</th>
              <th>{t('machineStatus.since')}</th>
              <th>{t('machineStatus.notes')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{search || stateFilter ? t('inventory.filters.noResults') : t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((r) => (
              <tr key={r.machineId}>
                <td><code>{r.machineCode}</code></td>
                <td>{r.machineName}</td>
                <td>{r.workCenterCode}</td>
                <td><StatePill state={r.currentState} t={t} /></td>
                <td>{formatDateTime(r.since)}</td>
                <td style={{ color: '#666', fontSize: 13 }}>{r.notes ?? '-'}</td>
                <td><button onClick={() => openEdit(r)} style={{ padding: '4px 10px', fontSize: 12 }}>{t('machineStatus.changeState')}</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editing && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: 20, borderRadius: 4, minWidth: 360 }}>
            <h3 style={{ marginTop: 0 }}>{t('machineStatus.changeTitle', { code: editing.machineCode })}</h3>
            <div style={{ marginBottom: 12 }}>
              <label>{t('machineStatus.newState')}:</label>
              <select value={newState} onChange={(e) => setNewState(Number(e.target.value))} style={{ width: '100%', padding: 6, marginTop: 4 }}>
                {STATES.map((s) => (
                  <option key={s.value} value={s.value}>{t(`machineStatus.states.${s.key}`)}</option>
                ))}
              </select>
            </div>
            <div style={{ marginBottom: 12 }}>
              <label>{t('machineStatus.notes')}:</label>
              <textarea value={newNotes} onChange={(e) => setNewNotes(e.target.value)} rows={2} style={{ width: '100%', padding: 6, marginTop: 4, fontFamily: 'inherit' }} />
            </div>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
              <button onClick={() => setEditing(null)} style={{ padding: '6px 14px' }} disabled={saving}>{t('common.cancel')}</button>
              <button onClick={saveState} style={{ padding: '6px 14px', background: '#1976d2', color: '#fff', border: 'none' }} disabled={saving}>{t('common.save')}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default MachineStatus;
