import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import DetailDrawer from '../../components/common/DetailDrawer';

/**
 * P6.37 — focused import / export declaration views.
 *
 * Same endpoint as /customs but filtered by declaration type so the
 * `/customs/import-docs` and `/customs/export-docs` routes feel distinct.
 * Export side tags lines against source-MRN discharge volumes so operators
 * can spot incomplete closures at a glance.
 *
 * Row click opens a right-side drawer with full detail (lines, values, partner)
 * via GET /Customs/declarations/{id}. The list itself remains read-only; edits
 * stay on the dedicated Customs page.
 */

type Line = {
  id: string;
  lineNumber?: number;
  itemId?: string;
  itemCode?: string;
  itemName?: string;
  quantity: number;
  uoMCode?: string | null;
  batchNumber?: string | null;
  locationCode?: string | null;
  sourceMRN?: string | null;
  dischargeQuantity?: number | null;
  unitPrice?: number | null;
  customsValue?: number | null;
  tariffCode?: string | null;
};

type Declaration = {
  id: string;
  declarationNumber: string;
  mrn: string;
  declarationDate: string;
  currency?: string | null;
  totalCustomsValue?: number | null;
  totalDuty?: number | null;
  totalVAT?: number | null;
  status?: number | string | null;
  procedureCode?: string | null;
  procedureName?: string | null;
  partnerName?: string | null;
  partnerCode?: string | null;
  declarationType?: string | null;
  zaverkaNumber?: string | null;
  zaverkaDate?: string | null;
  notes?: string | null;
  lines?: Line[];
};

type Props = { type: 'import' | 'export' };

const DeclarationsByType: React.FC<Props> = ({ type }) => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Declaration[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [detailId, setDetailId] = useState<string | null>(null);
  const [detail, setDetail] = useState<Declaration | null>(null);
  const [detailLoading, setDetailLoading] = useState<boolean>(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await customsApi.getDeclarations();
        if (cancelled) return;
        const all = (resp.data as Declaration[]) ?? [];
        const filtered = all.filter((d) => {
          const pc = (d.procedureCode ?? '').trim();
          const ex = pc.startsWith('10') || pc.startsWith('31') || pc.startsWith('35')
            || (d.declarationType ?? '').toUpperCase() === 'EX';
          return type === 'export' ? ex : !ex;
        });
        setRows(filtered);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [type]);

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
        if (!cancelled) setDetail(resp.data as Declaration);
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

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) =>
        r.declarationNumber.toLowerCase().includes(q) ||
        r.mrn.toLowerCase().includes(q) ||
        (r.partnerName ?? '').toLowerCase().includes(q)
    );
  }, [rows, search]);

  const titleKey = type === 'export' ? 'declarationsByType.exportTitle' : 'declarationsByType.importTitle';
  const subtitleKey = type === 'export' ? 'declarationsByType.exportSubtitle' : 'declarationsByType.importSubtitle';

  const rowHoverStyle: React.CSSProperties = {
    cursor: 'pointer',
  };

  return (
    <div style={{ padding: 16 }}>
      <h1>{t(titleKey)}</h1>
      <p style={{ color: '#666' }}>{t(subtitleKey)}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <input
          type="text"
          placeholder={t('declarationsByType.searchPlaceholder') as string}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ padding: 6, minWidth: 260 }}
        />
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('declarationsByType.rowCount', { count: filtered.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'declarationNumber', label: t('declarationsByType.number') as string },
                { key: 'mrn', label: 'MRN' },
                { key: 'procedureCode', label: t('declarationsByType.procedure') as string },
                { key: 'partnerName', label: t('declarationsByType.partner') as string },
                { key: 'declarationDate', label: t('declarationsByType.date') as string, type: 'date' },
                { key: 'totalCustomsValue', label: t('declarationsByType.customsValue') as string, type: 'number' },
                { key: 'totalDuty', label: t('declarationsByType.duty') as string, type: 'number' },
                { key: 'totalVAT', label: t('declarationsByType.vat') as string, type: 'number' },
                { key: 'status', label: t('declarationsByType.status') as string },
              ],
              `declarations-${type}`
            )
          }
          disabled={filtered.length === 0}
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
              <th>{t('declarationsByType.number')}</th>
              <th>MRN</th>
              <th>{t('declarationsByType.procedure')}</th>
              <th>{t('declarationsByType.partner')}</th>
              <th>{t('declarationsByType.date')}</th>
              <th>{t('declarationsByType.customsValue')}</th>
              <th>{t('declarationsByType.duty')}</th>
              <th>{t('declarationsByType.vat')}</th>
              <th>{t('declarationsByType.status')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>
            )}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((d) => (
              <tr
                key={d.id}
                style={rowHoverStyle}
                onClick={() => setDetailId(d.id)}
                title={t('declarationsByType.clickToOpen') as string}
              >
                <td><strong>{d.declarationNumber}</strong></td>
                <td><code>{d.mrn}</code></td>
                <td>{d.procedureCode ?? '-'}</td>
                <td>{d.partnerName ?? '-'}</td>
                <td>{formatDate(d.declarationDate)}</td>
                <td>{formatQuantity(d.totalCustomsValue ?? 0)} {d.currency}</td>
                <td>{formatQuantity(d.totalDuty ?? 0)}</td>
                <td>{formatQuantity(d.totalVAT ?? 0)}</td>
                <td>{d.status ?? '-'}</td>
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
              <Field label={t('declarationsByType.partnerCode') as string} value={detail.partnerCode ?? '-'} />
              <Field label={t('declarationsByType.customsValue') as string} value={`${formatQuantity(detail.totalCustomsValue ?? 0)} ${detail.currency ?? ''}`} />
              <Field label={t('declarationsByType.duty') as string} value={formatQuantity(detail.totalDuty ?? 0)} />
              <Field label={t('declarationsByType.vat') as string} value={formatQuantity(detail.totalVAT ?? 0)} />
              <Field label={t('declarationsByType.status') as string} value={detail.status ?? '-'} />
              {detail.zaverkaNumber && (
                <Field label={t('declarationsByType.zaverkaNumber') as string} value={detail.zaverkaNumber} />
              )}
              {detail.zaverkaDate && (
                <Field label={t('declarationsByType.zaverkaDate') as string} value={formatDate(detail.zaverkaDate)} />
              )}
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
                      <th style={{ textAlign: 'left' }}>{t('declarationsByType.line.sourceMrn')}</th>
                      <th style={{ textAlign: 'right' }}>{t('declarationsByType.line.discharge')}</th>
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
                        <td>{l.sourceMRN ?? '-'}</td>
                        <td style={{ textAlign: 'right' }}>{l.dischargeQuantity !== null && l.dischargeQuantity !== undefined ? formatQuantity(l.dischargeQuantity) : '-'}</td>
                        <td style={{ textAlign: 'right' }}>{l.customsValue !== null && l.customsValue !== undefined ? formatQuantity(l.customsValue) : '-'}</td>
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

export default DeclarationsByType;
