import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity, formatDate } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P9.7 — FG returns.
 *
 * Lists return declarations (procedure 6121 — P2.6b). Gives operators a view
 * of goods that came back and the MRNs/lines they re-credit. Click row opens
 * the full declaration on /customs/export-docs if needed.
 */

type Line = { id: string; sourceMRN?: string | null; quantity: number };
type Declaration = {
  id: string;
  declarationNumber: string;
  mrn: string;
  declarationDate: string;
  currency?: string | null;
  totalCustomsValue?: number | null;
  procedureCode?: string | null;
  partnerName?: string | null;
  lines?: Line[];
};

const Returns: React.FC = () => {
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
        setRows(all.filter((d) => (d.procedureCode ?? '').startsWith('61')));
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
    return rows.filter((d) => `${d.declarationNumber} ${d.mrn} ${d.partnerName ?? ''}`.toLowerCase().includes(q));
  }, [rows, search]);

  const totals = useMemo(() => filtered.reduce((acc, d) => {
    acc.value += d.totalCustomsValue ?? 0;
    acc.lines += d.lines?.length ?? 0;
    acc.qty += d.lines?.reduce((s, l) => s + l.quantity, 0) ?? 0;
    return acc;
  }, { value: 0, lines: 0, qty: 0 }), [filtered]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('returns.title')}</h1>
      <p style={{ color: '#666' }}>{t('returns.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <div><small>{t('returns.count')}</small><div style={{ fontWeight: 600 }}>{filtered.length}</div></div>
        <div><small>{t('returns.totalLines')}</small><div style={{ fontWeight: 600 }}>{totals.lines}</div></div>
        <div><small>{t('returns.totalQty')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.qty, 0)}</div></div>
        <div><small>{t('returns.totalValue')}</small><div style={{ fontWeight: 600 }}>{formatQuantity(totals.value, 2)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('returns.searchPlaceholder') as string} style={{ padding: 6, minWidth: 220, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(filtered, [
          { key: 'declarationNumber', label: t('returns.number') as string },
          { key: 'mrn', label: 'MRN' },
          { key: 'procedureCode', label: t('returns.procedure') as string },
          { key: 'partnerName', label: t('returns.partner') as string },
          { key: 'declarationDate', label: t('returns.date') as string, type: 'date' },
          { key: 'totalCustomsValue', label: t('returns.value') as string, type: 'number' },
        ], 'returns')} disabled={filtered.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('returns.number')}</th>
              <th>MRN</th>
              <th>{t('returns.procedure')}</th>
              <th>{t('returns.partner')}</th>
              <th>{t('returns.date')}</th>
              <th>{t('returns.lines')}</th>
              <th>{t('returns.value')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={7} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('returns.empty')}</td></tr>}
            {!loading && filtered.map((d) => (
              <tr key={d.id}>
                <td><strong>{d.declarationNumber}</strong></td>
                <td><code>{d.mrn}</code></td>
                <td>{d.procedureCode ?? '-'}</td>
                <td>{d.partnerName ?? '-'}</td>
                <td>{formatDate(d.declarationDate)}</td>
                <td>{d.lines?.length ?? 0}</td>
                <td>{formatQuantity(d.totalCustomsValue ?? 0, 2)} {d.currency}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Returns;
