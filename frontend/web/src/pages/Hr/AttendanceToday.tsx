import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hrApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDateTime, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P10.1 — Today's attendance roster.
 *
 * Lists every active employee + their attendance row for today. Inline
 * Clock-in / Clock-out actions call the HR API directly; the handler upserts
 * one row per (Employee, Date). Hours are computed on clock-out.
 */

type Row = {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  department: string | null;
  attendanceId: string | null;
  clockIn: string | null;
  clockOut: string | null;
  hours: number | null;
  status: number | null;
};

const STATUS = [
  { value: 1, key: 'present', bg: '#c8e6c9', color: '#2e7d32' },
  { value: 2, key: 'late', bg: '#ffe0b2', color: '#ef6c00' },
  { value: 3, key: 'absent', bg: '#ffcdd2', color: '#c62828' },
  { value: 4, key: 'excused', bg: '#e0e0e0', color: '#616161' },
  { value: 5, key: 'onLeave', bg: '#bbdefb', color: '#1565c0' },
];

const AttendanceToday: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [filter, setFilter] = useState<string>('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await hrApi.getAttendanceToday();
      const envelope = resp.data as { isSuccess?: boolean; data?: Row[] };
      setRows(envelope?.data ?? (resp.data as Row[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const clockIn = async (employeeId: string) => {
    setBusy(employeeId);
    setError(null);
    try { await hrApi.clockIn({ employeeId }); await load(); }
    catch (err) { setError(translateError(err)); }
    finally { setBusy(null); }
  };

  const clockOut = async (employeeId: string) => {
    setBusy(employeeId);
    setError(null);
    try { await hrApi.clockOut({ employeeId }); await load(); }
    catch (err) { setError(translateError(err)); }
    finally { setBusy(null); }
  };

  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((r) =>
      r.employeeNumber.toLowerCase().includes(q) ||
      r.fullName.toLowerCase().includes(q) ||
      (r.department ?? '').toLowerCase().includes(q)
    );
  }, [rows, filter]);

  const summary = useMemo(() => {
    const present = rows.filter((r) => r.clockIn && !r.clockOut).length;
    const done = rows.filter((r) => r.clockOut).length;
    const notStarted = rows.filter((r) => !r.clockIn).length;
    const totalHours = rows.reduce((acc, r) => acc + (r.hours || 0), 0);
    return { present, done, notStarted, totalHours };
  }, [rows]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('attendance.title')}</h1>
      <p style={{ color: '#666' }}>{t('attendance.subtitle')}</p>

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap', alignItems: 'center' }}>
        <div><small>{t('attendance.clockedIn')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{summary.present}</div></div>
        <div><small>{t('attendance.clockedOut')}</small><div style={{ fontWeight: 600, color: '#1565c0' }}>{summary.done}</div></div>
        <div><small>{t('attendance.notStarted')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{summary.notStarted}</div></div>
        <div><small>{t('attendance.totalHours')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(summary.totalHours)}</div></div>

        <input
          placeholder={t('attendance.searchPlaceholder') as string}
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{ padding: 6, marginLeft: 'auto', minWidth: 200 }}
        />
        <button
          onClick={() => exportToCsv(
            rows,
            [
              { key: 'employeeNumber', label: 'Emp#' },
              { key: 'fullName', label: 'Name' },
              { key: 'department', label: 'Department', get: (r) => r.department ?? '' },
              { key: 'clockIn', label: 'ClockIn', type: 'date' },
              { key: 'clockOut', label: 'ClockOut', type: 'date' },
              { key: 'hours', label: 'Hours', type: 'number' },
              { key: 'status', label: 'Status', get: (r) => r.status == null ? '-' : (STATUS.find((s) => s.value === r.status)?.key ?? '-') },
            ],
            'attendance-today'
          )}
          disabled={rows.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('attendance.employeeNumber')}</th>
              <th>{t('attendance.name')}</th>
              <th>{t('attendance.department')}</th>
              <th>{t('attendance.clockIn')}</th>
              <th>{t('attendance.clockOut')}</th>
              <th>{t('attendance.hours')}</th>
              <th>{t('attendance.status')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && filtered.map((r) => {
              const isIdle = !r.clockIn;
              const isIn = r.clockIn && !r.clockOut;
              const s = r.status != null ? STATUS.find((x) => x.value === r.status) : null;
              return (
                <tr key={r.employeeId}>
                  <td><code>{r.employeeNumber}</code></td>
                  <td><strong>{r.fullName}</strong></td>
                  <td>{r.department ?? '-'}</td>
                  <td>{formatDateTime(r.clockIn)}</td>
                  <td>{formatDateTime(r.clockOut)}</td>
                  <td>{r.hours != null ? formatQuantity(r.hours) : '-'}</td>
                  <td>
                    {s && <span style={{ padding: '2px 8px', borderRadius: 12, background: s.bg, color: s.color, fontSize: 12, fontWeight: 600 }}>{t(`attendance.statuses.${s.key}`)}</span>}
                    {!s && <span style={{ color: '#888', fontSize: 12 }}>—</span>}
                  </td>
                  <td>
                    {isIdle && <button onClick={() => clockIn(r.employeeId)} disabled={busy === r.employeeId} style={{ padding: '4px 10px', fontSize: 12, background: '#2e7d32', color: '#fff', border: 'none' }}>{t('attendance.clockInBtn')}</button>}
                    {isIn && <button onClick={() => clockOut(r.employeeId)} disabled={busy === r.employeeId} style={{ padding: '4px 10px', fontSize: 12, background: '#1565c0', color: '#fff', border: 'none' }}>{t('attendance.clockOutBtn')}</button>}
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

export default AttendanceToday;
