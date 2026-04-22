import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hrApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P10.5 — Performance (employee × production output).
 *
 * Proxy until a real operator-to-production-receipt link exists: aggregates
 * hours worked per employee + per-machine production receipts via operator
 * assignments (employeeId → machineId → orders that used that machine).
 * For now, shows hours + assigned machines count; detailed per-piece output
 * lands with P8.9 OperationTimeLog.
 */

type Attendance = { employeeId: string; employeeNumber: string; fullName: string; hours: number | null };
type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string; department?: string };
type Assignment = { employeeId: string; machineId: string; validFrom: string; validTo: string | null };

type Row = {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  department: string;
  days: number;
  hours: number;
  activeAssignments: number;
  productivityIndex: number;
};

const Performance: React.FC = () => {
  const { t } = useTranslation();
  const today = new Date();
  const [month, setMonth] = useState(`${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [attendance, setAttendance] = useState<Attendance[]>([]);
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const [eResp, aResp] = await Promise.all([
          masterDataApi.getEmployees(),
          hrApi.getAssignments({ activeOnly: true }),
        ]);
        setEmployees((eResp.data as Employee[]) ?? []);
        const ae = aResp.data as any;
        setAssignments(ae?.data ?? ae ?? []);
      } catch (err) { setError(translateError(err)); }
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
        if (!cancelled) setAttendance(env?.data ?? env ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally { if (!cancelled) setLoading(false); }
    })();
    return () => { cancelled = true; };
  }, [month]);

  const rows = useMemo<Row[]>(() => {
    const byEmp = new Map<string, Row>();
    employees.forEach((e) => byEmp.set(e.id, {
      employeeId: e.id, employeeNumber: e.employeeNumber, fullName: `${e.firstName} ${e.lastName}`, department: e.department ?? '',
      days: 0, hours: 0, activeAssignments: 0, productivityIndex: 0,
    }));
    attendance.forEach((a) => {
      if (a.hours == null) return;
      let row = byEmp.get(a.employeeId);
      if (!row) {
        row = { employeeId: a.employeeId, employeeNumber: a.employeeNumber, fullName: a.fullName, department: '', days: 0, hours: 0, activeAssignments: 0, productivityIndex: 0 };
        byEmp.set(a.employeeId, row);
      }
      row.days++; row.hours += a.hours;
    });
    assignments.forEach((as) => {
      const row = byEmp.get(as.employeeId);
      if (row) row.activeAssignments++;
    });
    // Productivity index = hours × activeAssignments (simple proxy)
    byEmp.forEach((row) => {
      row.productivityIndex = Math.round(row.hours * (1 + 0.1 * row.activeAssignments));
    });
    return Array.from(byEmp.values()).filter((r) => r.hours > 0 || r.activeAssignments > 0);
  }, [employees, attendance, assignments]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows
      .filter((r) => !q || `${r.employeeNumber} ${r.fullName} ${r.department}`.toLowerCase().includes(q))
      .sort((a, b) => b.productivityIndex - a.productivityIndex);
  }, [rows, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('performance.title')}</h1>
      <p style={{ color: '#666' }}>{t('performance.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('performance.month')}: <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} style={{ padding: 4 }} /></label>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('performance.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('performance.rowCount', { count: filtered.length })}</span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'employeeNumber', label: 'Emp#' },
          { key: 'fullName', label: t('performance.name') as string },
          { key: 'department', label: t('performance.department') as string },
          { key: 'days', label: t('performance.days') as string, type: 'number', decimals: 0 },
          { key: 'hours', label: t('performance.hours') as string, type: 'number' },
          { key: 'activeAssignments', label: t('performance.assignments') as string, type: 'number', decimals: 0 },
          { key: 'productivityIndex', label: t('performance.index') as string, type: 'number', decimals: 0 },
        ], `performance-${month}`)}
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
              <th>{t('performance.name')}</th>
              <th>{t('performance.department')}</th>
              <th>{t('performance.days')}</th>
              <th>{t('performance.hours')}</th>
              <th>{t('performance.assignments')}</th>
              <th>{t('performance.index')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.map((r) => (
              <tr key={r.employeeId}>
                <td><code>{r.employeeNumber}</code></td>
                <td>{r.fullName}</td>
                <td>{r.department || '-'}</td>
                <td>{r.days}</td>
                <td>{formatQuantity(r.hours, 1)}</td>
                <td>{r.activeAssignments}</td>
                <td style={{ fontWeight: 600 }}>{r.productivityIndex}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Performance;
