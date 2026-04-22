import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P12.7 — P&L preview.
 *
 * Month-by-month rollup of invoice revenue. Until supplier invoices + payroll
 * are wired, "costs" is a placeholder — the preview surfaces gross revenue,
 * monthly trend and YTD totals. When P12.5/P12.6 backends land, the cost
 * column gets populated in place.
 */

type Invoice = {
  id: string;
  status: number;
  totalAmount: number;
  currency: string;
  issueDate: string;
};

type Row = {
  yearMonth: string;
  issued: number;
  paid: number;
  cancelled: number;
  revenue: number;
  estimatedCost: number;
  netMargin: number;
};

const PnLPreview: React.FC = () => {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [monthsBack, setMonthsBack] = useState<number>(12);
  const [costPercent, setCostPercent] = useState<number>(65);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await financeApi.getInvoices({});
        const env = resp.data as { data?: Invoice[] };
        if (!cancelled) setInvoices(env?.data ?? (resp.data as Invoice[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const rows = useMemo<Row[]>(() => {
    const bucket = new Map<string, Row>();
    const now = new Date();
    for (let i = 0; i < monthsBack; i++) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      bucket.set(ym, { yearMonth: ym, issued: 0, paid: 0, cancelled: 0, revenue: 0, estimatedCost: 0, netMargin: 0 });
    }
    invoices.forEach((inv) => {
      const d = new Date(inv.issueDate);
      if (isNaN(d.getTime())) return;
      const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      const row = bucket.get(ym);
      if (!row) return;
      if (inv.status === 2) row.issued++;
      if (inv.status === 3) row.paid++;
      if (inv.status === 4) row.cancelled++;
      if (inv.status !== 4) row.revenue += inv.totalAmount;
    });
    bucket.forEach((row) => {
      row.estimatedCost = row.revenue * (costPercent / 100);
      row.netMargin = row.revenue - row.estimatedCost;
    });
    return Array.from(bucket.values()).sort((a, b) => b.yearMonth.localeCompare(a.yearMonth));
  }, [invoices, monthsBack, costPercent]);

  const totals = useMemo(() => rows.reduce((acc, r) => {
    acc.revenue += r.revenue;
    acc.estimatedCost += r.estimatedCost;
    acc.netMargin += r.netMargin;
    acc.issued += r.issued;
    acc.paid += r.paid;
    return acc;
  }, { revenue: 0, estimatedCost: 0, netMargin: 0, issued: 0, paid: 0 }), [rows]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('pnl.title')}</h1>
      <p style={{ color: '#666' }}>{t('pnl.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 24, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap', alignItems: 'center' }}>
        <label>{t('pnl.monthsBack')}: <input type="number" min={1} max={36} value={monthsBack} onChange={(e) => setMonthsBack(Math.max(1, Math.min(36, Number(e.target.value))))} style={{ width: 70, padding: 4 }} /></label>
        <label>{t('pnl.costPercent')}: <input type="number" min={0} max={100} value={costPercent} onChange={(e) => setCostPercent(Math.max(0, Math.min(100, Number(e.target.value))))} style={{ width: 70, padding: 4 }} />%</label>
        <div><small>{t('pnl.ytdRevenue')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.revenue, 2)}</div></div>
        <div><small>{t('pnl.ytdCost')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{formatQuantity(totals.estimatedCost, 2)}</div></div>
        <div><small>{t('pnl.ytdNet')}</small><div style={{ fontWeight: 600, color: totals.netMargin >= 0 ? '#2e7d32' : '#c62828' }}>{formatQuantity(totals.netMargin, 2)}</div></div>
        <button
          onClick={() => exportToCsv(rows, [
            { key: 'yearMonth', label: t('pnl.month') as string },
            { key: 'issued', label: t('pnl.issued') as string, type: 'number', decimals: 0 },
            { key: 'paid', label: t('pnl.paid') as string, type: 'number', decimals: 0 },
            { key: 'revenue', label: t('pnl.revenue') as string, type: 'number' },
            { key: 'estimatedCost', label: t('pnl.estimatedCost') as string, type: 'number' },
            { key: 'netMargin', label: t('pnl.netMargin') as string, type: 'number' },
          ], 'pnl-preview')}
          disabled={rows.length === 0}
          style={{ padding: '6px 12px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('pnl.month')}</th>
              <th>{t('pnl.issued')}</th>
              <th>{t('pnl.paid')}</th>
              <th>{t('pnl.revenue')}</th>
              <th>{t('pnl.estimatedCost')}</th>
              <th>{t('pnl.netMargin')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.map((r) => (
              <tr key={r.yearMonth}>
                <td><strong>{r.yearMonth}</strong></td>
                <td>{r.issued}</td>
                <td style={{ color: '#2e7d32' }}>{r.paid}</td>
                <td>{formatQuantity(r.revenue, 2)}</td>
                <td style={{ color: '#c62828' }}>{formatQuantity(r.estimatedCost, 2)}</td>
                <td style={{ color: r.netMargin >= 0 ? '#2e7d32' : '#c62828', fontWeight: 600 }}>{formatQuantity(r.netMargin, 2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default PnLPreview;
