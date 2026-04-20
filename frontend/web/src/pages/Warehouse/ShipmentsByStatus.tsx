import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * Shared component for shipment status-slice pages.
 *   P7.4 /warehouse/ready-to-ship → status=Packed (4)
 *   P7.6 /finished/shipped → status=Shipped (5)
 */

type Shipment = {
  id: string;
  shipmentNumber: string;
  shipmentDate: string;
  customerId?: string | null;
  customerName?: string | null;
  carrierId?: string | null;
  carrierName?: string | null;
  status: number;
  trackingNumber?: string | null;
  salesOrderNumber?: string | null;
  lines?: Array<{ id: string; itemId: string; quantity: number; batchNumber?: string | null; mrn?: string | null }>;
};

interface Props {
  title: string;
  subtitle: string;
  filterStatus: number;
}

const ShipmentsByStatus: React.FC<Props> = ({ title, subtitle, filterStatus }) => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Shipment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await wmsApi.getShipments(1, 500);
        if (!cancelled) {
          const all = (resp.data as Shipment[]) ?? [];
          setRows(all.filter((s) => s.status === filterStatus));
        }
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [filterStatus]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (s) =>
        s.shipmentNumber.toLowerCase().includes(q) ||
        (s.customerName ?? '').toLowerCase().includes(q) ||
        (s.trackingNumber ?? '').toLowerCase().includes(q) ||
        (s.salesOrderNumber ?? '').toLowerCase().includes(q)
    );
  }, [rows, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{title}</h1>
      <p style={{ color: '#666' }}>{subtitle}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('shipmentsByStatus.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 260 }}
        />
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('shipmentsByStatus.rowCount', { count: filtered.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'shipmentNumber', label: t('shipmentsByStatus.number') as string },
                { key: 'shipmentDate', label: t('shipmentsByStatus.date') as string, type: 'date' },
                { key: 'customerName', label: t('shipmentsByStatus.customer') as string },
                { key: 'carrierName', label: t('shipmentsByStatus.carrier') as string },
                { key: 'trackingNumber', label: t('shipmentsByStatus.tracking') as string },
                { key: 'salesOrderNumber', label: 'SO #' },
                { key: 'lines', label: t('shipmentsByStatus.linesCount') as string, get: (r) => r.lines?.length ?? 0, type: 'number', decimals: 0 },
                { key: 'lines', label: t('shipmentsByStatus.totalQty') as string, get: (r) => r.lines?.reduce((a, l) => a + l.quantity, 0) ?? 0, type: 'number' },
              ],
              `shipments-status-${filterStatus}`
            )
          }
          disabled={filtered.length === 0}
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
              <th>{t('shipmentsByStatus.number')}</th>
              <th>{t('shipmentsByStatus.date')}</th>
              <th>{t('shipmentsByStatus.customer')}</th>
              <th>{t('shipmentsByStatus.carrier')}</th>
              <th>{t('shipmentsByStatus.tracking')}</th>
              <th>SO #</th>
              <th>{t('shipmentsByStatus.linesCount')}</th>
              <th>{t('shipmentsByStatus.totalQty')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((s) => (
              <tr key={s.id}>
                <td><strong>{s.shipmentNumber}</strong></td>
                <td>{formatDate(s.shipmentDate)}</td>
                <td>{s.customerName ?? '-'}</td>
                <td>{s.carrierName ?? '-'}</td>
                <td>{s.trackingNumber ?? '-'}</td>
                <td>{s.salesOrderNumber ?? '-'}</td>
                <td>{s.lines?.length ?? 0}</td>
                <td>{formatQuantity(s.lines?.reduce((a, l) => a + l.quantity, 0) ?? 0)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ShipmentsByStatus;
