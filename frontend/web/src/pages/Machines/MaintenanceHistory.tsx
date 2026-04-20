import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.5 — Maintenance work orders list per machine.
 *
 * Shows completed + open. Filters: machine, open-only toggle. "Complete"
 * action inline — hits `/maintenance-work-orders/{id}/complete`, which on
 * the backend rolls the linked schedule's NextDue forward (P11.4 dep).
 */

type WorkOrder = {
  id: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  scheduleId: string | null;
  scheduledDate: string;
  completedAt: string | null;
  technicianEmployeeId: string | null;
  taskDescription: string | null;
  notes: string | null;
  costImpact: number | null;
};

type Machine = { id: string; code: string; name: string };

const MaintenanceHistory: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<WorkOrder[]>([]);
  const [machines, setMachines] = useState<Machine[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [machineFilter, setMachineFilter] = useState<string>('');
  const [openOnly, setOpenOnly] = useState<boolean>(false);

  // Ad-hoc create
  const [showForm, setShowForm] = useState<boolean>(false);
  const [formMachine, setFormMachine] = useState<string>('');
  const [scheduledDate, setScheduledDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [task, setTask] = useState<string>('');
  const [costImpact, setCostImpact] = useState<string>('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await machinesApi.getWorkOrders({
        machineId: machineFilter || undefined,
        openOnly: openOnly || undefined,
      });
      const envelope = resp.data as { isSuccess?: boolean; data?: WorkOrder[] };
      setRows(envelope?.data ?? (resp.data as WorkOrder[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [machineFilter, openOnly]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const mResp = await masterDataApi.getMachines();
        if (!cancelled) {
          const list = (mResp.data as Machine[]) ?? [];
          setMachines(list);
          if (list.length > 0) setFormMachine(list[0].id);
        }
      } catch { /* ignore */ }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => { load(); }, [load]);

  const submit = async () => {
    if (!formMachine || !scheduledDate) return;
    setSaving(true);
    setError(null);
    try {
      await machinesApi.createWorkOrder({
        machineId: formMachine,
        scheduledDate: new Date(scheduledDate).toISOString(),
        taskDescription: task || null,
        costImpact: costImpact ? Number(costImpact) : null,
      });
      setShowForm(false);
      setTask('');
      setCostImpact('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const complete = async (id: string) => {
    if (!window.confirm(t('maintenanceHistory.confirmComplete'))) return;
    try {
      await machinesApi.completeWorkOrder(id, { completedAt: new Date().toISOString() });
      await load();
    } catch (err) {
      setError(translateError(err));
    }
  };

  const summary = useMemo(() => ({
    open: rows.filter((r) => !r.completedAt).length,
    completed: rows.filter((r) => r.completedAt).length,
    totalCost: rows.reduce((acc, r) => acc + (r.costImpact || 0), 0),
  }), [rows]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('maintenanceHistory.title')}</h1>
      <p style={{ color: '#666' }}>{t('maintenanceHistory.subtitle')}</p>

      <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 4, marginBottom: 12, display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('maintenanceHistory.machine')}:</label>
        <select value={machineFilter} onChange={(e) => setMachineFilter(e.target.value)} style={{ padding: 6 }}>
          <option value="">{t('maintenanceHistory.allMachines')}</option>
          {machines.map((m) => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}
        </select>
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <input type="checkbox" checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)} />
          {t('maintenanceHistory.openOnly')}
        </label>

        <span style={{ marginLeft: 'auto' }}>
          <small>{t('maintenanceHistory.open')}: <strong style={{ color: '#ef6c00' }}>{summary.open}</strong></small>
          &nbsp;·&nbsp;
          <small>{t('maintenanceHistory.completed')}: <strong style={{ color: '#2e7d32' }}>{summary.completed}</strong></small>
          &nbsp;·&nbsp;
          <small>{t('maintenanceHistory.totalCost')}: <strong>{formatQuantity(summary.totalCost)}</strong></small>
        </span>
        <button onClick={() => setShowForm((v) => !v)} style={{ padding: '6px 12px' }}>
          {showForm ? t('common.cancel') : t('maintenanceHistory.addWorkOrder')}
        </button>
        <button
          onClick={() => exportToCsv(
            rows,
            [
              { key: 'machineCode', label: 'Machine' },
              { key: 'scheduledDate', label: 'Scheduled', type: 'date' },
              { key: 'completedAt', label: 'Completed', type: 'date' },
              { key: 'taskDescription', label: 'Task', get: (w) => w.taskDescription ?? '' },
              { key: 'notes', label: 'Notes', get: (w) => w.notes ?? '' },
              { key: 'costImpact', label: 'CostImpact', type: 'number' },
            ],
            'maintenance-history'
          )}
          disabled={rows.length === 0}
          style={{ padding: '4px 10px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {showForm && (
        <div style={{ padding: 12, background: '#fff', border: '1px solid #ddd', borderRadius: 4, marginBottom: 12 }}>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
            <div>
              <label>{t('maintenanceHistory.machine')}</label>
              <select value={formMachine} onChange={(e) => setFormMachine(e.target.value)} style={{ padding: 6, display: 'block' }}>
                {machines.map((m) => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}
              </select>
            </div>
            <div>
              <label>{t('maintenanceHistory.scheduledDate')}</label>
              <input type="date" value={scheduledDate} onChange={(e) => setScheduledDate(e.target.value)} style={{ padding: 6, display: 'block' }} />
            </div>
            <div style={{ flex: 1, minWidth: 200 }}>
              <label>{t('maintenanceHistory.task')}</label>
              <input value={task} onChange={(e) => setTask(e.target.value)} placeholder={t('maintenanceHistory.taskPlaceholder') as string} style={{ padding: 6, width: '100%' }} />
            </div>
            <div>
              <label>{t('maintenanceHistory.costImpact')}</label>
              <input type="number" value={costImpact} onChange={(e) => setCostImpact(e.target.value)} style={{ padding: 6, width: 100 }} />
            </div>
            <button onClick={submit} disabled={saving} style={{ padding: '8px 16px', background: '#1976d2', color: '#fff', border: 'none' }}>
              {saving ? t('common.loading') : t('maintenanceHistory.create')}
            </button>
          </div>
        </div>
      )}

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('maintenanceHistory.machine')}</th>
              <th>{t('maintenanceHistory.scheduledDate')}</th>
              <th>{t('maintenanceHistory.completedAt')}</th>
              <th>{t('maintenanceHistory.task')}</th>
              <th>{t('maintenanceHistory.costImpact')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && rows.map((w) => {
              const isOpen = !w.completedAt;
              return (
                <tr key={w.id} style={isOpen ? { background: '#fff8e1' } : undefined}>
                  <td><code>{w.machineCode}</code> {w.machineName}</td>
                  <td>{formatDate(w.scheduledDate)}</td>
                  <td>{w.completedAt ? formatDate(w.completedAt) : <span style={{ color: '#ef6c00', fontWeight: 600 }}>{t('maintenanceHistory.openBadge')}</span>}</td>
                  <td style={{ fontSize: 13 }}>{w.taskDescription ?? '-'}</td>
                  <td>{w.costImpact != null ? formatQuantity(w.costImpact) : '-'}</td>
                  <td>{isOpen && <button onClick={() => complete(w.id)} style={{ padding: '4px 10px', fontSize: 12 }}>{t('maintenanceHistory.completeBtn')}</button>}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default MaintenanceHistory;
