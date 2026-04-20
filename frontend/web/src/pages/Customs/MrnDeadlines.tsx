import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { customsApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P6.37 customs-deadlines + P6.36 MRN meter.
 *
 * Lists every MRNRegistry row with:
 *   - Days to expiry (colour-coded).
 *   - Used / Total bar.
 *   - Discharged / Used bar (export closure progress).
 *   - Outstanding undischarged quantity = Used − Discharged.
 *
 * Defaults to showing only rows with unfinished closure so the page doubles
 * as "open items": active MRNs where UsedQuantity > DischargedQuantity.
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
};

const MrnDeadlines: React.FC = () => {
  const { t } = useTranslation();
  const [rows, setRows] = useState<MRNRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [onlyOpen, setOnlyOpen] = useState(true);
  const [maxDaysLeft, setMaxDaysLeft] = useState<number | ''>('');

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
    return enriched.filter((r) => {
      if (onlyOpen && r.outstanding <= 0) return false;
      if (maxDaysLeft !== '' && r.days !== null && r.days > Number(maxDaysLeft)) return false;
      return true;
    });
  }, [enriched, onlyOpen, maxDaysLeft]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('mrnDeadlines.title')}</h1>
      <p style={{ color: '#666' }}>{t('mrnDeadlines.subtitle')}</p>

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
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
                <tr key={r.id}>
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

export default MrnDeadlines;
