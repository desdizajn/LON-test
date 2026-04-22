import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import DetailDrawer from '../../components/common/DetailDrawer';

/**
 * P9.5 — Pack lists.
 *
 * Aggregates shipment lines into printable pack lists. Row click opens a
 * drawer with the shipment's line items (item, batch, MRN, qty) — the layout
 * matches a physical pack slip for easy print.
 */

type Line = { id: string; lineNumber?: number; itemId: string; itemCode?: string; itemName?: string; batchNumber?: string | null; mrn?: string | null; quantity: number; uoMCode?: string | null };
type Shipment = {
  id: string;
  shipmentNumber: string;
  shipmentDate: string;
  customerName?: string | null;
  carrierName?: string | null;
  salesOrderNumber?: string | null;
  trackingNumber?: string | null;
  status: number;
  lines?: Line[];
};

const PackLists: React.FC = () => {
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
          // Packed (4) or Shipped (5) — those are pack-list relevant.
          setRows(all.filter((s) => s.status === 4 || s.status === 5));
        }
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter((s) => `${s.shipmentNumber} ${s.customerName ?? ''} ${s.salesOrderNumber ?? ''}`.toLowerCase().includes(q));
  }, [rows, search]);

  const detailTotal = detail?.lines?.reduce((a, l) => a + l.quantity, 0) ?? 0;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('packLists.title')}</h1>
      <p style={{ color: '#666' }}>{t('packLists.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('packLists.searchPlaceholder') as string} style={{ padding: 6, minWidth: 260 }} />
        <span style={{ color: '#888', marginLeft: 'auto' }}>{t('packLists.rowCount', { count: filtered.length })}</span>
        <button onClick={() => exportToCsv(filtered, [
          { key: 'shipmentNumber', label: t('packLists.number') as string },
          { key: 'shipmentDate', label: t('packLists.date') as string, type: 'date' },
          { key: 'customerName', label: t('packLists.customer') as string },
          { key: 'salesOrderNumber', label: 'SO #' },
          { key: 'lineCount', label: t('packLists.lines') as string, get: (r: Shipment) => r.lines?.length ?? 0, type: 'number', decimals: 0 },
          { key: 'totalQty', label: t('packLists.totalQty') as string, get: (r: Shipment) => r.lines?.reduce((a, l) => a + l.quantity, 0) ?? 0, type: 'number' },
        ], 'pack-lists')} disabled={filtered.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('packLists.number')}</th>
              <th>{t('packLists.date')}</th>
              <th>{t('packLists.customer')}</th>
              <th>SO #</th>
              <th>{t('packLists.lines')}</th>
              <th>{t('packLists.totalQty')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>}
            {!loading && filtered.map((s) => (
              <tr key={s.id} style={{ cursor: 'pointer' }} onClick={() => setDetail(s)} title={t('packLists.clickToOpen') as string}>
                <td><strong>{s.shipmentNumber}</strong></td>
                <td>{formatDate(s.shipmentDate)}</td>
                <td>{s.customerName ?? '-'}</td>
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
        width={720}
        footer={detail ? (
          <button onClick={() => window.print()} style={{ padding: '6px 14px', background: 'var(--taris-blue-500, #1e88e5)', color: 'white', border: 'none', borderRadius: 4 }}>
            🖨 {t('packLists.print')}
          </button>
        ) : null}
      >
        {detail && (
          <div>
            <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 18, padding: 12, background: '#f8fafc', borderRadius: 6 }}>
              <div><div style={{ fontSize: 11, color: '#666' }}>{t('packLists.customer')}</div><div style={{ fontSize: 14 }}>{detail.customerName ?? '-'}</div></div>
              <div><div style={{ fontSize: 11, color: '#666' }}>{t('packLists.carrier')}</div><div style={{ fontSize: 14 }}>{detail.carrierName ?? '-'}</div></div>
              <div><div style={{ fontSize: 11, color: '#666' }}>{t('packLists.date')}</div><div style={{ fontSize: 14 }}>{formatDate(detail.shipmentDate)}</div></div>
              <div><div style={{ fontSize: 11, color: '#666' }}>{t('packLists.tracking')}</div><div style={{ fontSize: 14 }}>{detail.trackingNumber ?? '-'}</div></div>
            </section>

            <h3 style={{ fontSize: 14, margin: '0 0 8px' }}>{t('packLists.linesTitle', { count: detail.lines?.length ?? 0 })}</h3>
            <table style={{ width: '100%', fontSize: 13 }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'left' }}>#</th>
                  <th style={{ textAlign: 'left' }}>{t('packLists.item')}</th>
                  <th style={{ textAlign: 'left' }}>{t('packLists.batch')}</th>
                  <th style={{ textAlign: 'left' }}>MRN</th>
                  <th style={{ textAlign: 'right' }}>{t('packLists.qty')}</th>
                </tr>
              </thead>
              <tbody>
                {detail.lines?.map((l, i) => (
                  <tr key={l.id ?? i}>
                    <td>{l.lineNumber ?? i + 1}</td>
                    <td>{l.itemCode ?? l.itemId}{l.itemName ? ' · ' + l.itemName : ''}</td>
                    <td>{l.batchNumber ?? '-'}</td>
                    <td>{l.mrn ?? '-'}</td>
                    <td style={{ textAlign: 'right' }}>{formatQuantity(l.quantity)} {l.uoMCode ?? ''}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={4} style={{ textAlign: 'right', fontWeight: 600, paddingTop: 8 }}>{t('packLists.totalQty')}:</td>
                  <td style={{ textAlign: 'right', fontWeight: 700 }}>{formatQuantity(detailTotal)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </DetailDrawer>
    </div>
  );
};

export default PackLists;
