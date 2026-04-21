import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity, formatPercent } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.1 — Денешен производен план.
 *
 * Pure client-side filter over `GET /Production/orders`: POs whose
 * [PlannedStartDate, PlannedEndDate] window contains today AND status is
 * active (not Cancelled/Closed). Progress = ProducedQuantity / OrderQuantity.
 */

type Order = {
  id: string;
  orderNumber: string;
  itemId: string;
  item?: { code?: string; name?: string };
  orderQuantity: number;
  producedQuantity: number;
  scrapQuantity: number;
  uoM?: { code?: string };
  status: number;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  customerPartnerId?: string | null;
  customerOrderNumber?: string | null;
  weekNumber?: number | null;
};

const STATUS_LABEL: Record<number, string> = {
  1: 'production.status.draft',
  2: 'production.status.released',
  3: 'production.status.inProgress',
  4: 'production.status.completed',
  5: 'production.status.closed',
  6: 'production.status.cancelled',
};

const isoDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).toISOString();

const ProductionToday: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');

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

  const today = useMemo(() => {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
  }, []);

  const todayOrders = useMemo(() => {
    const q = search.trim().toLowerCase();
    return orders.filter((o) => {
      if (o.status === 5 || o.status === 6) return false;
      const s = new Date(o.plannedStartDate);
      const e = new Date(o.plannedEndDate);
      s.setHours(0, 0, 0, 0);
      e.setHours(23, 59, 59, 999);
      if (!(s.getTime() <= today.getTime() && today.getTime() <= e.getTime())) return false;
      if (statusFilter && String(o.status) !== statusFilter) return false;
      if (q) {
        const hay = `${o.orderNumber} ${o.item?.code ?? ''} ${o.item?.name ?? ''} ${o.customerOrderNumber ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [orders, today, search, statusFilter]);

  const progress = (o: Order) =>
    o.orderQuantity > 0 ? o.producedQuantity / o.orderQuantity : 0;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('productionToday.title')}</h1>
      <p style={{ color: '#666' }}>{t('productionToday.subtitle', { date: formatDate(isoDay(today)) })}</p>

      <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('productionToday.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 260 }}
        />
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} style={{ padding: 6 }}>
          <option value="">{t('productionToday.statusAll')}</option>
          <option value="1">{t('production.status.draft')}</option>
          <option value="2">{t('production.status.released')}</option>
          <option value="3">{t('production.status.inProgress')}</option>
          <option value="4">{t('production.status.completed')}</option>
        </select>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('productionToday.rowCount', { count: todayOrders.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              todayOrders,
              [
                { key: 'orderNumber', label: t('productionToday.orderNumber') as string },
                { key: 'item', label: t('productionToday.item') as string, get: (o) => `${o.item?.code ?? ''} ${o.item?.name ?? ''}`.trim() },
                { key: 'customerOrderNumber', label: t('productionToday.customerOrderNumber') as string, get: (o) => o.customerOrderNumber ?? '-' },
                { key: 'orderQuantity', label: t('productionToday.orderQty') as string, type: 'number' },
                { key: 'producedQuantity', label: t('productionToday.producedQty') as string, type: 'number' },
                { key: 'progress', label: t('productionToday.progress') as string, get: (o) => progress(o), type: 'number' },
                { key: 'plannedStartDate', label: t('productionToday.plannedStart') as string, type: 'date' },
                { key: 'plannedEndDate', label: t('productionToday.plannedEnd') as string, type: 'date' },
              ],
              'production-today'
            )
          }
          disabled={todayOrders.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
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
              <th>{t('productionToday.orderNumber')}</th>
              <th>{t('productionToday.item')}</th>
              <th>{t('productionToday.customerOrderNumber')}</th>
              <th>{t('productionToday.orderQty')}</th>
              <th>{t('productionToday.producedQty')}</th>
              <th>{t('productionToday.progress')}</th>
              <th>{t('productionToday.status')}</th>
              <th>{t('productionToday.plannedEnd')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && todayOrders.length === 0 && (
              <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && todayOrders.map((o) => {
              const pct = progress(o);
              const uom = o.uoM?.code ?? '';
              return (
                <tr key={o.id}>
                  <td><Link to={`/production/orders?order=${o.id}`}><code>{o.orderNumber}</code></Link></td>
                  <td>
                    <div><strong>{o.item?.code ?? ''}</strong></div>
                    <div style={{ color: '#666', fontSize: 12 }}>{o.item?.name ?? ''}</div>
                  </td>
                  <td>{o.customerOrderNumber ?? '-'}</td>
                  <td>{formatQuantity(o.orderQuantity)} {uom}</td>
                  <td>{formatQuantity(o.producedQuantity)} {uom}</td>
                  <td>
                    <div style={{ background: '#eee', height: 8, borderRadius: 4, overflow: 'hidden', minWidth: 90 }}>
                      <div style={{
                        width: `${Math.min(100, pct * 100)}%`,
                        height: '100%',
                        background: pct >= 1 ? '#2e7d32' : pct >= 0.5 ? '#f9a825' : '#1976d2',
                      }} />
                    </div>
                    <small>{formatPercent(pct, 0)}</small>
                  </td>
                  <td>{t(STATUS_LABEL[o.status] ?? 'production.status.draft')}</td>
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

export default ProductionToday;
