import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hrApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P10.2 — Absence register with approve / reject workflow.
 *
 * Pending absences (Approved == null) are highlighted; approver action
 * stamps `ApprovedByUserId` + `ApprovedAt`. Filter by employee +
 * pending-only toggle.
 */

type Absence = {
  id: string;
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  from: string;
  to: string;
  type: number;
  reason: string | null;
  approved: boolean | null;
  approvedByUserId: string | null;
  approvedAt: string | null;
};

type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string };

const TYPES = [
  { value: 1, key: 'sick' },
  { value: 2, key: 'vacation' },
  { value: 3, key: 'personal' },
  { value: 4, key: 'parental' },
  { value: 5, key: 'unpaid' },
  { value: 99, key: 'other' },
];

const Absences: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Absence[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pendingOnly, setPendingOnly] = useState<boolean>(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('');

  // Form
  const [employeeId, setEmployeeId] = useState<string>('');
  const [from, setFrom] = useState<string>(new Date().toISOString().slice(0, 10));
  const [to, setTo] = useState<string>(new Date().toISOString().slice(0, 10));
  const [type, setType] = useState<number>(1);
  const [reason, setReason] = useState<string>('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await hrApi.getAbsences({ pendingOnly: pendingOnly || undefined });
      const envelope = resp.data as { isSuccess?: boolean; data?: Absence[] };
      setRows(envelope?.data ?? (resp.data as Absence[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, [pendingOnly]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const eResp = await masterDataApi.getEmployees();
        if (!cancelled) {
          const list = (eResp.data as Employee[]) ?? [];
          setEmployees(list);
          if (list.length > 0 && !employeeId) setEmployeeId(list[0].id);
        }
      } catch { /* ignore */ }
    })();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => { load(); }, [load]);

  const submit = async () => {
    if (!employeeId || !from || !to) return;
    setSaving(true);
    setError(null);
    try {
      await hrApi.createAbsence({
        employeeId,
        from: new Date(from).toISOString(),
        to: new Date(to).toISOString(),
        type,
        reason: reason.trim() || null,
      });
      setReason('');
      await load();
    } catch (err) {
      setError(translateError(err));
    } finally {
      setSaving(false);
    }
  };

  const decide = async (id: string, approve: boolean) => {
    setBusy(id);
    setError(null);
    try { await hrApi.decideAbsence(id, approve); await load(); }
    catch (err) { setError(translateError(err)); }
    finally { setBusy(null); }
  };

  const pendingCount = useMemo(() => rows.filter((r) => r.approved == null).length, [rows]);

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (typeFilter && String(r.type) !== typeFilter) return false;
      if (q) {
        const hay = `${r.employeeNumber} ${r.fullName} ${r.reason ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [rows, search, typeFilter]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('absences.title')}</h1>
      <p style={{ color: '#666' }}>{t('absences.subtitle')}</p>

      {/* Create form */}
      <div style={{ padding: 12, background: '#f5f5f5', borderRadius: 4, marginBottom: 12 }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'end' }}>
          <div>
            <label>{t('absences.employee')}</label>
            <select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} style={{ padding: 6, display: 'block', minWidth: 220 }}>
              {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} — {e.firstName} {e.lastName}</option>)}
            </select>
          </div>
          <div>
            <label>{t('absences.type')}</label>
            <select value={type} onChange={(e) => setType(Number(e.target.value))} style={{ padding: 6, display: 'block' }}>
              {TYPES.map((tt) => <option key={tt.value} value={tt.value}>{t(`absences.types.${tt.key}`)}</option>)}
            </select>
          </div>
          <div>
            <label>{t('absences.from')}</label>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div>
            <label>{t('absences.to')}</label>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} style={{ padding: 6, display: 'block' }} />
          </div>
          <div style={{ flex: 1, minWidth: 200 }}>
            <label>{t('absences.reason')}</label>
            <input value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t('absences.reasonPlaceholder') as string} style={{ padding: 6, width: '100%' }} />
          </div>
          <button onClick={submit} disabled={saving || !employeeId} style={{ padding: '8px 16px', background: '#1976d2', color: '#fff', border: 'none' }}>
            {saving ? t('common.loading') : t('absences.submit')}
          </button>
        </div>
      </div>

      {/* Controls */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <input type="checkbox" checked={pendingOnly} onChange={(e) => setPendingOnly(e.target.checked)} />
          {t('absences.pendingOnly')}
        </label>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('absences.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 200 }}
        />
        <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)} style={{ padding: 6 }}>
          <option value="">{t('absences.typeAll')}</option>
          {TYPES.map((tt) => <option key={tt.value} value={String(tt.value)}>{t(`absences.types.${tt.key}`)}</option>)}
        </select>
        <span style={{ color: '#ef6c00' }}>{t('absences.pendingCount', { count: pendingCount })}</span>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('absences.rowCount', { count: filteredRows.length })}</span>
        <button
          onClick={() => exportToCsv(
            filteredRows,
            [
              { key: 'employeeNumber', label: 'Emp#' },
              { key: 'fullName', label: 'Name' },
              { key: 'type', label: 'Type', get: (r) => TYPES.find((t) => t.value === r.type)?.key ?? 'other' },
              { key: 'from', label: 'From', type: 'date' },
              { key: 'to', label: 'To', type: 'date' },
              { key: 'reason', label: 'Reason', get: (r) => r.reason ?? '' },
              { key: 'approved', label: 'Approved', get: (r) => r.approved === true ? 'yes' : r.approved === false ? 'no' : 'pending' },
            ],
            'absences'
          )}
          disabled={filteredRows.length === 0}
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
              <th>{t('absences.employee')}</th>
              <th>{t('absences.type')}</th>
              <th>{t('absences.from')}</th>
              <th>{t('absences.to')}</th>
              <th>{t('absences.reason')}</th>
              <th>{t('absences.statusCol')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filteredRows.length === 0 && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{search || typeFilter ? t('inventory.filters.noResults') : t('common.noData')}</td></tr>}
            {!loading && filteredRows.map((r) => {
              const typeKey = TYPES.find((t) => t.value === r.type)?.key ?? 'other';
              const isPending = r.approved == null;
              return (
                <tr key={r.id} style={isPending ? { background: '#fff8e1' } : undefined}>
                  <td><code>{r.employeeNumber}</code> {r.fullName}</td>
                  <td>{t(`absences.types.${typeKey}`)}</td>
                  <td>{formatDate(r.from)}</td>
                  <td>{formatDate(r.to)}</td>
                  <td style={{ fontSize: 13 }}>{r.reason ?? '-'}</td>
                  <td>
                    {r.approved === true && <span style={{ color: '#2e7d32', fontWeight: 600 }}>{t('absences.approved')}</span>}
                    {r.approved === false && <span style={{ color: '#c62828', fontWeight: 600 }}>{t('absences.rejected')}</span>}
                    {r.approved == null && <span style={{ color: '#ef6c00', fontWeight: 600 }}>{t('absences.pending')}</span>}
                  </td>
                  <td>
                    {isPending && (
                      <div style={{ display: 'flex', gap: 6 }}>
                        <button onClick={() => decide(r.id, true)} disabled={busy === r.id} style={{ padding: '4px 10px', fontSize: 12, background: '#2e7d32', color: '#fff', border: 'none' }}>{t('absences.approve')}</button>
                        <button onClick={() => decide(r.id, false)} disabled={busy === r.id} style={{ padding: '4px 10px', fontSize: 12, background: '#c62828', color: '#fff', border: 'none' }}>{t('absences.reject')}</button>
                      </div>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Absences;
