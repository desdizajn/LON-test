import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import DetailDrawer from '../../components/common/DetailDrawer';

/**
 * Shared component for shipment status-slice pages.
 *   P7.4 /warehouse/ready-to-ship → status=Packed (4)
 *   P7.6 /finished/shipped → status=Shipped (5)
 *
 * Row click opens a drawer with header fields + lines (item, batch, MRN,
 * quantity). Uses cached row data — no extra fetch.
 */

type Line = {
  id: string;
  lineNumber?: number;
  itemId: string;
  itemCode?: string;
  itemName?: string;
  quantity: number;
  uoMCode?: string | null;
  batchNumber?: string | null;
  mrn?: string | null;
};

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
  notes?: string | null;
  lines?: Line[];
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
  const [detail, setDetail] = useState<Shipment | null>(null);

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

  const detailTotal = useMemo(() => detail?.lines?.reduce((a, l) => a + l.quantity, 0) ?? 0, [detail]);

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
              <tr
                key={s.id}
                style={{ cursor: 'pointer' }}
                onClick={() => setDetail(s)}
                title={t('shipmentsByStatus.clickToOpen') as string}
              >
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

      <DetailDrawer
        open={!!detail}
        onClose={() => setDetail(null)}
        title={detail?.shipmentNumber ?? ''}
        subtitle={detail?.customerName ?? undefined}
        width={680}
      >
        {detail && (
          <>
            <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 18 }}>
              <Field label={t('shipmentsByStatus.date') as string} value={formatDate(detail.shipmentDate)} />
              <Field label={t('shipmentsByStatus.customer') as string} value={detail.customerName ?? '-'} />
              <Field label={t('shipmentsByStatus.carrier') as string} value={detail.carrierName ?? '-'} />
              <Field label={t('shipmentsByStatus.tracking') as string} value={detail.trackingNumber ?? '-'} />
              <Field label="SO #" value={detail.salesOrderNumber ?? '-'} />
              <Field label={t('shipmentsByStatus.totalQty') as string} value={formatQuantity(detailTotal)} />
            </section>
            <h3 style={{ fontSize: 14, margin: '0 0 8px' }}>
              {t('shipmentsByStatus.linesTitle', { count: detail.lines?.length ?? 0 })}
            </h3>
            {(detail.lines?.length ?? 0) === 0 && (
              <div style={{ color: '#888', fontSize: 13 }}>{t('declarationsByType.noLines')}</div>
            )}
            {detail.lines && detail.lines.length > 0 && (
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', fontSize: 12 }}>
                  <thead>
                    <tr>
                      <th style={{ textAlign: 'left' }}>#</th>
                      <th style={{ textAlign: 'left' }}>{t('declarationsByType.line.item')}</th>
                      <th style={{ textAlign: 'left' }}>{t('declarationsByType.line.batch')}</th>
                      <th style={{ textAlign: 'left' }}>MRN</th>
                      <th style={{ textAlign: 'right' }}>{t('declarationsByType.line.qty')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.lines.map((l, i) => (
                      <tr key={l.id ?? i}>
                        <td>{l.lineNumber ?? i + 1}</td>
                        <td>{l.itemCode ?? l.itemId}{l.itemName ? ' · ' + l.itemName : ''}</td>
                        <td>{l.batchNumber ?? '-'}</td>
                        <td>{l.mrn ?? '-'}</td>
                        <td style={{ textAlign: 'right' }}>{formatQuantity(l.quantity)} {l.uoMCode ?? ''}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {detail.notes && (
              <section style={{ marginTop: 18 }}>
                <h3 style={{ fontSize: 14, margin: '0 0 6px' }}>{t('declarationsByType.notes')}</h3>
                <div style={{ whiteSpace: 'pre-wrap', fontSize: 13 }}>{detail.notes}</div>
              </section>
            )}
          </>
        )}
      </DetailDrawer>
    </div>
  );
};

const Field: React.FC<{ label: string; value: React.ReactNode }> = ({ label, value }) => (
  <div>
    <div style={{ fontSize: 11, color: '#666', textTransform: 'uppercase', letterSpacing: 0.3 }}>{label}</div>
    <div style={{ fontSize: 14 }}>{value ?? '-'}</div>
  </div>
);

export default ShipmentsByStatus;
