import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.9 — Minutes variance.
 *
 * Until OperationTimeLog entity lands, this is a client-side estimate:
 *   planned minutes = orderQuantity × standardMinutesPerPiece (configurable)
 *   actual minutes  = (PlannedEnd − PlannedStart) × workingDayHours × 60
 *                     (proxy; real actuals need timesheet join)
 * Surfaces the deltas to flag POs that are over-consuming scheduled minutes.
 */

type Order = {
  id: string;
  orderNumber: string;
  item?: { code?: string; name?: string };
  orderQuantity: number;
  producedQuantity: number;
  status: number;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
};

const MinutesVariance: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [standardMinutesPerPiece, setStandardMinutesPerPiece] = useState<number>(15);
  const [workingHoursPerDay, setWorkingHoursPerDay] = useState<number>(8);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await productionApi.getOrders();
        if (!cancelled) setOrders((resp.data as Order[]) ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return orders
      .filter((o) => o.status !== 6) // exclude cancelled
      .map((o) => {
        const plannedMinutes = o.orderQuantity * standardMinutesPerPiece;
        const plannedDurationDays = Math.max(0.1, (new Date(o.plannedEndDate).getTime() - new Date(o.plannedStartDate).getTime()) / 86_400_000);
        const scheduledMinutes = plannedDurationDays * workingHoursPerDay * 60;
        const variance = scheduledMinutes - plannedMinutes;
        const variancePct = plannedMinutes > 0 ? variance / plannedMinutes : 0;
        return { ...o, plannedMinutes, scheduledMinutes, variance, variancePct };
      })
      .filter((r) => !q || `${r.orderNumber} ${r.item?.code ?? ''} ${r.item?.name ?? ''}`.toLowerCase().includes(q))
      .sort((a, b) => b.variancePct - a.variancePct);
  }, [orders, standardMinutesPerPiece, workingHoursPerDay, search]);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('minutesVariance.title')}</h1>
      <p style={{ color: '#666' }}>{t('minutesVariance.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center', flexWrap: 'wrap' }}>
        <label>{t('minutesVariance.standardMinutes')}: <input type="number" min={0.1} step="0.1" value={standardMinutesPerPiece} onChange={(e) => setStandardMinutesPerPiece(Math.max(0.1, Number(e.target.value)))} style={{ width: 70, padding: 4 }} /></label>
        <label>{t('minutesVariance.workingHours')}: <input type="number" min={1} max={24} value={workingHoursPerDay} onChange={(e) => setWorkingHoursPerDay(Math.max(1, Math.min(24, Number(e.target.value))))} style={{ width: 60, padding: 4 }} /></label>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('productionToday.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(rows, [
          { key: 'orderNumber', label: 'Order' },
          { key: 'itemCode', label: t('productionToday.item') as string, get: (o: any) => `${o.item?.code ?? ''} ${o.item?.name ?? ''}`.trim() },
          { key: 'orderQuantity', label: t('productionToday.orderQty') as string, type: 'number' },
          { key: 'plannedMinutes', label: t('minutesVariance.plannedMinutes') as string, type: 'number' },
          { key: 'scheduledMinutes', label: t('minutesVariance.scheduledMinutes') as string, type: 'number' },
          { key: 'variance', label: t('minutesVariance.variance') as string, type: 'number' },
          { key: 'variancePct', label: t('minutesVariance.variancePct') as string, type: 'number' },
        ], 'minutes-variance')} disabled={rows.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('productionToday.orderNumber')}</th>
              <th>{t('productionToday.item')}</th>
              <th>{t('productionToday.orderQty')}</th>
              <th>{t('minutesVariance.plannedMinutes')}</th>
              <th>{t('minutesVariance.scheduledMinutes')}</th>
              <th>{t('minutesVariance.variance')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.map((r) => {
              const color = r.variance >= 0 ? '#2e7d32' : '#c62828';
              return (
                <tr key={r.id}>
                  <td><Link to={`/production/orders?order=${r.id}`}><code>{r.orderNumber}</code></Link></td>
                  <td><strong>{r.item?.code}</strong> {r.item?.name}</td>
                  <td>{formatQuantity(r.orderQuantity, 0)}</td>
                  <td>{formatQuantity(r.plannedMinutes, 0)}</td>
                  <td>{formatQuantity(r.scheduledMinutes, 0)}</td>
                  <td style={{ color, fontWeight: 600 }}>
                    {r.variance >= 0 ? '+' : ''}{formatQuantity(r.variance, 0)} ({(r.variancePct * 100).toFixed(1)}%)
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default MinutesVariance;
