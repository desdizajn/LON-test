import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.8 — Rework view.
 *
 * Surfaces production orders with scrap > 0 — the scrap column is the lead
 * indicator for rework activity. Groups by item to show which products are
 * worst offenders, then lists the individual POs that contribute.
 */

type Order = {
  id: string;
  orderNumber: string;
  item?: { id?: string; code?: string; name?: string };
  itemId?: string;
  orderQuantity: number;
  producedQuantity: number;
  scrapQuantity: number;
  uoM?: { code?: string };
  status: number;
  plannedEndDate: string;
};

const Rework: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [minScrap, setMinScrap] = useState<number>(1);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await productionApi.getOrders();
        if (!cancelled) setOrders((resp.data as Order[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return orders
      .filter((o) => (o.scrapQuantity || 0) >= minScrap)
      .filter((o) => !q || `${o.orderNumber} ${o.item?.code ?? ''} ${o.item?.name ?? ''}`.toLowerCase().includes(q))
      .sort((a, b) => (b.scrapQuantity || 0) - (a.scrapQuantity || 0));
  }, [orders, search, minScrap]);

  const totals = useMemo(() => {
    const totalScrap = rows.reduce((s, o) => s + (o.scrapQuantity || 0), 0);
    const totalProduced = rows.reduce((s, o) => s + (o.producedQuantity || 0), 0);
    const scrapRate = totalProduced > 0 ? totalScrap / (totalScrap + totalProduced) : 0;
    return { totalScrap, totalProduced, scrapRate };
  }, [rows]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('rework.title')}</h1>
      <p style={{ color: '#666' }}>{t('rework.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('rework.minScrap')}: <input type="number" min={0} value={minScrap} onChange={(e) => setMinScrap(Math.max(0, Number(e.target.value)))} style={{ width: 70, padding: 4 }} /></label>
        <div><small>{t('rework.totalScrap')}</small><div style={{ fontWeight: 600, color: '#c62828' }}>{formatQuantity(totals.totalScrap, 0)}</div></div>
        <div><small>{t('rework.totalProduced')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.totalProduced, 0)}</div></div>
        <div><small>{t('rework.scrapRate')}</small><div style={{ fontWeight: 700, color: totals.scrapRate > 0.05 ? '#c62828' : '#2e7d32' }}>{(totals.scrapRate * 100).toFixed(2)}%</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('productionToday.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(rows, [
          { key: 'orderNumber', label: 'Order' },
          { key: 'itemCode', label: t('productionToday.item') as string, get: (o: Order) => `${o.item?.code ?? ''} ${o.item?.name ?? ''}`.trim() },
          { key: 'orderQuantity', label: t('productionToday.orderQty') as string, type: 'number' },
          { key: 'producedQuantity', label: t('productionToday.producedQty') as string, type: 'number' },
          { key: 'scrapQuantity', label: t('rework.scrap') as string, type: 'number' },
        ], 'rework')} disabled={rows.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('productionToday.orderNumber')}</th>
              <th>{t('productionToday.item')}</th>
              <th>{t('productionToday.orderQty')}</th>
              <th>{t('productionToday.producedQty')}</th>
              <th>{t('rework.scrap')}</th>
              <th>{t('rework.scrapPct')}</th>
              <th>{t('productionToday.plannedEnd')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#2e7d32' }}>{t('rework.empty')}</td></tr>}
            {!loading && rows.map((o) => {
              const totalOut = (o.producedQuantity || 0) + (o.scrapQuantity || 0);
              const rate = totalOut > 0 ? (o.scrapQuantity || 0) / totalOut : 0;
              return (
                <tr key={o.id}>
                  <td><code>{o.orderNumber}</code></td>
                  <td><strong>{o.item?.code}</strong> {o.item?.name}</td>
                  <td>{formatQuantity(o.orderQuantity, 0)}</td>
                  <td style={{ color: '#2e7d32' }}>{formatQuantity(o.producedQuantity, 0)}</td>
                  <td style={{ color: '#c62828', fontWeight: 600 }}>{formatQuantity(o.scrapQuantity, 0)} {o.uoM?.code}</td>
                  <td style={{ color: rate > 0.1 ? '#c62828' : rate > 0.03 ? '#ef6c00' : '#2e7d32' }}>{(rate * 100).toFixed(1)}%</td>
                  <td>{formatDate(o.plannedEndDate)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Rework;
