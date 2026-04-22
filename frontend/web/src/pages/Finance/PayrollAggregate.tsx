import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { hrApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P12.6 — Плати (aggregate).
 *
 * Aggregates attendance hours × hourly-rate-per-employee (rate stored in
 * browser localStorage per tenant until a HR payroll entity lands) into
 * a period payroll total. Supports month selector + overtime premium.
 */

type Employee = { id: string; employeeNumber: string; firstName: string; lastName: string; department?: string };

type AttendanceRow = {
  employeeId: string;
  employeeNumber: string;
  fullName: string;
  department: string | null;
  clockIn: string | null;
  clockOut: string | null;
  hours: number | null;
};

type Rate = { employeeId: string; ratePerHour: number; currency: string };

const rateKey = (tenantId: string) => `lon.payrollRates.${tenantId || 'default'}`;
function currentTenantId(): string {
  try {
    const raw = localStorage.getItem('token') || '';
    const part = raw.split('.')[1];
    if (!part) return 'default';
    const p = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/')));
    return p['tenant_id'] || 'default';
  } catch { return 'default'; }
}

const PayrollAggregate: React.FC = () => {
  const { t } = useTranslation();
  const today = new Date();
  const [month, setMonth] = useState<string>(`${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [attendanceByEmp, setAttendanceByEmp] = useState<Record<string, number>>({});
  const [rates, setRates] = useState<Rate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [overtimeMultiplier, setOvertimeMultiplier] = useState<number>(1.5);
  const [standardHoursPerDay, setStandardHoursPerDay] = useState<number>(8);
  const [search, setSearch] = useState('');

  const tenantId = currentTenantId();

  useEffect(() => {
    (async () => {
      try {
        const resp = await masterDataApi.getEmployees();
        setEmployees((resp.data as Employee[]) ?? []);
      } catch (err) { setError(translateError(err)); }
    })();
  }, []);

  useEffect(() => {
    const raw = localStorage.getItem(rateKey(tenantId));
    if (raw) { try { setRates(JSON.parse(raw)); } catch { /* ignore */ } }
  }, [tenantId]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        // Attendance across the month; per-day aggregate.
        const [yy, mm] = month.split('-').map(Number);
        const start = new Date(yy, mm - 1, 1);
        const end = new Date(yy, mm, 0);
        const resp = await hrApi.getAttendanceHistory({ from: start.toISOString(), to: end.toISOString() });
        const env = resp.data as { data?: AttendanceRow[] } | AttendanceRow[];
        const rows: AttendanceRow[] = (Array.isArray(env) ? env : (env as any)?.data) ?? [];
        const byEmp: Record<string, number> = {};
        rows.forEach((r) => {
          if (r.hours != null) byEmp[r.employeeId] = (byEmp[r.employeeId] ?? 0) + r.hours;
        });
        if (!cancelled) setAttendanceByEmp(byEmp);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [month]);

  function setRate(employeeId: string, ratePerHour: number) {
    const next = rates.filter((r) => r.employeeId !== employeeId);
    if (ratePerHour > 0) next.push({ employeeId, ratePerHour, currency: 'EUR' });
    setRates(next);
    localStorage.setItem(rateKey(tenantId), JSON.stringify(next));
  }

  const rateByEmp = useMemo(() => new Map(rates.map((r) => [r.employeeId, r.ratePerHour])), [rates]);

  const [yy, mm] = month.split('-').map(Number);
  const daysInMonth = new Date(yy, mm, 0).getDate();
  const standardMonthlyHours = daysInMonth * standardHoursPerDay;

  const computed = useMemo(() => {
    const q = search.trim().toLowerCase();
    return employees
      .filter((e) => !q || `${e.employeeNumber} ${e.firstName} ${e.lastName} ${e.department ?? ''}`.toLowerCase().includes(q))
      .map((e) => {
        const totalHours = attendanceByEmp[e.id] ?? 0;
        const rate = rateByEmp.get(e.id) ?? 0;
        const regularHours = Math.min(totalHours, standardMonthlyHours);
        const overtimeHours = Math.max(0, totalHours - standardMonthlyHours);
        const regularPay = regularHours * rate;
        const overtimePay = overtimeHours * rate * overtimeMultiplier;
        const totalPay = regularPay + overtimePay;
        return { ...e, totalHours, rate, regularHours, overtimeHours, regularPay, overtimePay, totalPay };
      });
  }, [employees, attendanceByEmp, rateByEmp, standardMonthlyHours, overtimeMultiplier, search]);

  const totals = useMemo(() => computed.reduce((acc, r) => {
    acc.hours += r.totalHours;
    acc.pay += r.totalPay;
    return acc;
  }, { hours: 0, pay: 0 }), [computed]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('payrollAggregate.title')}</h1>
      <p style={{ color: '#666' }}>{t('payrollAggregate.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('payrollAggregate.month')}: <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} style={{ padding: 4 }} /></label>
        <label>{t('payrollAggregate.standardHours')}: <input type="number" min={1} max={12} step="0.5" value={standardHoursPerDay} onChange={(e) => setStandardHoursPerDay(Number(e.target.value))} style={{ width: 70, padding: 4 }} /> {t('payrollAggregate.perDay')}</label>
        <label>{t('payrollAggregate.overtimeX')}: <input type="number" min={1} max={3} step="0.1" value={overtimeMultiplier} onChange={(e) => setOvertimeMultiplier(Number(e.target.value))} style={{ width: 60, padding: 4 }} />×</label>
        <div><small>{t('payrollAggregate.totalHours')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.hours, 1)}</div></div>
        <div><small>{t('payrollAggregate.totalPay')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.pay, 2)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('payrollAggregate.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(computed, [
          { key: 'employeeNumber', label: 'Emp#' },
          { key: 'fullName', label: t('payrollAggregate.fullName') as string, get: (r) => `${r.firstName} ${r.lastName}` },
          { key: 'department', label: t('payrollAggregate.department') as string },
          { key: 'totalHours', label: t('payrollAggregate.hours') as string, type: 'number' },
          { key: 'rate', label: t('payrollAggregate.rate') as string, type: 'number' },
          { key: 'regularHours', label: t('payrollAggregate.regularHours') as string, type: 'number' },
          { key: 'overtimeHours', label: t('payrollAggregate.overtimeHours') as string, type: 'number' },
          { key: 'regularPay', label: t('payrollAggregate.regularPay') as string, type: 'number' },
          { key: 'overtimePay', label: t('payrollAggregate.overtimePay') as string, type: 'number' },
          { key: 'totalPay', label: t('payrollAggregate.totalPay') as string, type: 'number' },
        ], `payroll-${month}`)}
          disabled={computed.length === 0}
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
              <th>{t('payrollAggregate.fullName')}</th>
              <th>{t('payrollAggregate.hours')}</th>
              <th>{t('payrollAggregate.rate')}</th>
              <th>{t('payrollAggregate.regularPay')}</th>
              <th>{t('payrollAggregate.overtimeHours')}</th>
              <th>{t('payrollAggregate.overtimePay')}</th>
              <th>{t('payrollAggregate.totalPay')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && computed.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && computed.map((r) => (
              <tr key={r.id}>
                <td><code>{r.employeeNumber}</code></td>
                <td>{r.firstName} {r.lastName}</td>
                <td>{formatQuantity(r.totalHours, 1)}</td>
                <td>
                  <input type="number" min={0} step="0.1" value={r.rate} onChange={(e) => setRate(r.id, Number(e.target.value))} style={{ width: 70, padding: 4 }} />
                </td>
                <td>{formatQuantity(r.regularPay, 2)}</td>
                <td style={{ color: r.overtimeHours > 0 ? '#ef6c00' : undefined }}>{formatQuantity(r.overtimeHours, 1)}</td>
                <td>{formatQuantity(r.overtimePay, 2)}</td>
                <td style={{ fontWeight: 600 }}>{formatQuantity(r.totalPay, 2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default PayrollAggregate;
