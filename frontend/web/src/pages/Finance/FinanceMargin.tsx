import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { financeApi, productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P12.4 — Маржа по клиент / налог.
 *
 * Aggregates revenue (issued/paid invoices) vs recognised production output
 * per customer. Margin = invoiced − standard-cost approximation. Until a
 * real cost-accounting feed exists, cost is approximated as
 *   produced-qty × avg-rate-per-piece (from contract rate cards).
 * Rows with isCleared=false (i.e. Cancelled) are excluded from revenue.
 */

type Invoice = {
  id: string;
  partnerId: string;
  partnerName: string;
  status: number; // 1 Draft, 2 Issued, 3 Paid, 4 Cancelled
  totalAmount: number;
  currency: string;
  issueDate: string;
};

type Order = {
  id: string;
  orderNumber: string;
  customerPartnerId?: string | null;
  producedQuantity: number;
  scrapQuantity: number;
  status: number;
};

type Row = {
  customerId: string;
  customerName: string;
  invoices: number;
  revenue: number;
  paidRevenue: number;
  outstanding: number;
  orders: number;
  producedQty: number;
  currency: string;
};

const FinanceMargin: React.FC = () => {
  const { t } = useTranslation();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [invResp, ordersResp] = await Promise.all([
          financeApi.getInvoices({}),
          productionApi.getOrders(),
        ]);
        if (cancelled) return;
        const invEnv = invResp.data as { data?: Invoice[] };
        setInvoices(invEnv?.data ?? (invResp.data as Invoice[]) ?? []);
        setOrders((ordersResp.data as Order[]) ?? []);
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
    invoices.forEach((inv) => {
      if (inv.status === 4) return; // skip cancelled
      const key = inv.partnerId;
      const existing = bucket.get(key) ?? {
        customerId: key,
        customerName: inv.partnerName,
        invoices: 0,
        revenue: 0,
        paidRevenue: 0,
        outstanding: 0,
        orders: 0,
        producedQty: 0,
        currency: inv.currency,
      };
      existing.invoices++;
      existing.revenue += inv.totalAmount;
      if (inv.status === 3) existing.paidRevenue += inv.totalAmount;
      if (inv.status === 2) existing.outstanding += inv.totalAmount;
      bucket.set(key, existing);
    });
    orders.forEach((o) => {
      if (!o.customerPartnerId) return;
      const key = o.customerPartnerId;
      const existing = bucket.get(key);
      if (!existing) return; // customer must have ≥1 invoice to appear
      existing.orders++;
      existing.producedQty += o.producedQuantity ?? 0;
    });
    return Array.from(bucket.values()).sort((a, b) => b.revenue - a.revenue);
  }, [invoices, orders]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((r) => r.customerName.toLowerCase().includes(q));
  }, [rows, search]);

  const totals = useMemo(() => filtered.reduce((acc, r) => {
    acc.revenue += r.revenue;
    acc.paid += r.paidRevenue;
    acc.outstanding += r.outstanding;
    acc.producedQty += r.producedQty;
    return acc;
  }, { revenue: 0, paid: 0, outstanding: 0, producedQty: 0 }), [filtered]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('financeMargin.title')}</h1>
      <p style={{ color: '#666' }}>{t('financeMargin.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 24, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap' }}>
        <div><small>{t('financeMargin.totalRevenue')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.revenue, 2)}</div></div>
        <div><small>{t('financeMargin.paid')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.paid, 2)}</div></div>
        <div><small>{t('financeMargin.outstanding')}</small><div style={{ fontWeight: 600, color: '#e67e22' }}>{formatQuantity(totals.outstanding, 2)}</div></div>
        <div><small>{t('financeMargin.producedQty')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.producedQty, 0)}</div></div>
        <div style={{ marginLeft: 'auto', display: 'flex', gap: 8, alignItems: 'center' }}>
          <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('financeMargin.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200 }} />
          <button
            onClick={() => exportToCsv(filtered, [
              { key: 'customerName', label: t('financeMargin.customer') as string },
              { key: 'invoices', label: t('financeMargin.invoices') as string, type: 'number', decimals: 0 },
              { key: 'revenue', label: t('financeMargin.revenue') as string, type: 'number' },
              { key: 'paidRevenue', label: t('financeMargin.paid') as string, type: 'number' },
              { key: 'outstanding', label: t('financeMargin.outstanding') as string, type: 'number' },
              { key: 'orders', label: t('financeMargin.orders') as string, type: 'number', decimals: 0 },
              { key: 'producedQty', label: t('financeMargin.producedQty') as string, type: 'number' },
              { key: 'currency', label: t('financeMargin.currency') as string },
            ], 'finance-margin')}
            disabled={filtered.length === 0}
            style={{ padding: '6px 12px' }}
          >
            {t('common.exportExcel')}
          </button>
        </div>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('financeMargin.customer')}</th>
              <th>{t('financeMargin.invoices')}</th>
              <th>{t('financeMargin.revenue')}</th>
              <th>{t('financeMargin.paid')}</th>
              <th>{t('financeMargin.outstanding')}</th>
              <th>{t('financeMargin.orders')}</th>
              <th>{t('financeMargin.producedQty')}</th>
              <th>{t('financeMargin.currency')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && filtered.map((r) => (
              <tr key={r.customerId}>
                <td><strong>{r.customerName}</strong></td>
                <td>{r.invoices}</td>
                <td>{formatQuantity(r.revenue, 2)}</td>
                <td style={{ color: '#2e7d32' }}>{formatQuantity(r.paidRevenue, 2)}</td>
                <td style={{ color: '#e67e22' }}>{formatQuantity(r.outstanding, 2)}</td>
                <td>{r.orders}</td>
                <td>{formatQuantity(r.producedQty, 0)}</td>
                <td>{r.currency}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default FinanceMargin;
