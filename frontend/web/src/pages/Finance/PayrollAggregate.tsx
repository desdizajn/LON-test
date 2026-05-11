import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'react-toastify';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import {
  PAYROLL_STATUS_LABEL,
  PayrollLineDto,
  PayrollPeriodDto,
  useCreatePayrollPeriod,
  useExportPayrollPeriod,
  useFinalizePayrollPeriod,
  usePayrollPeriodsQuery,
  useUpdatePayrollLine,
} from '../../hooks/queries/usePayroll';

/**
 * P16.C3.b — Payroll periods backed by PayrollPeriod + PayrollLine.
 *
 * Pick a month → seed (or fetch) the period; lines are pre-filled from
 * Attendance + approved Absence. Operator enters NetAmount per line
 * (rate × hours plus/minus bonus/deduction is computed in their head
 * or via the inline helper; not persisted). Finalize freezes the lines;
 * Export stamps ExportedAt.
 */

function monthRange(month: string): { start: string; end: string } {
  const [yy, mm] = month.split('-').map(Number);
  const start = new Date(yy, mm - 1, 1);
  const end = new Date(yy, mm, 0);
  return {
    start: start.toISOString().slice(0, 10),
    end: end.toISOString().slice(0, 10),
  };
}

const PayrollAggregate: React.FC = () => {
  const { t } = useTranslation();
  const today = new Date();
  const [month, setMonth] = useState<string>(
    `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`
  );
  const [standardHoursPerDay, setStandardHoursPerDay] = useState<number>(8);
  const [search, setSearch] = useState('');

  const { data: periods = [], isLoading } = usePayrollPeriodsQuery();
  const createMut = useCreatePayrollPeriod();
  const updateLineMut = useUpdatePayrollLine();
  const finalizeMut = useFinalizePayrollPeriod();
  const exportMut = useExportPayrollPeriod();

  const range = useMemo(() => monthRange(month), [month]);
  const activePeriod = useMemo<PayrollPeriodDto | undefined>(
    () => periods.find((p) => p.periodStart.slice(0, 10) === range.start && p.periodEnd.slice(0, 10) === range.end),
    [periods, range]
  );

  async function ensurePeriod() {
    if (activePeriod) return;
    try {
      await createMut.mutateAsync({
        periodStart: range.start,
        periodEnd: range.end,
        standardHoursPerDay,
      });
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  async function updateLine(line: PayrollLineDto, patch: Partial<PayrollLineDto>) {
    try {
      await updateLineMut.mutateAsync({
        id: line.id,
        regularHours: patch.regularHours ?? line.regularHours,
        overtimeHours: patch.overtimeHours ?? line.overtimeHours,
        absenceHours: patch.absenceHours ?? line.absenceHours,
        bonusAmount: patch.bonusAmount ?? line.bonusAmount,
        deductionAmount: patch.deductionAmount ?? line.deductionAmount,
        netAmount: patch.netAmount ?? line.netAmount,
        currency: patch.currency ?? line.currency,
      });
    } catch (err: any) {
      toast.error(err.message || 'Failed');
    }
  }

  const isDraft = activePeriod?.status === 1;
  const filteredLines = useMemo(() => {
    if (!activePeriod) return [];
    const q = search.trim().toLowerCase();
    if (!q) return activePeriod.lines;
    return activePeriod.lines.filter((l) =>
      `${l.employeeName ?? ''} ${l.employeeNumber ?? ''}`.toLowerCase().includes(q)
    );
  }, [activePeriod, search]);

  const totals = useMemo(() => filteredLines.reduce(
    (acc, l) => ({
      hours: acc.hours + l.regularHours + l.overtimeHours,
      net: acc.net + l.netAmount,
    }),
    { hours: 0, net: 0 }
  ), [filteredLines]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('payrollAggregate.title')}</h1>
      <p style={{ color: '#666' }}>{t('payrollAggregate.subtitle')}</p>

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('payrollAggregate.month')}: <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} style={{ padding: 4 }} /></label>
        <label>{t('payrollAggregate.standardHours')}: <input type="number" min={1} max={12} step="0.5" value={standardHoursPerDay} onChange={(e) => setStandardHoursPerDay(Number(e.target.value))} style={{ width: 70, padding: 4 }} /> {t('payrollAggregate.perDay')}</label>
        {activePeriod ? (
          <span style={{ padding: '4px 10px', background: '#e3f2fd', borderRadius: 4, fontSize: 12 }}>
            {PAYROLL_STATUS_LABEL[activePeriod.status]} · {activePeriod.lines.length} lines
            {activePeriod.exportedAt && ` · exported ${new Date(activePeriod.exportedAt).toLocaleDateString()}`}
          </span>
        ) : (
          <button onClick={ensurePeriod} disabled={createMut.isPending} style={{ padding: '6px 12px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            {createMut.isPending ? t('common.saving') : t('payrollAggregate.createPeriod', { defaultValue: 'Create period for this month' })}
          </button>
        )}
        <div><small>{t('payrollAggregate.totalHours')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.hours, 1)}</div></div>
        <div><small>{t('payrollAggregate.totalPay')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.net, 2)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('payrollAggregate.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        {activePeriod && isDraft && (
          <button onClick={() => finalizeMut.mutate(activePeriod.id)} disabled={finalizeMut.isPending} style={{ padding: '6px 12px' }}>
            {finalizeMut.isPending ? '…' : t('payrollAggregate.finalize', { defaultValue: 'Finalize' })}
          </button>
        )}
        {activePeriod && activePeriod.status === 2 && (
          <button onClick={() => exportMut.mutate(activePeriod.id)} disabled={exportMut.isPending} style={{ padding: '6px 12px' }}>
            {exportMut.isPending ? '…' : t('payrollAggregate.export', { defaultValue: 'Export' })}
          </button>
        )}
        <button onClick={() => exportToCsv(filteredLines, [
          { key: 'employeeNumber', label: 'Emp#' },
          { key: 'employeeName', label: t('payrollAggregate.fullName') as string },
          { key: 'regularHours', label: t('payrollAggregate.regularHours') as string, type: 'number' },
          { key: 'overtimeHours', label: t('payrollAggregate.overtimeHours') as string, type: 'number' },
          { key: 'absenceHours', label: 'Absence h', type: 'number' },
          { key: 'bonusAmount', label: 'Bonus', type: 'number' },
          { key: 'deductionAmount', label: 'Deduction', type: 'number' },
          { key: 'netAmount', label: t('payrollAggregate.totalPay') as string, type: 'number' },
          { key: 'currency', label: 'Currency' },
        ], `payroll-${month}`)}
          disabled={filteredLines.length === 0}
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
              <th>{t('payrollAggregate.regularHours')}</th>
              <th>{t('payrollAggregate.overtimeHours')}</th>
              <th>Absence h</th>
              <th>Bonus</th>
              <th>Deduction</th>
              <th>{t('payrollAggregate.totalPay')}</th>
              <th>Cur</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!isLoading && !activePeriod && <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('payrollAggregate.noPeriod', { defaultValue: 'No payroll period for this month yet — click "Create period".' })}</td></tr>}
            {!isLoading && activePeriod && filteredLines.length === 0 && <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!isLoading && activePeriod && filteredLines.map((l) => (
              <tr key={l.id}>
                <td><code>{l.employeeNumber ?? '-'}</code></td>
                <td>{l.employeeName ?? '-'}</td>
                <td><input type="number" step="0.1" min={0} value={l.regularHours} disabled={!isDraft} onChange={(e) => updateLine(l, { regularHours: Number(e.target.value) })} style={{ width: 80, padding: 4 }} /></td>
                <td style={{ color: l.overtimeHours > 0 ? '#ef6c00' : undefined }}>
                  <input type="number" step="0.1" min={0} value={l.overtimeHours} disabled={!isDraft} onChange={(e) => updateLine(l, { overtimeHours: Number(e.target.value) })} style={{ width: 80, padding: 4 }} />
                </td>
                <td><input type="number" step="0.1" min={0} value={l.absenceHours} disabled={!isDraft} onChange={(e) => updateLine(l, { absenceHours: Number(e.target.value) })} style={{ width: 80, padding: 4 }} /></td>
                <td><input type="number" step="0.01" min={0} value={l.bonusAmount} disabled={!isDraft} onChange={(e) => updateLine(l, { bonusAmount: Number(e.target.value) })} style={{ width: 80, padding: 4 }} /></td>
                <td><input type="number" step="0.01" min={0} value={l.deductionAmount} disabled={!isDraft} onChange={(e) => updateLine(l, { deductionAmount: Number(e.target.value) })} style={{ width: 80, padding: 4 }} /></td>
                <td><input type="number" step="0.01" min={0} value={l.netAmount} disabled={!isDraft} onChange={(e) => updateLine(l, { netAmount: Number(e.target.value) })} style={{ width: 100, padding: 4, fontWeight: 600 }} /></td>
                <td>
                  <input type="text" maxLength={3} value={l.currency} disabled={!isDraft} onChange={(e) => updateLine(l, { currency: e.target.value.toUpperCase() })} style={{ width: 50, padding: 4 }} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default PayrollAggregate;
