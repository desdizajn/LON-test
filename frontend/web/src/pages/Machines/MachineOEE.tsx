import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatPercent } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.3 — Overall Equipment Effectiveness (OEE).
 *
 * OEE = Availability × Performance × Quality.
 *   • Availability = (available − downtime) / available
 *   • Performance = proxy at 0.92 until cycle-time telemetry lands.
 *   • Quality = 1 − (scrap / produced) from production orders aggregation.
 * Until we have per-machine downtime + output, this uses global aggregates
 * equally distributed.
 */

type Machine = { id: string; code: string; name: string };
type ParetoBucket = { category: number; count: number; totalMinutes: number };
type Order = { producedQuantity: number; scrapQuantity: number };

const MachineOEE: React.FC = () => {
  const { t } = useTranslation();
  const [machines, setMachines] = useState<Machine[]>([]);
  const [pareto, setPareto] = useState<ParetoBucket[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [periodDays, setPeriodDays] = useState(30);
  const [search, setSearch] = useState('');
  const [performanceProxy, setPerformanceProxy] = useState(92);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [mResp, pResp] = await Promise.all([
          masterDataApi.getMachines(),
          machinesApi.getDowntimePareto(),
        ]);
        // Orders via axios-free pattern: use fetch-ish path through existing api.
        // reuse productionApi via dynamic import? Simpler: piggyback fetch below.
        const prodResp = await fetch('/api/Production/orders', {
          headers: { Authorization: `Bearer ${localStorage.getItem('token') || ''}` },
        });
        const ordersData = prodResp.ok ? await prodResp.json() : [];
        if (cancelled) return;
        setMachines((mResp.data as Machine[]) ?? []);
        setPareto((pResp.data as any)?.data ?? (pResp.data as ParetoBucket[]) ?? []);
        setOrders(Array.isArray(ordersData) ? ordersData : []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const availableMinutes = periodDays * 8 * 60;
  const totalDowntime = pareto.reduce((s, b) => s + b.totalMinutes, 0);
  const totalProduced = orders.reduce((s, o) => s + (o.producedQuantity || 0), 0);
  const totalScrap = orders.reduce((s, o) => s + (o.scrapQuantity || 0), 0);
  const globalQuality = totalProduced > 0 ? (totalProduced - totalScrap) / totalProduced : 1;

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return machines
      .filter((m) => !q || `${m.code} ${m.name}`.toLowerCase().includes(q))
      .map((m) => {
        const machineDowntime = machines.length > 0 ? totalDowntime / machines.length : 0;
        const availability = availableMinutes > 0 ? Math.max(0, availableMinutes - machineDowntime) / availableMinutes : 0;
        const performance = performanceProxy / 100;
        const quality = globalQuality;
        const oee = availability * performance * quality;
        return { ...m, availability, performance, quality, oee };
      })
      .sort((a, b) => b.oee - a.oee);
  }, [machines, search, totalDowntime, availableMinutes, performanceProxy, globalQuality]);

  const avgOee = rows.length > 0 ? rows.reduce((s, r) => s + r.oee, 0) / rows.length : 0;

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('oee.title')}</h1>
      <p style={{ color: '#666' }}>{t('oee.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, flexWrap: 'wrap', alignItems: 'center' }}>
        <label>{t('oee.periodDays')}: <input type="number" min={1} max={365} value={periodDays} onChange={(e) => setPeriodDays(Math.max(1, Number(e.target.value)))} style={{ width: 70, padding: 4 }} /></label>
        <label>{t('oee.performanceProxy')}: <input type="number" min={0} max={100} value={performanceProxy} onChange={(e) => setPerformanceProxy(Math.max(0, Math.min(100, Number(e.target.value))))} style={{ width: 60, padding: 4 }} />%</label>
        <div><small>{t('oee.globalQuality')}</small><div style={{ fontWeight: 600 }}>{formatPercent(globalQuality, 1)}</div></div>
        <div><small>{t('oee.avgOee')}</small><div style={{ fontWeight: 700, fontSize: 18, color: avgOee > 0.75 ? '#2e7d32' : avgOee > 0.60 ? '#f9a825' : '#c62828' }}>{formatPercent(avgOee, 1)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('oee.searchPlaceholder') as string} style={{ padding: 6, minWidth: 180, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(rows, [
          { key: 'code', label: t('oee.machine') as string },
          { key: 'name', label: t('common.name') as string },
          { key: 'availability', label: t('oee.availability') as string, type: 'number' },
          { key: 'performance', label: t('oee.performance') as string, type: 'number' },
          { key: 'quality', label: t('oee.quality') as string, type: 'number' },
          { key: 'oee', label: 'OEE', type: 'number' },
        ], 'oee')} disabled={rows.length === 0} style={{ padding: '6px 12px' }}>
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('oee.machine')}</th>
              <th>{t('oee.availability')}</th>
              <th>{t('oee.performance')}</th>
              <th>{t('oee.quality')}</th>
              <th>OEE</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={6} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.map((r) => (
              <tr key={r.id}>
                <td><code>{r.code}</code> {r.name}</td>
                <td>{formatPercent(r.availability, 1)}</td>
                <td>{formatPercent(r.performance, 1)}</td>
                <td>{formatPercent(r.quality, 1)}</td>
                <td style={{ fontWeight: 700, color: r.oee > 0.75 ? '#2e7d32' : r.oee > 0.60 ? '#f9a825' : '#c62828' }}>{formatPercent(r.oee, 1)}</td>
                <td>
                  <div style={{ background: '#eee', height: 8, borderRadius: 4, overflow: 'hidden', width: 120 }}>
                    <div style={{ width: `${Math.round(r.oee * 100)}%`, height: '100%', background: r.oee > 0.75 ? '#2e7d32' : r.oee > 0.60 ? '#f9a825' : '#c62828' }} />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default MachineOEE;
