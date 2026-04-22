import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { managementApi, financeApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatPercent } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P13.9 — Client scorecard.
 *
 * Composite view per customer combining on-time delivery (P13.1), revenue
 * + outstanding (from invoicing), and order volume. Overall score mixes
 * on-time % (60%) + paid-ratio (40%) into a 0-100 composite.
 */

type OnTimeRollup = {
  customerId: string;
  customerName: string;
  totalShipments: number;
  onTime: number;
  late1To7: number;
  lateOver7: number;
  unknown: number;
  onTimePercentage: number;
};

type Invoice = { partnerId: string; partnerName: string; status: number; totalAmount: number };

type Row = {
  customerId: string;
  customerName: string;
  shipments: number;
  onTimePct: number;
  revenue: number;
  paid: number;
  outstanding: number;
  paidRatio: number;
  score: number;
};

const ClientScorecard: React.FC = () => {
  const { t } = useTranslation();
  const [onTime, setOnTime] = useState<OnTimeRollup[]>([]);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [otResp, invResp] = await Promise.all([
          managementApi.getOnTime(),
          financeApi.getInvoices({}),
        ]);
        if (cancelled) return;
        const ot = (otResp.data as any)?.data ?? otResp.data;
        setOnTime(ot?.byCustomer ?? []);
        const invEnv = invResp.data as { data?: Invoice[] };
        setInvoices(invEnv?.data ?? (invResp.data as Invoice[]) ?? []);
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
    onTime.forEach((ot) => {
      bucket.set(ot.customerId, {
        customerId: ot.customerId,
        customerName: ot.customerName,
        shipments: ot.totalShipments,
        onTimePct: ot.onTimePercentage,
        revenue: 0, paid: 0, outstanding: 0, paidRatio: 0, score: 0,
      });
    });
    invoices.forEach((inv) => {
      if (inv.status === 4) return;
      let row = bucket.get(inv.partnerId);
      if (!row) {
        row = { customerId: inv.partnerId, customerName: inv.partnerName, shipments: 0, onTimePct: 0, revenue: 0, paid: 0, outstanding: 0, paidRatio: 0, score: 0 };
        bucket.set(inv.partnerId, row);
      }
      row.revenue += inv.totalAmount;
      if (inv.status === 3) row.paid += inv.totalAmount;
      if (inv.status === 2) row.outstanding += inv.totalAmount;
    });
    bucket.forEach((row) => {
      row.paidRatio = row.revenue > 0 ? row.paid / row.revenue : 0;
      const otScore = row.onTimePct; // 0-100
      const payScore = row.paidRatio * 100;
      row.score = Math.round(otScore * 0.6 + payScore * 0.4);
    });
    return Array.from(bucket.values()).sort((a, b) => b.score - a.score);
  }, [onTime, invoices]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((r) => r.customerName.toLowerCase().includes(q));
  }, [rows, search]);

  const scoreColor = (s: number) => (s >= 80 ? '#2e7d32' : s >= 60 ? '#f9a825' : '#c62828');

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('clientScorecard.title')}</h1>
      <p style={{ color: '#666' }}>{t('clientScorecard.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('clientScorecard.searchPlaceholder') as string} style={{ padding: 6, minWidth: 260 }} />
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('clientScorecard.rowCount', { count: filtered.length })}</span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'customerName', label: t('clientScorecard.customer') as string },
          { key: 'shipments', label: t('clientScorecard.shipments') as string, type: 'number', decimals: 0 },
          { key: 'onTimePct', label: t('clientScorecard.onTimePct') as string, type: 'number' },
          { key: 'revenue', label: t('clientScorecard.revenue') as string, type: 'number' },
          { key: 'paid', label: t('clientScorecard.paid') as string, type: 'number' },
          { key: 'outstanding', label: t('clientScorecard.outstanding') as string, type: 'number' },
          { key: 'paidRatio', label: t('clientScorecard.paidRatio') as string, type: 'number' },
          { key: 'score', label: t('clientScorecard.score') as string, type: 'number', decimals: 0 },
        ], 'client-scorecard')} disabled={filtered.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('clientScorecard.score')}</th>
              <th>{t('clientScorecard.customer')}</th>
              <th>{t('clientScorecard.shipments')}</th>
              <th>{t('clientScorecard.onTimePct')}</th>
              <th>{t('clientScorecard.revenue')}</th>
              <th>{t('clientScorecard.paid')}</th>
              <th>{t('clientScorecard.outstanding')}</th>
              <th>{t('clientScorecard.paidRatio')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && filtered.map((r) => (
              <tr key={r.customerId}>
                <td><span style={{ padding: '4px 10px', borderRadius: 12, background: scoreColor(r.score), color: 'white', fontWeight: 600 }}>{r.score}</span></td>
                <td><strong>{r.customerName}</strong></td>
                <td>{r.shipments}</td>
                <td>{formatPercent(r.onTimePct / 100, 1)}</td>
                <td>{formatQuantity(r.revenue, 2)}</td>
                <td style={{ color: '#2e7d32' }}>{formatQuantity(r.paid, 2)}</td>
                <td style={{ color: '#e67e22' }}>{formatQuantity(r.outstanding, 2)}</td>
                <td>{formatPercent(r.paidRatio, 1)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ClientScorecard;
