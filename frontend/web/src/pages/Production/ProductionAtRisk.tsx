import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { productionApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatDate, formatQuantity, formatPercent } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P8.4 — Налози во ризик од доцнење.
 *
 * Heuristic without needing RoutingOperation aggregates:
 *   scheduleUsedPct = (now - PlannedStart) / (PlannedEnd - PlannedStart)
 *   progressPct     = ProducedQuantity / OrderQuantity
 *   gap             = scheduleUsedPct - progressPct
 *
 * "Red" when gap >= 0.25 AND PlannedEnd is within 7 days (or past).
 * "Amber" when gap >= 0.10.
 * Rows below the amber threshold are hidden.
 *
 * This is intentionally a self-contained signal that uses only fields already
 * returned by `GET /Production/orders`. A future upgrade (P8.9 piece-level
 * time log) will let us swap the heuristic for a real operations burn-down.
 */

type Order = {
  id: string;
  orderNumber: string;
  item?: { code?: string; name?: string };
  orderQuantity: number;
  producedQuantity: number;
  uoM?: { code?: string };
  status: number;
  plannedStartDate: string;
  plannedEndDate: string;
  customerOrderNumber?: string | null;
};

type Risk = 'red' | 'amber';

type Row = {
  order: Order;
  scheduleUsedPct: number;
  progressPct: number;
  gap: number;
  daysToEnd: number;
  risk: Risk;
};

const ProductionAtRisk: React.FC = () => {
  const { t } = useTranslation();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [riskFilter, setRiskFilter] = useState<'all' | 'red' | 'amber'>('all');

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

  const rows: Row[] = useMemo(() => {
    const now = Date.now();
    const oneDay = 24 * 60 * 60 * 1000;
    const active = orders.filter((o) => o.status !== 4 && o.status !== 5 && o.status !== 6);
    const out: Row[] = [];
    for (const o of active) {
      const startMs = new Date(o.plannedStartDate).getTime();
      const endMs = new Date(o.plannedEndDate).getTime();
      if (!isFinite(startMs) || !isFinite(endMs) || endMs <= startMs) continue;
      const scheduleUsedPct = Math.max(0, Math.min(1, (now - startMs) / (endMs - startMs)));
      const progressPct = o.orderQuantity > 0 ? Math.max(0, Math.min(1, o.producedQuantity / o.orderQuantity)) : 0;
      const gap = scheduleUsedPct - progressPct;
      const daysToEnd = (endMs - now) / oneDay;
      let risk: Risk | null = null;
      if (gap >= 0.25 && daysToEnd <= 7) risk = 'red';
      else if (gap >= 0.10) risk = 'amber';
      if (!risk) continue;
      out.push({ order: o, scheduleUsedPct, progressPct, gap, daysToEnd, risk });
    }
    return out.sort((a, b) => b.gap - a.gap);
  }, [orders]);

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((r) => {
      if (riskFilter !== 'all' && r.risk !== riskFilter) return false;
      if (!q) return true;
      const hay = `${r.order.orderNumber} ${r.order.item?.code ?? ''} ${r.order.item?.name ?? ''} ${r.order.customerOrderNumber ?? ''}`.toLowerCase();
      return hay.includes(q);
    });
  }, [rows, search, riskFilter]);

  const redCount = rows.filter((r) => r.risk === 'red').length;
  const amberCount = rows.filter((r) => r.risk === 'amber').length;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('productionAtRisk.title')}</h1>
      <p style={{ color: '#666' }}>{t('productionAtRisk.subtitle')}</p>

      <div style={{ display: 'flex', gap: 24, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap', alignItems: 'center' }}>
        <button onClick={() => setRiskFilter('all')} style={{ padding: '6px 12px', fontWeight: riskFilter === 'all' ? 'bold' : 'normal' }}>{t('productionAtRisk.all')} ({rows.length})</button>
        <button onClick={() => setRiskFilter('red')} style={{ padding: '6px 12px', fontWeight: riskFilter === 'red' ? 'bold' : 'normal', color: '#c62828' }}>{t('productionAtRisk.red')} ({redCount})</button>
        <button onClick={() => setRiskFilter('amber')} style={{ padding: '6px 12px', fontWeight: riskFilter === 'amber' ? 'bold' : 'normal', color: '#f9a825' }}>{t('productionAtRisk.amber')} ({amberCount})</button>
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('productionToday.searchPlaceholder') as string}
          style={{ padding: 6, minWidth: 240 }}
        />
        <span style={{ color: '#888', marginLeft: 'auto' }}>
          {t('productionAtRisk.rowCount', { count: filteredRows.length })}
        </span>
        <button
          onClick={() =>
            exportToCsv(
              filteredRows,
              [
                { key: 'orderNumber', label: 'Order', get: (r) => r.order.orderNumber },
                { key: 'item', label: 'Item', get: (r) => `${r.order.item?.code ?? ''} ${r.order.item?.name ?? ''}`.trim() },
                { key: 'risk', label: 'Risk', get: (r) => r.risk },
                { key: 'scheduleUsedPct', label: 'ScheduleUsed', get: (r) => r.scheduleUsedPct, type: 'number' },
                { key: 'progressPct', label: 'Progress', get: (r) => r.progressPct, type: 'number' },
                { key: 'gap', label: 'Gap', get: (r) => r.gap, type: 'number' },
                { key: 'plannedEnd', label: 'PlannedEnd', get: (r) => r.order.plannedEndDate, type: 'date' },
                { key: 'daysToEnd', label: 'DaysToEnd', get: (r) => r.daysToEnd.toFixed(1), type: 'number' },
              ],
              'production-at-risk'
            )
          }
          disabled={filteredRows.length === 0}
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
              <th>{t('productionAtRisk.risk')}</th>
              <th>{t('productionAtRisk.orderNumber')}</th>
              <th>{t('productionAtRisk.item')}</th>
              <th>{t('productionAtRisk.scheduleUsed')}</th>
              <th>{t('productionAtRisk.progress')}</th>
              <th>{t('productionAtRisk.gap')}</th>
              <th>{t('productionAtRisk.plannedEnd')}</th>
              <th>{t('productionAtRisk.daysToEnd')}</th>
              <th>{t('productionAtRisk.remainingQty')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filteredRows.length === 0 && (
              <tr><td colSpan={9} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{search || riskFilter !== 'all' ? t('inventory.filters.noResults') : t('productionAtRisk.noneAtRisk')}</td></tr>
            )}
            {!loading && filteredRows.map((r) => {
              const o = r.order;
              const color = r.risk === 'red' ? '#c62828' : '#f9a825';
              const uom = o.uoM?.code ?? '';
              const remaining = Math.max(0, o.orderQuantity - o.producedQuantity);
              return (
                <tr key={o.id}>
                  <td><span style={{ display: 'inline-block', padding: '2px 8px', borderRadius: 12, background: color, color: '#fff', fontSize: 12, fontWeight: 600 }}>{r.risk.toUpperCase()}</span></td>
                  <td><Link to={`/production/orders?order=${o.id}`}><code>{o.orderNumber}</code></Link></td>
                  <td><strong>{o.item?.code}</strong> {o.item?.name}</td>
                  <td>{formatPercent(r.scheduleUsedPct, 0)}</td>
                  <td>{formatPercent(r.progressPct, 0)}</td>
                  <td style={{ color, fontWeight: 600 }}>+{formatPercent(r.gap, 0)}</td>
                  <td>{formatDate(o.plannedEndDate)}</td>
                  <td>{r.daysToEnd.toFixed(1)}</td>
                  <td>{formatQuantity(remaining)} {uom}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default ProductionAtRisk;
