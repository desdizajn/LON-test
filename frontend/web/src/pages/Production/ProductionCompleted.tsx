import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.3 — Завршени налози со period selector.
 *
 * Filters `GET /Production/orders?status=Completed` by ActualEndDate
 * (falls back to PlannedEndDate when ActualEndDate is null) within the
 * selected rolling window (7 / 30 / 90 / 365 days).
 */

type Order = {
  id: string;
  orderNumber: string;
  item?: { code?: string; name?: string };
  orderQuantity: number;
  producedQuantity: number;
  scrapQuantity: number;
  uoM?: { code?: string };
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  customerPartnerId?: string | null;
  customerOrderNumber?: string | null;
};

const PERIOD_OPTIONS = [
  { key: '7', days: 7 },
  { key: '30', days: 30 },
  { key: '90', days: 90 },
  { key: '365', days: 365 },
];

const ProductionCompleted: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [periodDays, setPeriodDays] = useState<number>(30);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await productionApi.getOrders({ status: 'Completed' });
        if (!cancelled) setOrders((resp.data as Order[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const cutoff = useMemo(() => {
    const d = new Date();
    d.setDate(d.getDate() - periodDays);
    d.setHours(0, 0, 0, 0);
    return d;
  }, [periodDays]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return orders
      .filter((o) => {
        const effective = o.actualEndDate ?? o.plannedEndDate;
        if (!effective) return false;
        if (new Date(effective).getTime() < cutoff.getTime()) return false;
        if (q) {
          const hay = `${o.orderNumber} ${o.item?.code ?? ''} ${o.item?.name ?? ''} ${o.customerOrderNumber ?? ''}`.toLowerCase();
          if (!hay.includes(q)) return false;
        }
        return true;
      })
      .sort((a, b) => {
        const ea = new Date(a.actualEndDate ?? a.plannedEndDate).getTime();
        const eb = new Date(b.actualEndDate ?? b.plannedEndDate).getTime();
        return eb - ea;
      });
  }, [orders, cutoff, search]);

  const totals = useMemo(() => {
    return filtered.reduce(
      (acc, o) => {
        acc.ordered += o.orderQuantity || 0;
        acc.produced += o.producedQuantity || 0;
        acc.scrap += o.scrapQuantity || 0;
        return acc;
      },
      { ordered: 0, produced: 0, scrap: 0 }
    );
  }, [filtered]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('productionCompleted.title')}</h1>
      <p style={{ color: '#666' }}>{t('productionCompleted.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('productionCompleted.period')}:</label>
        <select value={periodDays} onChange={(e) => setPeriodDays(Number(e.target.value))} style={{ padding: '4px 8px' }}>
          {PERIOD_OPTIONS.map((p) => (
            <option key={p.key} value={p.days}>{t(`productionCompleted.period${p.key}`)}</option>
          ))}
        </select>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('productionToday.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 240 }}
        />
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('productionCompleted.rowCount', { count: filtered.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'orderNumber', label: 'Order' },
                { key: 'item', label: 'Item', get: (o) => `${o.item?.code ?? ''} ${o.item?.name ?? ''}`.trim() },
                { key: 'customerOrderNumber', label: 'CustomerOrder', get: (o) => o.customerOrderNumber ?? '-' },
                { key: 'orderQuantity', label: 'OrderQty', type: 'number' },
                { key: 'producedQuantity', label: 'Produced', type: 'number' },
                { key: 'scrapQuantity', label: 'Scrap', type: 'number' },
                { key: 'plannedEndDate', label: 'PlannedEnd', type: 'date' },
                { key: 'actualEndDate', label: 'ActualEnd', type: 'date' },
              ],
              'production-completed'
            )
          }
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div style={{ display: 'flex', gap: 24, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4 }}>
        <div>
          <small>{t('productionCompleted.totalOrdered')}</small>
          <div style={{ fontWeight: 600 }}>{formatQuantity(totals.ordered)}</div>
        </div>
        <div>
          <small>{t('productionCompleted.totalProduced')}</small>
          <div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.produced)}</div>
        </div>
        <div>
          <small>{t('productionCompleted.totalScrap')}</small>
          <div style={{ fontWeight: 600, color: '#c62828' }}>{formatQuantity(totals.scrap)}</div>
        </div>
      </div>

      {error && (
        <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>
          {error}
        </div>
      )}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('productionCompleted.orderNumber')}</th>
              <th>{t('productionCompleted.item')}</th>
              <th>{t('productionCompleted.customerOrderNumber')}</th>
              <th>{t('productionCompleted.producedQty')}</th>
              <th>{t('productionCompleted.scrapQty')}</th>
              <th>{t('productionCompleted.actualEnd')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((o) => (
              <tr key={o.id}>
                <td><Link to={`/production/orders?order=${o.id}`}><code>{o.orderNumber}</code></Link></td>
                <td><strong>{o.item?.code}</strong> {o.item?.name}</td>
                <td>{o.customerOrderNumber ?? '-'}</td>
                <td>{formatQuantity(o.producedQuantity)} {o.uoM?.code ?? ''}</td>
                <td>{formatQuantity(o.scrapQuantity)} {o.uoM?.code ?? ''}</td>
                <td>{formatDate(o.actualEndDate ?? o.plannedEndDate)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ProductionCompleted;
