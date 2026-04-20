import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { finishedGoodsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P9.6 — Состојба на паковни материјали.
 *
 * `GET /FinishedGoods/packaging-stock`: за секој Item.Type=Packaging
 * (активен во catalog), вкупна количина од InventoryBalance со OK quality
 * и process state ≠ Exported/Waste, + број локации каде се наоѓа.
 */

type Row = {
  itemId: string;
  itemCode: string;
  itemName: string;
  uoMId: string;
  uoMCode: string;
  totalOnHand: number;
  locationCount: number;
};

const PackagingStock: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<string>('');
  const [zeroOnly, setZeroOnly] = useState<boolean>(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const resp = await finishedGoodsApi.getPackagingStock();
      const envelope = resp.data as { isSuccess?: boolean; data?: Row[] };
      setRows(envelope?.data ?? (resp.data as Row[]) ?? []);
    } catch (err) {
      setError(translateError(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    return rows.filter((r) => {
      if (zeroOnly && r.totalOnHand > 0) return false;
      if (!q) return true;
      return r.itemCode.toLowerCase().includes(q) || r.itemName.toLowerCase().includes(q);
    });
  }, [rows, filter, zeroOnly]);

  const totals = rows.reduce(
    (acc, r) => {
      acc.total += r.totalOnHand;
      if (r.totalOnHand === 0) acc.zeroItems += 1;
      return acc;
    },
    { total: 0, zeroItems: 0 }
  );

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('packagingStock.title')}</h1>
      <p style={{ color: '#666' }}>{t('packagingStock.subtitle')}</p>

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <div><small>{t('packagingStock.items')}</small><div style={{ fontWeight: 600 }}>{rows.length}</div></div>
        <div><small>{t('packagingStock.zeroItems')}</small><div style={{ fontWeight: 600, color: totals.zeroItems > 0 ? '#c62828' : '#2e7d32' }}>{totals.zeroItems}</div></div>
        <div><small>{t('packagingStock.totalOnHand')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.total)}</div></div>
        <input
          placeholder={t('packagingStock.searchPlaceholder') as string}
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{ padding: 6, marginLeft: 'auto', minWidth: 200 }}
        />
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <input type="checkbox" checked={zeroOnly} onChange={(e) => setZeroOnly(e.target.checked)} />
          {t('packagingStock.zeroOnly')}
        </label>
        <button
          onClick={() => exportToCsv(
            rows,
            [
              { key: 'itemCode', label: 'Code' },
              { key: 'itemName', label: 'Name' },
              { key: 'uoMCode', label: 'UoM' },
              { key: 'totalOnHand', label: 'OnHand', type: 'number' },
              { key: 'locationCount', label: 'Locations', type: 'number' },
            ],
            'packaging-stock'
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
              <th>{t('packagingStock.code')}</th>
              <th>{t('packagingStock.name')}</th>
              <th>{t('packagingStock.uom')}</th>
              <th>{t('packagingStock.onHand')}</th>
              <th>{t('packagingStock.locations')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={5} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={5} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((r) => (
              <tr key={r.itemId} style={r.totalOnHand === 0 ? { background: '#ffebee' } : undefined}>
                <td><code>{r.itemCode}</code></td>
                <td>{r.itemName}</td>
                <td>{r.uoMCode}</td>
                <td style={{ fontWeight: 600, color: r.totalOnHand === 0 ? '#c62828' : undefined }}>{formatQuantity(r.totalOnHand)}</td>
                <td>{r.locationCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default PackagingStock;
