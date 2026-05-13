import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi, wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity, formatPercent } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.2 — Работа во тек (Work In Progress).
 *
 * Two sections stacked on one page:
 *   1. POs со status InProgress (eager pulled via `GET /Production/orders?status=InProgress`)
 *   2. InventoryBalance rows со LonProcessState=InProduction (client-filter of `/WMS/inventory`)
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
};

type Balance = {
  id: string;
  itemId: string;
  item?: { code?: string; name?: string };
  locationId: string;
  location?: { code?: string; name?: string };
  quantity: number;
  uoM?: { code?: string };
  batchNumber?: string | null;
  mrn?: string | null;
  lonProcessState?: number | null;
};

// Matches LonProcessState enum: InProduction = 6
const LON_IN_PRODUCTION = 6;

const ProductionWip: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [balances, setBalances] = useState<Balance[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [orderSearch, setOrderSearch] = useState('');
  const [stockSearch, setStockSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [ordersResp, invResp] = await Promise.all([
          productionApi.getOrders({ status: 'InProgress' }),
          wmsApi.getInventory(),
        ]);
        if (cancelled) return;
        setOrders((ordersResp.data as Order[]) ?? []);
        setBalances((invResp.data as Balance[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const wipBalances = useMemo(() => {
    const q = stockSearch.trim().toLowerCase();
    return balances.filter((b) => {
      if (b.lonProcessState !== LON_IN_PRODUCTION || b.quantity <= 0) return false;
      if (!q) return true;
      const hay = `${b.item?.code ?? ''} ${b.item?.name ?? ''} ${b.location?.code ?? ''} ${b.batchNumber ?? ''} ${b.mrn ?? ''}`.toLowerCase();
      return hay.includes(q);
    });
  }, [balances, stockSearch]);

  const filteredOrders = useMemo(() => {
    const q = orderSearch.trim().toLowerCase();
    if (!q) return orders;
    return orders.filter((o) => {
      const hay = `${o.orderNumber} ${o.item?.code ?? ''} ${o.item?.name ?? ''}`.toLowerCase();
      return hay.includes(q);
    });
  }, [orders, orderSearch]);

  const totalWipQty = useMemo(
    () => wipBalances.reduce((acc, b) => acc + (b.quantity || 0), 0),
    [wipBalances]
  );

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('productionWip.title')}</h1>
      <p style={{ color: '#666' }}>{t('productionWip.subtitle')}</p>

      {error && (
        <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>
          {error}
        </div>
      )}

      <h2 style={{ marginTop: 24 }}>{t('productionWip.ordersSection', { count: filteredOrders.length })}</h2>
      <div style={{ display: 'flex', gap: 8, marginBottom: 8, alignItems: 'center' }}>
        <input
          type="text"
          value={orderSearch}
          onChange={(e) => setOrderSearch(e.target.value)}
          placeholder={t('productionToday.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 240 }}
        />
        <button
          onClick={() =>
            exportToCsv(
              filteredOrders,
              [
                { key: 'orderNumber', label: 'Order' },
                { key: 'item', label: 'Item', get: (o) => `${o.item?.code ?? ''} ${o.item?.name ?? ''}`.trim() },
                { key: 'orderQuantity', label: 'OrderQty', type: 'number' },
                { key: 'producedQuantity', label: 'Produced', type: 'number' },
                { key: 'plannedEndDate', label: 'PlannedEnd', type: 'date' },
              ],
              'production-wip-orders'
            )
          }
          disabled={orders.length === 0}
          style={{ padding: '4px 10px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('productionWip.orderNumber')}</th>
              <th>{t('productionWip.item')}</th>
              <th>{t('productionWip.orderQty')}</th>
              <th>{t('productionWip.producedQty')}</th>
              <th>{t('productionWip.progress')}</th>
              <th>{t('productionWip.plannedEnd')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filteredOrders.length === 0 && (
              <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{orderSearch ? t('inventory.filters.noResults') : t('common.noData')}</td></tr>
            )}
            {!loading && filteredOrders.map((o) => {
              const pct = o.orderQuantity > 0 ? o.producedQuantity / o.orderQuantity : 0;
              const uom = o.uoM?.code ?? '';
              return (
                <tr key={o.id}>
                  <td><Link to={`/production/orders?order=${o.id}`}><code>{o.orderNumber}</code></Link></td>
                  <td><strong>{o.item?.code}</strong> {o.item?.name}</td>
                  <td>{formatQuantity(o.orderQuantity)} {uom}</td>
                  <td>{formatQuantity(o.producedQuantity)} {uom}</td>
                  <td>{formatPercent(pct, 0)}</td>
                  <td>{formatDate(o.plannedEndDate)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <h2 style={{ marginTop: 32 }}>
        {t('productionWip.wipStockSection', { count: wipBalances.length })}
      </h2>
      <p style={{ color: '#666', marginTop: -8 }}>
        {t('productionWip.wipStockHint', { total: formatQuantity(totalWipQty) })}
      </p>
      <div style={{ display: 'flex', gap: 8, marginBottom: 8, alignItems: 'center' }}>
        <input
          type="text"
          value={stockSearch}
          onChange={(e) => setStockSearch(e.target.value)}
          placeholder={t('productionWip.stockSearchPlaceholder') as string}
          style={{ padding: 6, minWidth: 240 }}
        />
        <button
          onClick={() =>
            exportToCsv(
              wipBalances,
              [
                { key: 'item', label: 'Item', get: (b) => `${b.item?.code ?? ''} ${b.item?.name ?? ''}`.trim() },
                { key: 'location', label: 'Location', get: (b) => b.location?.code ?? '-' },
                { key: 'batchNumber', label: 'Batch', get: (b) => b.batchNumber ?? '-' },
                { key: 'mrn', label: 'MRN', get: (b) => b.mrn ?? '-' },
                { key: 'quantity', label: 'Qty', type: 'number' },
              ],
              'production-wip-stock'
            )
          }
          disabled={wipBalances.length === 0}
          style={{ padding: '4px 10px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>
      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('productionWip.stockItem')}</th>
              <th>{t('productionWip.stockLocation')}</th>
              <th>{t('productionWip.stockBatch')}</th>
              <th>MRN</th>
              <th>{t('productionWip.stockQty')}</th>
            </tr>
          </thead>
          <tbody>
            {!loading && wipBalances.length === 0 && (
              <tr><td colSpan={5} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {wipBalances.map((b) => (
              <tr key={b.id}>
                <td><strong>{b.item?.code}</strong> {b.item?.name}</td>
                <td><code>{b.location?.code ?? '-'}</code></td>
                <td><code>{b.batchNumber ?? '-'}</code></td>
                <td><code>{b.mrn ?? '-'}</code></td>
                <td>{formatQuantity(b.quantity)} {b.uoM?.code ?? ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ProductionWip;
