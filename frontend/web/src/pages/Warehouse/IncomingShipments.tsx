import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import DetailDrawer from '../../components/common/DetailDrawer';

/**
 * P7.1 — "Чека прием": MRNRegistry rows where UsedQuantity = 0.
 *
 * Pragmatic proxy for ASN until a dedicated advance-shipment-notice domain
 * lands. If customs paperwork is filed (MRN exists) but nothing has been
 * booked to inventory yet, physical goods are in transit.
 *
 * Row click opens a drawer with full declaration + line preview so the
 * operator can confirm what to expect without leaving the list.
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
  customsDeclaration?: {
    id?: string;
    declarationNumber?: string;
    declarationDate?: string;
    partnerId?: string | null;
    partnerName?: string | null;
    procedureCode?: string | null;
    totalCustomsValue?: number | null;
    currency?: string | null;
  };
};

type DeclarationDetail = {
  id: string;
  declarationNumber: string;
  mrn: string;
  declarationDate: string;
  partnerName?: string | null;
  procedureCode?: string | null;
  procedureName?: string | null;
  totalCustomsValue?: number | null;
  currency?: string | null;
  lines?: Array<{
    id: string;
    lineNumber?: number;
    itemCode?: string;
    itemName?: string;
    batchNumber?: string | null;
    quantity: number;
    uoMCode?: string | null;
    tariffCode?: string | null;
    customsValue?: number | null;
  }>;
};

const IncomingShipments: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Registry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [detailId, setDetailId] = useState<string | null>(null);
  const [detail, setDetail] = useState<DeclarationDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

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

  useEffect(() => {
    if (!detailId) {
      setDetail(null);
      return;
    }
    let cancelled = false;
    (async () => {
      setDetailLoading(true);
      try {
        const resp = await customsApi.getDeclaration(detailId);
        if (!cancelled) setDetail(resp.data as DeclarationDetail);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setDetailLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [detailId]);

  const incoming = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows
      .filter((r) => r.isActive && r.usedQuantity === 0)
      .filter((r) => {
        if (!q) return true;
        return (
          r.mrn.toLowerCase().includes(q) ||
          (r.customsDeclaration?.declarationNumber ?? '').toLowerCase().includes(q) ||
          (r.customsDeclaration?.partnerName ?? '').toLowerCase().includes(q)
        );
      });
  }, [rows, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('incomingShipments.title')}</h1>
      <p style={{ color: '#666' }}>{t('incomingShipments.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('incomingShipments.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 260 }}
        />
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
              <tr
                key={r.id}
                style={{ cursor: r.customsDeclarationId ? 'pointer' : undefined }}
                onClick={() => r.customsDeclarationId && setDetailId(r.customsDeclarationId)}
                title={r.customsDeclarationId ? (t('incomingShipments.clickToOpen') as string) : undefined}
              >
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

      <DetailDrawer
        open={!!detailId}
        onClose={() => setDetailId(null)}
        title={detail?.declarationNumber ?? (t('common.loading') as string)}
        subtitle={detail?.mrn ?? undefined}
        width={680}
      >
        {detailLoading && <div>{t('common.loading')}</div>}
        {!detailLoading && detail && (
          <>
            <section style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 18 }}>
              <Field label={t('declarationsByType.procedure') as string} value={`${detail.procedureCode ?? '-'}${detail.procedureName ? ' · ' + detail.procedureName : ''}`} />
              <Field label={t('declarationsByType.date') as string} value={formatDate(detail.declarationDate)} />
              <Field label={t('declarationsByType.partner') as string} value={detail.partnerName ?? '-'} />
              <Field label={t('declarationsByType.customsValue') as string} value={`${formatQuantity(detail.totalCustomsValue ?? 0)} ${detail.currency ?? ''}`} />
            </section>
            <h3 style={{ fontSize: 14, margin: '0 0 8px' }}>
              {t('declarationsByType.linesTitle', { count: detail.lines?.length ?? 0 })}
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
                      <th style={{ textAlign: 'right' }}>{t('declarationsByType.line.qty')}</th>
                      <th style={{ textAlign: 'right' }}>{t('declarationsByType.line.customsValue')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.lines.map((l, i) => (
                      <tr key={l.id ?? i}>
                        <td>{l.lineNumber ?? i + 1}</td>
                        <td>{l.itemCode ?? '-'}{l.itemName ? ' · ' + l.itemName : ''}</td>
                        <td>{l.batchNumber ?? '-'}</td>
                        <td style={{ textAlign: 'right' }}>{formatQuantity(l.quantity)} {l.uoMCode ?? ''}</td>
                        <td style={{ textAlign: 'right' }}>{l.customsValue !== null && l.customsValue !== undefined ? formatQuantity(l.customsValue) : '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
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

export default IncomingShipments;
