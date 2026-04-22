import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi, productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P13.7 — Trends.
 *
 * Time series of 3 key metrics per month: invoiced amount, orders created,
 * production output. Gives management a rolling 12-month view of business
 * health.
 */

type Invoice = { issueDate: string; status: number; totalAmount: number };
type Order = { plannedStartDate: string; producedQuantity: number; scrapQuantity: number };

type Row = {
  yearMonth: string;
  invoices: number;
  revenue: number;
  orders: number;
  producedQty: number;
};

const Trends: React.FC = () => {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [monthsBack, setMonthsBack] = useState<number>(12);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [iResp, oResp] = await Promise.all([
          financeApi.getInvoices({}),
          productionApi.getOrders(),
        ]);
        if (cancelled) return;
        const iEnv = iResp.data as { data?: Invoice[] };
        setInvoices(iEnv?.data ?? (iResp.data as Invoice[]) ?? []);
        setOrders((oResp.data as Order[]) ?? []);
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
      bucket.set(ym, { yearMonth: ym, invoices: 0, revenue: 0, orders: 0, producedQty: 0 });
    }
    invoices.forEach((inv) => {
      if (inv.status === 4) return;
      const d = new Date(inv.issueDate);
      if (isNaN(d.getTime())) return;
      const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      const row = bucket.get(ym);
      if (!row) return;
      row.invoices++;
      row.revenue += inv.totalAmount;
    });
    orders.forEach((o) => {
      const d = o.plannedStartDate ? new Date(o.plannedStartDate) : null;
      if (!d || isNaN(d.getTime())) return;
      const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      const row = bucket.get(ym);
      if (!row) return;
      row.orders++;
      row.producedQty += o.producedQuantity ?? 0;
    });
    return Array.from(bucket.values()).sort((a, b) => a.yearMonth.localeCompare(b.yearMonth));
  }, [invoices, orders, monthsBack]);

  const max = useMemo(() => ({
    revenue: Math.max(1, ...rows.map((r) => r.revenue)),
    orders: Math.max(1, ...rows.map((r) => r.orders)),
    producedQty: Math.max(1, ...rows.map((r) => r.producedQty)),
  }), [rows]);

  const Bar: React.FC<{ pct: number; color: string }> = ({ pct, color }) => (
    <div style={{ background: '#eee', height: 8, borderRadius: 4, overflow: 'hidden', width: 100 }}>
      <div style={{ width: `${Math.round(pct * 100)}%`, height: '100%', background: color }} />
    </div>
  );

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('trends.title')}</h1>
      <p style={{ color: '#666' }}>{t('trends.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center' }}>
        <label>{t('trends.monthsBack')}: <input type="number" min={3} max={36} value={monthsBack} onChange={(e) => setMonthsBack(Math.max(3, Math.min(36, Number(e.target.value))))} style={{ width: 70, padding: 4 }} /></label>
        <button onClick={() => exportToCsv(rows, [
          { key: 'yearMonth', label: t('trends.month') as string },
          { key: 'invoices', label: t('trends.invoices') as string, type: 'number', decimals: 0 },
          { key: 'revenue', label: t('trends.revenue') as string, type: 'number' },
          { key: 'orders', label: t('trends.orders') as string, type: 'number', decimals: 0 },
          { key: 'producedQty', label: t('trends.producedQty') as string, type: 'number' },
        ], 'trends')} disabled={rows.length === 0} style={{ padding: '6px 12px', marginLeft: 'auto' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('trends.month')}</th>
              <th>{t('trends.invoices')}</th>
              <th>{t('trends.revenue')}</th>
              <th></th>
              <th>{t('trends.orders')}</th>
              <th>{t('trends.producedQty')}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.map((r) => (
              <tr key={r.yearMonth}>
                <td><strong>{r.yearMonth}</strong></td>
                <td>{r.invoices}</td>
                <td>{formatQuantity(r.revenue, 2)}</td>
                <td><Bar pct={r.revenue / max.revenue} color="#1976d2" /></td>
                <td>{r.orders}</td>
                <td>{formatQuantity(r.producedQty, 0)}</td>
                <td><Bar pct={r.producedQty / max.producedQty} color="#2e7d32" /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Trends;
