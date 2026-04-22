import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { machinesApi } from '../../services/api';
import { translateError } from '../../utils/translateError';
import { formatQuantity } from '../../utils/format';
import { exportToCsv } from '../../utils/export';

/**
 * P11.7 — Setup time.
 *
 * Filtered view of MachineDowntime events with category = Changeover (4).
 * Per-machine total setup minutes + count, highlights the outliers.
 */

type Event = {
  id: string;
  machineId: string;
  machineCode: string;
  machineName: string;
  start: string;
  end: string | null;
  durationMinutes: number | null;
  category: number;
  reason: string;
};

const CAT_CHANGEOVER = 4;

const SetupTime: React.FC = () => {
  const { t } = useTranslation();
  const [events, setEvents] = useState<Event[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const resp = await machinesApi.getDowntime();
        const env = resp.data as any;
        const data: Event[] = env?.data ?? env ?? [];
        if (!cancelled) setEvents(data.filter((e) => e.category === CAT_CHANGEOVER));
      } catch (err) {
        if (!cancelled) setError(translateError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const byMachine = useMemo(() => {
    const bucket = new Map<string, { machineId: string; machineCode: string; machineName: string; count: number; totalMinutes: number; avgMinutes: number }>();
    events.forEach((e) => {
      const existing = bucket.get(e.machineId) ?? { machineId: e.machineId, machineCode: e.machineCode, machineName: e.machineName, count: 0, totalMinutes: 0, avgMinutes: 0 };
      existing.count++;
      existing.totalMinutes += e.durationMinutes ?? 0;
      bucket.set(e.machineId, existing);
    });
    bucket.forEach((r) => { r.avgMinutes = r.count > 0 ? r.totalMinutes / r.count : 0; });
    return Array.from(bucket.values()).sort((a, b) => b.totalMinutes - a.totalMinutes);
  }, [events]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return byMachine;
    return byMachine.filter((r) => `${r.machineCode} ${r.machineName}`.toLowerCase().includes(q));
  }, [byMachine, search]);

  const totalSetup = byMachine.reduce((s, r) => s + r.totalMinutes, 0);

  return (
    <div style={{ padding: 16 }}>
      <h1>{t('setupTime.title')}</h1>
      <p style={{ color: '#666' }}>{t('setupTime.subtitle')}</p>

      {error && <div style={{ padding: 12, background: '#fdecea', color: '#a00', borderRadius: 4, marginBottom: 12 }}>{error}</div>}

      <div style={{ display: 'flex', gap: 16, marginBottom: 12, padding: 12, background: '#f5f5f5', borderRadius: 4, alignItems: 'center' }}>
        <div><small>{t('setupTime.totalEvents')}</small><div style={{ fontWeight: 600 }}>{events.length}</div></div>
        <div><small>{t('setupTime.totalMinutes')}</small><div style={{ fontWeight: 600, color: '#ef6c00' }}>{formatQuantity(totalSetup, 0)}</div></div>
        <input type="text" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('setupTime.searchPlaceholder') as string} style={{ padding: 6, minWidth: 200, marginLeft: 'auto' }} />
        <button onClick={() => exportToCsv(filtered, [
          { key: 'machineCode', label: t('setupTime.machine') as string },
          { key: 'machineName', label: t('common.name') as string },
          { key: 'count', label: t('setupTime.count') as string, type: 'number', decimals: 0 },
          { key: 'totalMinutes', label: t('setupTime.totalMinutes') as string, type: 'number', decimals: 0 },
          { key: 'avgMinutes', label: t('setupTime.avgMinutes') as string, type: 'number' },
        ], 'setup-time')}
          disabled={filtered.length === 0}
          style={{ padding: '6px 12px' }}
        >
          {t('common.exportExcel')}
        </button>
      </div>

      <div className="table-container">
        <table>
          <thead>
            <tr>
              <th>{t('setupTime.machine')}</th>
              <th>{t('setupTime.count')}</th>
              <th>{t('setupTime.totalMinutes')}</th>
              <th>{t('setupTime.avgMinutes')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={4} style={{ textAlign: 'center', padding: 20 }}>{t('common.loading')}</td></tr>}
            {!loading && filtered.length === 0 && <tr><td colSpan={4} style={{ textAlign: 'center', padding: 20, color: '#888' }}>{t('setupTime.empty')}</td></tr>}
            {!loading && filtered.map((r) => (
              <tr key={r.machineId}>
                <td><code>{r.machineCode}</code> {r.machineName}</td>
                <td>{r.count}</td>
                <td style={{ fontWeight: 600 }}>{formatQuantity(r.totalMinutes, 0)}</td>
                <td>{formatQuantity(r.avgMinutes, 1)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default SetupTime;
