import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { wmsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P7.3 — "Недостиг / Вишок / Разлика": cycle-count variance report.
 *
 * CycleCountLine.Variance = CountedQuantity − SystemQuantity. Exposes all
 * non-zero variances across completed or in-progress counts. Focus on the
 * count lines, not the parent counts — the operator cares about row-level
 * discrepancies.
 */

type Line = {
  id: string;
  locationId: string;
  itemId: string;
  batchNumber?: string | null;
  mrn?: string | null;
  systemQuantity: number;
  countedQuantity?: number | null;
  variance?: number | null;
  item?: { code: string; name: string } | null;
  location?: { code: string; name: string } | null;
  uoM?: { code: string; name: string } | null;
};

type CycleCount = {
  id: string;
  countNumber: string;
  scheduledDate: string;
  completedDate?: string | null;
  status: number;
  warehouse?: { code: string; name: string } | null;
  lines?: Line[];
};

const VarianceReport: React.FC = () => {
  const { t } = useTranslation();
  const [counts, setCounts] = useState<CycleCount[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'shortage' | 'surplus'>('all');
  const [itemSearch, setItemSearch] = useState('');
  const [countSearch, setCountSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await wmsApi.getCycleCounts();
        if (!cancelled) setCounts((resp.data as CycleCount[]) ?? []);
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

  const flat = useMemo(() => {
    const rows: Array<Line & { countNumber: string; scheduledDate: string }> = [];
    counts.forEach((c) => {
      (c.lines ?? []).forEach((l) => {
        if (l.variance === undefined || l.variance === null || l.variance === 0) return;
        rows.push({ ...l, countNumber: c.countNumber, scheduledDate: c.scheduledDate });
      });
    });
    return rows;
  }, [counts]);

  const filtered = useMemo(() => {
    const iq = itemSearch.trim().toLowerCase();
    const cq = countSearch.trim().toLowerCase();
    return flat.filter((r) => {
      if (filter === 'shortage' && (r.variance ?? 0) >= 0) return false;
      if (filter === 'surplus' && (r.variance ?? 0) <= 0) return false;
      if (iq) {
        const hay = `${r.item?.code ?? ''} ${r.item?.name ?? ''} ${r.batchNumber ?? ''} ${r.mrn ?? ''}`.toLowerCase();
        if (!hay.includes(iq)) return false;
      }
      if (cq && !r.countNumber.toLowerCase().includes(cq)) return false;
      return true;
    });
  }, [flat, filter, itemSearch, countSearch]);

  const totals = useMemo(() => ({
    shortage: flat.filter((r) => (r.variance ?? 0) < 0).length,
    surplus: flat.filter((r) => (r.variance ?? 0) > 0).length,
    netQty: flat.reduce((acc, r) => acc + (r.variance ?? 0), 0),
  }), [flat]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('variance.title')}</h1>
      <p style={{ color: '#666' }}>{t('variance.subtitle')}</p>

      <div style={{ display: 'flex', gap: 8, marginBottom: 10, alignItems: 'center', flexWrap: 'wrap' }}>
        <button onClick={() => setFilter('all')} style={{ padding: '6px 12px', fontWeight: filter === 'all' ? 'bold' : 'normal' }}>
          {t('variance.all')} ({flat.length})
        </button>
        <button onClick={() => setFilter('shortage')} style={{ padding: '6px 12px', fontWeight: filter === 'shortage' ? 'bold' : 'normal', color: '#c00' }}>
          ⬇ {t('variance.shortage')} ({totals.shortage})
        </button>
        <button onClick={() => setFilter('surplus')} style={{ padding: '6px 12px', fontWeight: filter === 'surplus' ? 'bold' : 'normal', color: '#27ae60' }}>
          ⬆ {t('variance.surplus')} ({totals.surplus})
        </button>
        <span style={{ color: '#666', marginLeft: 'auto' }}>
          {t('variance.netQty')}: <strong>{formatQuantity(totals.netQty)}</strong>
        </span>
      </div>
      <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        <input
          type="text"
          value={countSearch}
          onChange={(e) => setCountSearch(e.target.value)}
          placeholder={t('variance.countSearchPlaceholder') as string}
          style={{ padding: 6, minWidth: 180 }}
        />
        <input
          type="text"
          value={itemSearch}
          onChange={(e) => setItemSearch(e.target.value)}
          placeholder={t('variance.itemSearchPlaceholder') as string}
          style={{ padding: 6, minWidth: 240 }}
        />
        <span style={{ fontSize: 12, color: '#666' }}>{t('inventory.filters.showing', { count: filtered.length, total: flat.length })}</span>
        <button
          onClick={() =>
            exportToCsv(
              filtered,
              [
                { key: 'countNumber', label: t('variance.countNumber') as string },
                { key: 'scheduledDate', label: t('variance.scheduledDate') as string, type: 'date' },
                { key: 'location', label: t('common.code') as string, get: (r) => r.location?.code ?? '' },
                { key: 'item', label: t('common.name') as string, get: (r) => r.item?.name ?? '' },
                { key: 'batchNumber', label: 'Batch' },
                { key: 'mrn', label: 'MRN' },
                { key: 'systemQuantity', label: t('variance.system') as string, type: 'number' },
                { key: 'countedQuantity', label: t('variance.counted') as string, type: 'number' },
                { key: 'variance', label: t('variance.variance') as string, type: 'number' },
              ],
              'variance'
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
              <th>{t('variance.countNumber')}</th>
              <th>{t('variance.scheduledDate')}</th>
              <th>{t('variance.location')}</th>
              <th>{t('common.name')}</th>
              <th>Batch</th>
              <th>{t('variance.system')}</th>
              <th>{t('variance.counted')}</th>
              <th>{t('variance.variance')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && (
              <tr><td colSpan={8} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('common.noData')}</td></tr>
            )}
            {!loading && filtered.map((r) => (
              <tr key={r.id}>
                <td>{r.countNumber}</td>
                <td>{formatDate(r.scheduledDate)}</td>
                <td><code>{r.location?.code ?? '-'}</code></td>
                <td>{r.item?.name ?? '-'}</td>
                <td>{r.batchNumber ?? '-'}</td>
                <td>{formatQuantity(r.systemQuantity)}</td>
                <td>{r.countedQuantity === null || r.countedQuantity === undefined ? '-' : formatQuantity(r.countedQuantity)}</td>
                <td style={{ color: (r.variance ?? 0) < 0 ? '#c00' : '#27ae60', fontWeight: 'bold' }}>
                  {(r.variance ?? 0) > 0 ? '+' : ''}{formatQuantity(r.variance ?? 0)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default VarianceReport;
