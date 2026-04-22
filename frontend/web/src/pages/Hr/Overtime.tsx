import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hrApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P10.4 — Overtime tracker.
 *
 * Client-side rollup of attendance history: hours > standard-per-day
 * are overtime; totals by employee for the selected month. Standard hours
 * per day is configurable (default 8).
 */

type Attendance = {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  department: string | null;
  clockIn: string | null;
  clockOut: string | null;
  hours: number | null;
};
type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string; department?: string };

type Row = {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  department: string;
  days: number;
  totalHours: number;
  regularHours: number;
  overtimeHours: number;
};

const Overtime: React.FC = () => {
  const { t } = useTranslation();
  const today = new Date();
  const [month, setMonth] = useState(`${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`);
  const [standardHours, setStandardHours] = useState<number>(8);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [rows, setRows] = useState<Attendance[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    (async () => {
      try { const resp = await masterDataApi.getEmployees(); setEmployees((resp.data as Employee[]) ?? []); }
      catch (err) { setError(translateError(err)); }
    })();
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [yy, mm] = month.split('-').map(Number);
        const from = new Date(yy, mm - 1, 1).toISOString();
        const to = new Date(yy, mm, 0, 23, 59, 59).toISOString();
        const resp = await hrApi.getAttendanceHistory({ from, to });
        const env = resp.data as any;
        const data: Attendance[] = env?.data ?? env ?? [];
        if (!cancelled) setRows(data);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [month]);

  const aggregated = useMemo<Row[]>(() => {
    const byEmp = new Map<string, Row>();
    employees.forEach((e) => byEmp.set(e.id, {
      employeeId: e.id, employeeNumber: e.employeeNumber, fullName: `${e.firstName} ${e.lastName}`, department: e.department ?? '',
      days: 0, totalHours: 0, regularHours: 0, overtimeHours: 0,
    }));
    rows.forEach((att) => {
      if (att.hours == null) return;
      let row = byEmp.get(att.employeeId);
      if (!row) {
        row = { employeeId: att.employeeId, employeeNumber: att.employeeNumber, fullName: att.fullName, department: att.department ?? '', days: 0, totalHours: 0, regularHours: 0, overtimeHours: 0 };
        byEmp.set(att.employeeId, row);
      }
      row.days++;
      row.totalHours += att.hours;
      const reg = Math.min(att.hours, standardHours);
      const ot = Math.max(0, att.hours - standardHours);
      row.regularHours += reg;
      row.overtimeHours += ot;
    });
    return Array.from(byEmp.values()).filter((r) => r.totalHours > 0);
  }, [rows, employees, standardHours]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return aggregated
      .filter((r) => !q || `${r.employeeNumber} ${r.fullName} ${r.department}`.toLowerCase().includes(q))
      .sort((a, b) => b.overtimeHours - a.overtimeHours);
  }, [aggregated, search]);

  const totals = useMemo(() => filtered.reduce((acc, r) => {
    acc.total += r.totalHours; acc.regular += r.regularHours; acc.overtime += r.overtimeHours; return acc;
  }, { total: 0, regular: 0, overtime: 0 }), [filtered]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('overtime.title')}</h1>
      <p style={{ color: '#666' }}>{t('overtime.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('overtime.month')}: <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} style={{ padding: 4 }} /></label>
        <label>{t('overtime.standardHours')}: <input type="number" min={1} max={16} step="0.5" value={standardHours} onChange={(e) => setStandardHours(Number(e.target.value))} style={{ width: 70, padding: 4 }} /> {t('overtime.perDay')}</label>
        <div><small>{t('overtime.totalHours')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.total, 1)}</div></div>
        <div><small>{t('overtime.regularHours')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.regular, 1)}</div></div>
        <div><small>{t('overtime.overtimeHours')}</small><div style={{ fontWeight: 600, color: '#ef6c00' }}>{formatQuantity(totals.overtime, 1)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('overtime.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(filtered, [
          { key: 'employeeNumber', label: 'Emp#' },
          { key: 'fullName', label: t('overtime.name') as string },
          { key: 'department', label: t('overtime.department') as string },
          { key: 'days', label: t('overtime.days') as string, type: 'number', decimals: 0 },
          { key: 'totalHours', label: t('overtime.totalHours') as string, type: 'number' },
          { key: 'regularHours', label: t('overtime.regularHours') as string, type: 'number' },
          { key: 'overtimeHours', label: t('overtime.overtimeHours') as string, type: 'number' },
        ], `overtime-${month}`)}
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
              <th>Emp#</th>
              <th>{t('overtime.name')}</th>
              <th>{t('overtime.department')}</th>
              <th>{t('overtime.days')}</th>
              <th>{t('overtime.totalHours')}</th>
              <th>{t('overtime.regularHours')}</th>
              <th>{t('overtime.overtimeHours')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && filtered.map((r) => (
              <tr key={r.employeeId}>
                <td><code>{r.employeeNumber}</code></td>
                <td>{r.fullName}</td>
                <td>{r.department || '-'}</td>
                <td>{r.days}</td>
                <td>{formatQuantity(r.totalHours, 1)}</td>
                <td>{formatQuantity(r.regularHours, 1)}</td>
                <td style={{ color: r.overtimeHours > 0 ? '#ef6c00' : undefined, fontWeight: r.overtimeHours > 0 ? 600 : 400 }}>
                  {formatQuantity(r.overtimeHours, 1)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Overtime;
