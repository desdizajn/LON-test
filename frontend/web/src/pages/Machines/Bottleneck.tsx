import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi, masterDataApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.8 — Bottleneck analysis.
 *
 * Ranks machines by downtime minutes per period (lower = healthier). The
 * machine with the highest downtime + most frequent events is flagged as
 * the bottleneck that's choking throughput.
 */

type Machine = { id: string; code: string; name: string; workCenterId?: string };
type Event = { machineId: string; machineCode: string; machineName: string; durationMinutes: number | null; category: number };

type Row = {
  machineId: string;
  machineCode: string;
  machineName: string;
  events: number;
  totalDowntime: number;
  avgDowntime: number;
  rank: number;
};

const Bottleneck: React.FC = () => {
  const { t } = useTranslation();
  const [machines, setMachines] = useState<Machine[]>([]);
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const [mResp, eResp] = await Promise.all([
          masterDataApi.getMachines(),
          machinesApi.getDowntime(),
        ]);
        if (cancelled) return;
        setMachines((mResp.data as Machine[]) ?? []);
        const env = eResp.data as any;
        setEvents(env?.data ?? env ?? []);
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const rows = useMemo<Row[]>(() => {
    const bucket = new Map<string, Row>();
    machines.forEach((m) => bucket.set(m.id, {
      machineId: m.id, machineCode: m.code, machineName: m.name,
      events: 0, totalDowntime: 0, avgDowntime: 0, rank: 0,
    }));
    events.forEach((e) => {
      const row = bucket.get(e.machineId);
      if (!row) return;
      row.events++;
      row.totalDowntime += e.durationMinutes ?? 0;
    });
    bucket.forEach((r) => { r.avgDowntime = r.events > 0 ? r.totalDowntime / r.events : 0; });
    const sorted = Array.from(bucket.values()).sort((a, b) => b.totalDowntime - a.totalDowntime);
    sorted.forEach((r, i) => { r.rank = i + 1; });
    return sorted;
  }, [machines, events]);

  const worst = rows[0];

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('bottleneck.title')}</h1>
      <p style={{ color: '#666' }}>{t('bottleneck.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      {worst && worst.totalDowntime > 0 && (
        <div style={{ padding: 16, background: '#fff3e0', border: '2px solid #ef6c00', borderRadius: 8, marginBottom: 16 }}>
          <div style={{ fontSize: 12, color: '#666' }}>{t('bottleneck.current')}</div>
          <div style={{ fontSize: 20, fontWeight: 700, color: '#c62828' }}>
            ⚠ {worst.machineCode} · {worst.machineName}
          </div>
          <div style={{ fontSize: 13, color: '#444' }}>
            {t('bottleneck.downtimeSummary', { count: worst.events, minutes: formatQuantity(worst.totalDowntime, 0) })}
          </div>
        </div>
      )}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'center' }}>
        <span style={{ color: '#888' }}>{t('bottleneck.rowCount', { count: rows.length })}</span>
        <button onClick={() => exportToCsv(rows, [
          { key: 'rank', label: t('bottleneck.rank') as string, type: 'number', decimals: 0 },
          { key: 'machineCode', label: t('bottleneck.machine') as string },
          { key: 'machineName', label: t('common.name') as string },
          { key: 'events', label: t('bottleneck.events') as string, type: 'number', decimals: 0 },
          { key: 'totalDowntime', label: t('bottleneck.totalDowntime') as string, type: 'number', decimals: 0 },
          { key: 'avgDowntime', label: t('bottleneck.avgDowntime') as string, type: 'number' },
        ], 'bottleneck')}
          disabled={rows.length === 0}
          style={{ padding: '6px 12px', marginLeft: 'auto' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('bottleneck.rank')}</th>
              <th>{t('bottleneck.machine')}</th>
              <th>{t('bottleneck.events')}</th>
              <th>{t('bottleneck.totalDowntime')}</th>
              <th>{t('bottleneck.avgDowntime')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={5} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && rows.map((r) => (
              <tr key={r.machineId} style={r.rank <= 3 ? { background: '#fff8e1' } : undefined}>
                <td><strong>#{r.rank}</strong></td>
                <td><code>{r.machineCode}</code> {r.machineName}</td>
                <td>{r.events}</td>
                <td style={{ color: r.totalDowntime > 0 ? '#c62828' : undefined }}>{formatQuantity(r.totalDowntime, 0)}</td>
                <td>{formatQuantity(r.avgDowntime, 1)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default Bottleneck;
