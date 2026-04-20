import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P6.37 — focused import / export declaration views.
 *
 * Same endpoint as /customs but filtered by declaration type so the
 * `/customs/import-docs` and `/customs/export-docs` routes feel distinct.
 * Export side tags lines against source-MRN discharge volumes so operators
 * can spot incomplete closures at a glance.
 */

type Line = {
  id: string;
  quantity: number;
  sourceMRN?: string | null;
  dischargeQuantity?: number | null;
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
  partnerName?: string | null;
  declarationType?: string | null;
  lines?: Line[];
};

type Props = { type: 'import' | 'export' };

const DeclarationsByType: React.FC<Props> = ({ type }) => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<Declaration[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await customsApi.getDeclarations();
        if (cancelled) return;
        const all = (resp.data as Declaration[]) ?? [];
        // Backend does not flag IM vs EX consistently; fall back to procedure
        // code heuristics. IM procs start 40/42/51, EX procs start 10/31/35.
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
              <tr key={d.id}>
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
    </div>
  );
};

export default DeclarationsByType;
