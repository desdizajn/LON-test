import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hrApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P10.5 — Operator ↔ machine assignment shift board.
 *
 * List + create + end-range. Active-only toggle filters by
 * `ValidFrom <= now <= (ValidTo ?? infinity)`.
 */

type Assignment = {
  id: string;
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  validFrom: string;
  validTo: string | null;
  notes: string | null;
};

type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string };
type Machine = { id: string; code: string; name: string };

const OperatorAssignment: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Assignment[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [machines, setMachines] = useState<Machine[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeOnly, setActiveOnly] = useState<boolean>(true);
  const [busy, setBusy] = useState<string | null>(null);

  // Form
  const [employeeId, setEmployeeId] = useState<string>('');
  const [machineId, setMachineId] = useState<string>('');
  const [validFrom, setValidFrom] = useState<string>(new Date().toISOString().slice(0, 10));
  const [validTo, setValidTo] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await hrApi.getAssignments({ activeOnly: activeOnly || undefined });
      const envelope = resp.data as { isSuccess?: boolean; data?: Assignment[] };
      setRows(envelope?.data ?? (resp.data as Assignment[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [activeOnly]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [eResp, mResp] = await Promise.all([masterDataApi.getEmployees(), masterDataApi.getMachines()]);
        if (cancelled) return;
        const el = (eResp.data as Employee[]) ?? [];
        const ml = (mResp.data as Machine[]) ?? [];
        setEmployees(el);
        setMachines(ml);
        if (el.length > 0) setEmployeeId(el[0].id);
        if (ml.length > 0) setMachineId(ml[0].id);
      } catch { /* ignore */ }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => { load(); }, [load]);

  const submit = async () => {
    if (!employeeId || !machineId || !validFrom) return;
    setSaving(true);
    setError(null);
    try {
      await hrApi.createAssignment({
        employeeId,
        machineId,
        validFrom: new Date(validFrom).toISOString(),
        validTo: validTo ? new Date(validTo).toISOString() : null,
        notes: notes.trim() || null,
      });
      setNotes('');
      setValidTo('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const end = async (id: string) => {
    if (!window.confirm(t('assignments.confirmEnd'))) return;
    setBusy(id);
    setError(null);
    try { await hrApi.endAssignment(id, new Date().toISOString()); await load(); }
    catch (err) { setError(translateError(err)); }
    finally { setBusy(null); }
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('assignments.title')}</h1>
      <p style={{ color: '#666' }}>{t('assignments.subtitle')}</p>

      <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 4, marginBottom: 12 }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <div>
            <label>{t('assignments.employee')}</label>
            <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} style={{ padding: 6, display: 'block', minWidth: 220 }}>
              {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} — {e.firstName} {e.lastName}</option>)}
            </select>
          </div>
          <div>
            <label>{t('assignments.machine')}</label>
            <select value={machineId} onChange={(e) => setMachineId(e.target.value)} style={{ padding: 6, display: 'block', minWidth: 180 }}>
              {machines.map((m) => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}
            </select>
          </div>
          <div>
            <label>{t('assignments.validFrom')}</label>
            <input type="date" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div>
            <label>{t('assignments.validToOptional')}</label>
            <input type="date" value={validTo} onChange={(e) => setValidTo(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div style={{ flex: 1, minWidth: 180 }}>
            <label>{t('assignments.notes')}</label>
            <input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder={t('assignments.notesPlaceholder') as string} style={{ padding: 6, width: '100%' }} />
          </div>
          <button onClick={submit} disabled={saving || !employeeId || !machineId} style={{ padding: '8px 16px', background: '#1976d2', color: '#fff', border: 'none' }}>
            {saving ? t('common.loading') : t('assignments.assign')}
          </button>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 12, marginBottom: 8, alignItems: 'center' }}>
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />
          {t('assignments.activeOnly')}
        </label>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('assignments.rowCount', { count: rows.length })}</span>
        <button
          onClick={() => exportToCsv(
            rows,
            [
              { key: 'employeeNumber', label: 'Emp#' },
              { key: 'fullName', label: 'Name' },
              { key: 'machineCode', label: 'Machine' },
              { key: 'machineName', label: 'MachineName' },
              { key: 'validFrom', label: 'From', type: 'date' },
              { key: 'validTo', label: 'To', type: 'date' },
              { key: 'notes', label: 'Notes', get: (a) => a.notes ?? '' },
            ],
            'operator-assignments'
          )}
          disabled={rows.length === 0}
          style={{ padding: '4px 10px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('assignments.employee')}</th>
              <th>{t('assignments.machine')}</th>
              <th>{t('assignments.validFrom')}</th>
              <th>{t('assignments.validTo')}</th>
              <th>{t('assignments.notes')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && rows.map((a) => {
              const isOpen = !a.validTo;
              return (
                <tr key={a.id} style={isOpen ? { background: '#e8f5e9' } : undefined}>
                  <td><code>{a.employeeNumber}</code> {a.fullName}</td>
                  <td><code>{a.machineCode}</code> {a.machineName}</td>
                  <td>{formatDate(a.validFrom)}</td>
                  <td>{a.validTo ? formatDate(a.validTo) : <span style={{ color: '#2e7d32', fontWeight: 600 }}>{t('assignments.openEnded')}</span>}</td>
                  <td style={{ fontSize: 13 }}>{a.notes ?? '-'}</td>
                  <td>{isOpen && <button onClick={() => end(a.id)} disabled={busy === a.id} style={{ padding: '4px 10px', fontSize: 12 }}>{t('assignments.endNow')}</button>}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default OperatorAssignment;
