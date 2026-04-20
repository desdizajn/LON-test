import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P7.1 — "Чека прием": MRNRegistry rows where UsedQuantity = 0.
 *
 * Pragmatic proxy for ASN until a dedicated advance-shipment-notice domain
 * lands. If customs paperwork is filed (MRN exists) but nothing has been
 * booked to inventory yet, physical goods are in transit.
 */

type Registry = {
  id: string;
  mrn: string;
  totalQuantity: number;
  usedQuantity: number;
  dischargedQuantity?: number | null;
  expiryDate?: string | null;
  isActive: boolean;
  customsDeclarationId?: string | null;
  customsDeclaration?: { declarationNumber?: string; declarationDate?: string; partnerId?: string | null };
};

const IncomingShipments: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Registry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await customsApi.getMRNRegistry(undefined, true);
        if (!cancelled) setRows((resp.data as Registry[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const incoming = useMemo(
    () => rows.filter((r) => r.isActive && r.usedQuantity === 0),
    [rows]
  );

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('incomingShipments.title')}</h1>
      <p style={{ color: '#666' }}>{t('incomingShipments.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12 }}>
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('incomingShipments.rowCount', { count: incoming.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              incoming,
              [
                { key: 'mrn', label: 'MRN' },
                {
                  key: 'customsDeclaration',
                  label: t('incomingShipments.declaration') as string,
                  get: (r) => r.customsDeclaration?.declarationNumber ?? '-',
                },
                {
                  key: 'declarationDate',
                  label: t('incomingShipments.declarationDate') as string,
                  get: (r) => r.customsDeclaration?.declarationDate ?? null,
                  type: 'date',
                },
                { key: 'totalQuantity', label: t('incomingShipments.expectedQty') as string, type: 'number' },
                { key: 'expiryDate', label: t('incomingShipments.expiryDate') as string, type: 'date' },
              ],
              'incoming-shipments'
            )
          }
          disabled={incoming.length === 0}
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
              <th>MRN</th>
              <th>{t('incomingShipments.declaration')}</th>
              <th>{t('incomingShipments.declarationDate')}</th>
              <th>{t('incomingShipments.expectedQty')}</th>
              <th>{t('incomingShipments.expiryDate')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={5} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && incoming.length === 0 && (
              <tr><td colSpan={5} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && incoming.map((r) => (
              <tr key={r.id}>
                <td><code>{r.mrn}</code></td>
                <td>{r.customsDeclaration?.declarationNumber ?? '-'}</td>
                <td>{formatDate(r.customsDeclaration?.declarationDate)}</td>
                <td><strong>{formatQuantity(r.totalQuantity)}</strong></td>
                <td>{formatDate(r.expiryDate)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default IncomingShipments;
