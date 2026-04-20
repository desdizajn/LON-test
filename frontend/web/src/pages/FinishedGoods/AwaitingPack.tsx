import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { finishedGoodsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P9.1 — Налози завршени што сè уште не се целосно испратени.
 *
 * Consumes `GET /FinishedGoods/awaiting-pack`. Backend agregira по PO:
 *   remaining = ProducedQuantity − SUM(ShipmentLine.Quantity WHERE batch ∈ PO's ProductionReceipt batches).
 * Само редови со `remaining > 0`.
 */

type Row = {
  productionOrderId: string;
  orderNumber: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  producedQuantity: number;
  shippedQuantity: number;
  remainingToPack: number;
  uoMId: string;
  uoMCode: string;
  actualEndDate: string | null;
  customerPartnerId: string | null;
  customerOrderNumber: string | null;
};

const AwaitingPack: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await finishedGoodsApi.getAwaitingPack();
      const envelope = resp.data as { isSuccess?: boolean; data?: Row[] };
      setRows(envelope?.data ?? (resp.data as Row[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const totals = rows.reduce(
    (acc, r) => {
      acc.produced += r.producedQuantity;
      acc.shipped += r.shippedQuantity;
      acc.remaining += r.remainingToPack;
      return acc;
    },
    { produced: 0, shipped: 0, remaining: 0 }
  );

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('awaitingPack.title')}</h1>
      <p style={{ color: '#666' }}>{t('awaitingPack.subtitle')}</p>

      <div style={{ display: 'flex', gap: 24, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center' }}>
        <div><small>{t('awaitingPack.produced')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.produced)}</div></div>
        <div><small>{t('awaitingPack.shipped')}</small><div style={{ fontWeight: 600, color: '#2e7d32' }}>{formatQuantity(totals.shipped)}</div></div>
        <div><small>{t('awaitingPack.remaining')}</small><div style={{ fontWeight: 600, color: '#ef6c00' }}>{formatQuantity(totals.remaining)}</div></div>
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('awaitingPack.rowCount', { count: rows.length })}</span>
        <button
          onClick={() => exportToCsv(
            rows,
            [
              { key: 'orderNumber', label: 'Order' },
              { key: 'item', label: 'Item', get: (r) => `${r.itemCode} ${r.itemName}`.trim() },
              { key: 'customerOrderNumber', label: 'CustomerOrder', get: (r) => r.customerOrderNumber ?? '' },
              { key: 'producedQuantity', label: 'Produced', type: 'number' },
              { key: 'shippedQuantity', label: 'Shipped', type: 'number' },
              { key: 'remainingToPack', label: 'Remaining', type: 'number' },
              { key: 'actualEndDate', label: 'ActualEnd', type: 'date' },
            ],
            'awaiting-pack'
          )}
          disabled={rows.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('awaitingPack.orderNumber')}</th>
              <th>{t('awaitingPack.item')}</th>
              <th>{t('awaitingPack.customerOrderNumber')}</th>
              <th>{t('awaitingPack.produced')}</th>
              <th>{t('awaitingPack.shipped')}</th>
              <th>{t('awaitingPack.remaining')}</th>
              <th>{t('awaitingPack.actualEnd')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.length === 0 && (
              <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#2e7d32' }}>{t('awaitingPack.none')}</td></tr>
            )}
            {!loading && rows.map((r) => (
              <tr key={r.productionOrderId}>
                <td><Link to={`/production/orders?order=${r.productionOrderId}`}><code>{r.orderNumber}</code></Link></td>
                <td><strong>{r.itemCode}</strong> {r.itemName}</td>
                <td>{r.customerOrderNumber ?? '-'}</td>
                <td>{formatQuantity(r.producedQuantity)} {r.uoMCode}</td>
                <td>{formatQuantity(r.shippedQuantity)} {r.uoMCode}</td>
                <td style={{ color: '#ef6c00', fontWeight: 600 }}>{formatQuantity(r.remainingToPack)} {r.uoMCode}</td>
                <td>{formatDate(r.actualEndDate)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default AwaitingPack;
