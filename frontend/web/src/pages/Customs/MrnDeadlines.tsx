import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';
import DetailDrawer from '../../components/common/DetailDrawer';

/**
 * P6.37 customs-deadlines + P6.36 MRN meter.
 *
 * Lists every MRNRegistry row with:
 *   - Days to expiry (colour-coded).
 *   - Used / Total bar.
 *   - Discharged / Used bar (export closure progress).
 *   - Outstanding undischarged quantity = Used − Discharged.
 *
 * Row click opens a drawer with the source declaration (partner, procedure,
 * customs value, lines). Text search across MRN / declaration number / partner
 * helps narrow the list when there are hundreds of active rows.
 */

type MRNRow = {
  id: string;
  mrn: string;
  totalQuantity: number;
  usedQuantity: number;
  dischargedQuantity?: number | null;
  expiryDate?: string | null;
  isActive: boolean;
  customsDeclarationId?: string | null;
  customsDeclaration?: {
    declarationNumber?: string;
    partnerName?: string | null;
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
  }>;
};

const MrnDeadlines: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<MRNRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [onlyOpen, setOnlyOpen] = useState(true);
  const [maxDaysLeft, setMaxDaysLeft] = useState<number | ''>('');
  const [search, setSearch] = useState('');
  const [detailId, setDetailId] = useState<string | null>(null);
  const [detail, setDetail] = useState<DeclarationDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await customsApi.getMRNRegistry(undefined, undefined);
        if (!cancelled) setRows((resp.data as MRNRow[]) ?? []);
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

  const enriched = useMemo(() => {
    return rows.map((r) => {
      const discharged = r.dischargedQuantity ?? 0;
      const outstanding = r.usedQuantity - discharged;
      const usedPct = r.totalQuantity > 0 ? (r.usedQuantity / r.totalQuantity) * 100 : 0;
      const dischargedPct = r.usedQuantity > 0 ? (discharged / r.usedQuantity) * 100 : 0;
      const days = r.expiryDate ? Math.round((new Date(r.expiryDate).getTime() - Date.now()) / 86_400_000) : null;
      return { ...r, outstanding, usedPct, dischargedPct, days };
    });
  }, [rows]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return enriched.filter((r) => {
      if (onlyOpen && r.outstanding <= 0) return false;
      if (maxDaysLeft !== '' && r.days !== null && r.days > Number(maxDaysLeft)) return false;
      if (q) {
        const hay = `${r.mrn} ${r.customsDeclaration?.declarationNumber ?? ''} ${r.customsDeclaration?.partnerName ?? ''}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });
  }, [enriched, onlyOpen, maxDaysLeft, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('mrnDeadlines.title')}</h1>
      <p style={{ color: '#666' }}>{t('mrnDeadlines.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <input type="checkbox" checked={onlyOpen} onChange={(e) => setOnlyOpen(e.target.checked)} />
          {t('mrnDeadlines.onlyOpen')}
        </label>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {t('mrnDeadlines.daysLeftFilter')}:
          <input
            type="number"
            value={maxDaysLeft}
            onChange={(e) => setMaxDaysLeft(e.target.value === '' ? '' : Number(e.target.value))}
            style={{ width: 80, padding: 4 }}
            min={-30}
          />
        </label>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('mrnDeadlines.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 240 }}
        />
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('mrnDeadlines.rowCount', { count: filtered.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'mrn', label: 'MRN' },
                { key: 'expiryDate', label: t('mrnDeadlines.expiryDate') as string, type: 'date' },
                { key: 'days', label: t('mrnDeadlines.daysLeft') as string, type: 'number', decimals: 0 },
                { key: 'totalQuantity', label: 'Total', type: 'number' },
                { key: 'usedQuantity', label: 'Used', type: 'number' },
                { key: 'dischargedQuantity', label: 'Discharged', type: 'number' },
                { key: 'outstanding', label: t('mrnDeadlines.outstanding') as string, type: 'number' },
                { key: 'isActive', label: t('mrnDeadlines.status') as string, get: (r) => (r.isActive ? t('mrnDeadlines.active') : t('mrnDeadlines.closed')) },
              ],
              'mrn-deadlines'
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
              <th>MRN</th>
              <th>{t('mrnDeadlines.expiryDate')}</th>
              <th>{t('mrnDeadlines.daysLeft')}</th>
              <th>{t('mrnDeadlines.consumption')}</th>
              <th>{t('mrnDeadlines.discharge')}</th>
              <th>{t('mrnDeadlines.outstanding')}</th>
              <th>{t('mrnDeadlines.status')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>
            )}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((r) => {
              const daysColor =
                r.days === null ? '#888' : r.days < 0 ? '#c00' : r.days < 14 ? '#e67e22' : r.days < 30 ? '#f1c40f' : '#27ae60';
              return (
                <tr
                  key={r.id}
                  style={{ cursor: r.customsDeclarationId ? 'pointer' : undefined }}
                  onClick={() => r.customsDeclarationId && setDetailId(r.customsDeclarationId)}
                  title={r.customsDeclarationId ? (t('incomingShipments.clickToOpen') as string) : undefined}
                >
                  <td><code>{r.mrn}</code></td>
                  <td>{formatDate(r.expiryDate)}</td>
                  <td style={{ color: daysColor, fontWeight: 'bold' }}>
                    {r.days === null ? '-' : r.days < 0 ? t('mrnDeadlines.expired') : r.days}
                  </td>
                  <td>
                    <Bar pct={r.usedPct} tone={r.usedPct > 95 ? 'red' : r.usedPct > 80 ? 'amber' : 'green'} />
                    <div style={{ fontSize: 11, color: '#555' }}>
                      {formatQuantity(r.usedQuantity)} / {formatQuantity(r.totalQuantity)}
                    </div>
                  </td>
                  <td>
                    <Bar pct={r.dischargedPct} tone={r.dischargedPct > 95 ? 'green' : r.dischargedPct > 50 ? 'amber' : 'red'} />
                    <div style={{ fontSize: 11, color: '#555' }}>
                      {formatQuantity(r.dischargedQuantity ?? 0)} / {formatQuantity(r.usedQuantity)}
                    </div>
                  </td>
                  <td>
                    <strong style={{ color: r.outstanding > 0 ? '#e67e22' : '#27ae60' }}>
                      {formatQuantity(r.outstanding)}
                    </strong>
                  </td>
                  <td>{r.isActive ? t('mrnDeadlines.active') : t('mrnDeadlines.closed')}</td>
                </tr>
              );
            })}
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
                    </tr>
                  </thead>
                  <tbody>
                    {detail.lines.map((l, i) => (
                      <tr key={l.id ?? i}>
                        <td>{l.lineNumber ?? i + 1}</td>
                        <td>{l.itemCode ?? '-'}{l.itemName ? ' · ' + l.itemName : ''}</td>
                        <td>{l.batchNumber ?? '-'}</td>
                        <td style={{ textAlign: 'right' }}>{formatQuantity(l.quantity)} {l.uoMCode ?? ''}</td>
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

const Bar: React.FC<{ pct: number; tone: 'red' | 'amber' | 'green' }> = ({ pct, tone }) => {
  const colors = { red: '#e74c3c', amber: '#f1c40f', green: '#27ae60' } as const;
  const clamped = Math.max(0, Math.min(100, pct));
  return (
    <div style={{ width: 140, height: 8, background: '#eee', borderRadius: 3, overflow: 'hidden' }}>
      <div style={{ width: `${clamped}%`, height: '100%', background: colors[tone] }} />
    </div>
  );
};

const Field: React.FC<{ label: string; value: React.ReactNode }> = ({ label, value }) => (
  <div>
    <div style={{ fontSize: 11, color: '#666', textTransform: 'uppercase', letterSpacing: 0.3 }}>{label}</div>
    <div style={{ fontSize: 14 }}>{value ?? '-'}</div>
  </div>
);

export default MrnDeadlines;
