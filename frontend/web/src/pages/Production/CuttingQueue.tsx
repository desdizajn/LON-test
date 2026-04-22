import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.6 — Cutting queue.
 *
 * Filters production orders to those in Draft/Released/InProgress that are
 * still pre-production (producedQuantity == 0). Until a real
 * ProductionOrderOperation.Status enum + OperationType tag lands, this is
 * a proxy that surfaces what needs cutting attention first.
 */

type Order = {
  id: string;
  orderNumber: string;
  item?: { code?: string; name?: string };
  orderQuantity: number;
  producedQuantity: number;
  uoM?: { code?: string };
  status: number;
  plannedStartDate: string;
  plannedEndDate: string;
  customerOrderNumber?: string | null;
};

type Props = { operationType: 'cutting' | 'sewing' };

const QUEUE_STATUSES = new Set([1, 2, 3]); // Draft / Released / InProgress

const OperationQueue: React.FC<Props> = ({ operationType }) => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

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
      .filter((o) => {
        if (!QUEUE_STATUSES.has(o.status)) return false;
        // Cutting: producedQuantity == 0 (pre-production).
        // Sewing: producedQuantity > 0 but < orderQuantity (in-progress).
        if (operationType === 'cutting' && o.producedQuantity > 0) return false;
        if (operationType === 'sewing' && (o.producedQuantity === 0 || o.producedQuantity >= o.orderQuantity)) return false;
        if (q) {
          const hay = `${o.orderNumber} ${o.item?.code ?? ''} ${o.item?.name ?? ''} ${o.customerOrderNumber ?? ''}`.toLowerCase();
          if (!hay.includes(q)) return false;
        }
        return true;
      })
      .sort((a, b) => new Date(a.plannedEndDate).getTime() - new Date(b.plannedEndDate).getTime());
  }, [orders, operationType, search]);

  const titleKey = operationType === 'cutting' ? 'cuttingQueue.title' : 'sewingQueue.title';
  const subtitleKey = operationType === 'cutting' ? 'cuttingQueue.subtitle' : 'sewingQueue.subtitle';

  return (
    <div style={{ padding: 16 }}>
      <h1>{t(titleKey)}</h1>
      <p style={{ color: '#666' }}>{t(subtitleKey)}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('productionToday.searchPlaceholder') as string} style={{ padding: 6, minWidth: 260 }} />
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('cuttingQueue.rowCount', { count: rows.length })}</span>
        <button onClick={() => exportToCsv(rows, [
          { key: 'orderNumber', label: t('productionToday.orderNumber') as string },
          { key: 'itemCode', label: t('productionToday.item') as string, get: (o: Order) => `${o.item?.code ?? ''} ${o.item?.name ?? ''}`.trim() },
          { key: 'orderQuantity', label: t('productionToday.orderQty') as string, type: 'number' },
          { key: 'producedQuantity', label: t('productionToday.producedQty') as string, type: 'number' },
          { key: 'plannedEndDate', label: t('productionToday.plannedEnd') as string, type: 'date' },
          { key: 'customerOrderNumber', label: t('productionToday.customerOrderNumber') as string, get: (o: Order) => o.customerOrderNumber ?? '' },
        ], operationType === 'cutting' ? 'cutting-queue' : 'sewing-queue')} disabled={rows.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('productionToday.orderNumber')}</th>
              <th>{t('productionToday.item')}</th>
              <th>{t('productionToday.customerOrderNumber')}</th>
              <th>{t('productionToday.orderQty')}</th>
              <th>{t('productionToday.producedQty')}</th>
              <th>{t('productionToday.plannedEnd')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('cuttingQueue.empty')}</td></tr>}
            {!loading && rows.map((o) => {
              const uom = o.uoM?.code ?? '';
              const pct = o.orderQuantity > 0 ? o.producedQuantity / o.orderQuantity : 0;
              return (
                <tr key={o.id}>
                  <td><Link to={`/production/orders?order=${o.id}`}><code>{o.orderNumber}</code></Link></td>
                  <td><strong>{o.item?.code}</strong> {o.item?.name}</td>
                  <td>{o.customerOrderNumber ?? '-'}</td>
                  <td>{formatQuantity(o.orderQuantity)} {uom}</td>
                  <td>{formatQuantity(o.producedQuantity)} {uom} <small style={{ color: '#888' }}>({Math.round(pct * 100)}%)</small></td>
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

export default OperationQueue;
